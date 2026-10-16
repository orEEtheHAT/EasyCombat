using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    internal struct AreaCandidate
    {
        public Collider Collider;
        public MonoBehaviour Receiver;
        public Hurtbox Zone;
        public Vector3 Point;
        public float Distance;
        public long LifeId;
    }

    /// <summary>Reusable area selector shared by direct AOE and overlap arbitration.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AreaDamageQuery
    {
        private Collider[] overlaps = new Collider[32];
        private readonly DamageQuery visibility = new DamageQuery();
        private readonly Dictionary<MonoBehaviour, int> indices = new Dictionary<MonoBehaviour, int>(32);
        private readonly List<AreaCandidate> candidates = new List<AreaCandidate>(32);
        private bool busy;
        internal AreaCandidate GetCandidate(int index) => candidates[index];
        internal void ClearSelection() { indices.Clear(); candidates.Clear(); busy = false; }

        internal int Select(Vector3 center, Vector3 forward, DamageInfo damage, AreaSettings settings, Transform owner)
        {
            if (busy) throw new InvalidOperationException("Do not recursively reuse the same area query from its callbacks.");
            if (!settings.Enabled || !damage.IsValid || !CombatMath.Finite(center) || !CombatMath.Finite(forward)
                || (settings.Shape == AreaShape.Cone && forward.sqrMagnitude < .000001f)) return 0;
            busy = true;
            var count = 0;
            try
            {
                forward.Normalize();
                while (true)
                {
                    count = Physics.OverlapSphereNonAlloc(center, settings.Radius, overlaps, settings.TargetMask, settings.Triggers);
                    if (count < overlaps.Length) break;
                    Array.Resize(ref overlaps, overlaps.Length * 2);
                }
                for (var i = 0; i < count; i++)
                {
                    var collider = overlaps[i];
                    if (!TryResolve(collider, out var receiver, out var zone) || !receiver.isActiveAndEnabled) continue;
                    if (!settings.AffectOwner && IsOwner(collider, receiver, owner, damage.Origin.Actor)) continue;
                    if (!AllowsTeam(receiver, settings.Targets, damage.Origin)) continue;
                    var point = collider.ClosestPoint(center);
                    var delta = point - center;
                    var distance = delta.magnitude;
                    if (distance > settings.Radius) continue;
                    if (settings.DistanceFalloff && Mathf.Lerp(1, settings.EdgeMultiplier, distance / settings.Radius) <= 0) continue;
                    if (settings.Shape == AreaShape.Cone && distance > .0001f && Vector3.Dot(forward, delta / distance) < settings.ConeCosine) continue;
                    if (settings.RequireLineOfSight && distance > .001f
                        && visibility.Cast(center, delta / distance, distance, 0, settings.ObstacleMask, QueryTriggerInteraction.Ignore, owner, out var obstruction)
                        && (!TryResolve(obstruction.collider, out var blockedReceiver, out _) || blockedReceiver != receiver)) continue;
                    var candidate = new AreaCandidate { Collider = collider, Receiver = receiver, Zone = zone, Point = point,
                        Distance = distance, LifeId = receiver is Health health ? health.Identity.Id : 0 };
                    if (indices.TryGetValue(receiver, out var existing))
                    {
                        var old = candidates[existing];
                        if (distance < old.Distance || (distance == old.Distance && collider.GetInstanceID() < old.Collider.GetInstanceID())) candidates[existing] = candidate;
                    }
                    else { indices.Add(receiver, candidates.Count); candidates.Add(candidate); }
                }
                return candidates.Count;
            }
            catch { ClearSelection(); throw; }
            finally { Array.Clear(overlaps, 0, count); }
        }
        public int Apply(Vector3 center, Vector3 forward, DamageInfo damage, AreaSettings settings, Transform owner = null, Action<DamageImpact> onImpact = null)
        {
            var count = Select(center, forward, damage, settings, owner);
            CombatReactionQueue.Enter();
            try
            {
                var affected = 0;
                for (var i = 0; i < count; i++) if (ApplyCandidate(candidates[i], center, forward, damage, settings, onImpact)) affected++;
                return affected;
            }
            finally { ClearSelection(); CombatReactionQueue.Exit(); }
        }
        internal static bool ApplyCandidate(in AreaCandidate candidate, Vector3 center, Vector3 forward, DamageInfo damage,
            AreaSettings settings, Action<DamageImpact> onImpact)
        {
            if (candidate.Receiver == null || !candidate.Receiver.isActiveAndEnabled || candidate.Collider == null
                || !candidate.Collider.enabled || !candidate.Collider.gameObject.activeInHierarchy
                || candidate.Receiver is Health h && h.Identity.Id != candidate.LifeId) return false;
            var multiplier = settings.DistanceFalloff ? Mathf.Lerp(1, settings.EdgeMultiplier, candidate.Distance / settings.Radius) : 1;
            if (multiplier <= 0) return false;
            var direction = candidate.Point - center;
            var request = damage.Scale(multiplier).At(candidate.Point, direction.sqrMagnitude > .000001f ? direction.normalized : forward);
            var result = settings.UseHurtboxMultiplier && candidate.Zone != null
                ? candidate.Zone.ApplyDamage(request) : ((IDamageable)candidate.Receiver).ApplyDamage(request);
            onImpact?.Invoke(new DamageImpact(candidate.Collider, candidate.Point, -request.Direction, result));
            return result.HitApplied || result.StatusesApplied > 0;
        }
        private static bool IsOwner(Collider collider, MonoBehaviour receiver, Transform owner, CombatActorIdentity source)
        {
            if (owner != null && (collider.transform == owner || collider.transform.IsChildOf(owner)
                || receiver.transform == owner || receiver.transform.IsChildOf(owner))) return true;
            return source.Id != 0 && receiver is Health health && health.Identity.Id == source.Id;
        }
        private static bool AllowsTeam(MonoBehaviour target, AreaTargetFilter filter, DamageOrigin origin)
        {
            if (filter == AreaTargetFilter.All) return true;
            var team = target.GetComponentInParent<CombatTeam>();
            var allies = origin.HasTeam && team != null && team.Team == origin.Team;
            return filter == AreaTargetFilter.Allies ? allies : !allies;
        }
        private static bool TryResolve(Collider collider, out MonoBehaviour receiver, out Hurtbox zone)
        {
            receiver = null; zone = collider != null ? collider.GetComponentInParent<Hurtbox>() : null;
            if (zone != null) { if (!zone.isActiveAndEnabled) return false; receiver = zone.Receiver; }
            else if (collider != null) receiver = collider.GetComponentInParent<IDamageable>() as MonoBehaviour;
            return receiver != null && !(receiver is Hurtbox) && receiver is IDamageable;
        }
    }
}
