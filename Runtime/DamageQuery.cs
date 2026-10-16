using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>3D physics adapter. Reuses buffers; grows only when a query saturates them.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageQuery
    {
        private RaycastHit[] hits = new RaycastHit[16];
        private Collider[] overlaps = new Collider[16];
        private readonly List<DamageContact> contacts = new List<DamageContact>(16);

        internal DamageContact GetContact(int index) => contacts[index];
        internal void ClearContacts() => contacts.Clear();

        internal int Collect(Vector3 origin, Vector3 direction, float distance, float radius,
            LayerMask mask, QueryTriggerInteraction triggers, Transform owner)
        {
            contacts.Clear();
            var overlapCount = 0;
            try
            {
                while (true)
                {
                    overlapCount = Physics.OverlapSphereNonAlloc(origin, Mathf.Max(radius, .001f), overlaps, mask, triggers);
                    if (overlapCount < overlaps.Length) break;
                    Array.Resize(ref overlaps, overlaps.Length * 2);
                }
                for (var i = 0; i < overlapCount; i++)
                {
                    var collider = overlaps[i];
                    if (collider == null || IsOwner(collider, owner)) continue;
                    contacts.Add(new DamageContact(collider, 0, collider.ClosestPoint(origin), -direction));
                }
                if (distance > 0)
                {
                    int count;
                    while (true)
                    {
                        count = radius > 0
                            ? Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, mask, triggers)
                            : Physics.RaycastNonAlloc(origin, direction, hits, distance, mask, triggers);
                        if (count < hits.Length) break;
                        Array.Resize(ref hits, hits.Length * 2);
                    }
                    for (var i = 0; i < count; i++)
                    {
                        var hit = hits[i];
                        var collider = hit.collider;
                        if (collider == null || IsOwner(collider, owner)) continue;
                        var alreadyOverlapping = false;
                        for (var j = 0; j < overlapCount; j++)
                            if (overlaps[j] == collider) { alreadyOverlapping = true; break; }
                        if (!alreadyOverlapping) contacts.Add(new DamageContact(collider, hit.distance, hit.point, hit.normal));
                    }
                }
                contacts.Sort(DamageContact.Comparer);
                return contacts.Count;
            }
            finally { Array.Clear(overlaps, 0, overlapCount); }
        }

        private static bool IsOwner(Collider collider, Transform owner) =>
            owner != null && (collider.transform == owner || collider.transform.IsChildOf(owner));

        public bool Cast(Vector3 origin, Vector3 direction, float distance, float radius,
            LayerMask mask, QueryTriggerInteraction triggers, Transform owner, out RaycastHit closest)
        {
            closest = default;
            if (!CombatMath.Finite(origin) || !CombatMath.Finite(direction) || direction.sqrMagnitude < 0.000001f
                || !CombatMath.Positive(distance) || !CombatMath.Finite(radius) || radius < 0f) return false;
            direction.Normalize();
            int count;
            while (true)
            {
                count = radius > 0f
                    ? Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance, mask, triggers)
                    : Physics.RaycastNonAlloc(origin, direction, hits, distance, mask, triggers);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            var found = false;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                if (hits[i].collider == null || IsOwner(hits[i].collider, owner) || hits[i].distance >= nearest) continue;
                closest = hits[i];
                nearest = hits[i].distance;
                found = true;
            }
            return found;
        }

        /// <summary>Sphere casts do not detect initial overlaps. Test these before each sweep.</summary>
        public Collider InitialOverlap(Vector3 position, float radius, LayerMask mask,
            QueryTriggerInteraction triggers, Transform owner)
        {
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(position, Mathf.Max(radius, 0.001f), overlaps, mask, triggers);
                if (count < overlaps.Length) break;
                Array.Resize(ref overlaps, overlaps.Length * 2);
            }
            Collider best = null;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var collider = overlaps[i];
                if (collider == null || IsOwner(collider, owner)) continue;
                var distance = (collider.ClosestPoint(position) - position).sqrMagnitude;
                if (distance < nearest || (distance == nearest && best != null && collider.GetInstanceID() < best.GetInstanceID()))
                {
                    nearest = distance;
                    best = collider;
                }
            }
            return best;
        }

        public static DamageImpact Apply(Collider collider, Vector3 point, Vector3 normal, DamageInfo damage)
        {
            var request = damage.At(point, damage.Direction);
            IDamageable receiver = collider != null ? collider.GetComponentInParent<Hurtbox>() : null;
            if (receiver == null && collider != null) receiver = collider.GetComponentInParent<IDamageable>();
            var result = receiver != null ? receiver.ApplyDamage(request) : DamageResult.Rejected(request, DamageRejection.NoReceiver);
            return new DamageImpact(collider, point, normal, result);
        }
    }

    internal readonly struct DamageContact
    {
        public readonly Collider Collider;
        public readonly MonoBehaviour Receiver;
        public readonly CombatHitKey HitKey;
        private readonly long life;
        private readonly Hurtbox zone;
        public readonly Vector3 Point, Normal;
        public readonly float Distance;
        public readonly bool WasTarget;
        private readonly int colliderId;
        public static readonly IComparer<DamageContact> Comparer = new ContactComparer();

        public DamageContact(Collider collider, float distance, Vector3 point, Vector3 normal)
        {
            Collider = collider; Distance = distance; Point = point; Normal = normal;
            colliderId = collider.GetInstanceID();
            zone = collider.GetComponentInParent<Hurtbox>();
            Receiver = zone != null ? zone.Receiver : collider.GetComponentInParent<IDamageable>() as MonoBehaviour;
            WasTarget = Receiver != null && !(Receiver is Hurtbox) && Receiver is IDamageable;
            if (!WasTarget) Receiver = null;
            HitKey=WasTarget ? new CombatHitKey(Receiver) : default;
            life=WasTarget ? CombatTargetGroups.Life(Receiver) : 0;
        }
        public bool IsCurrent=>!WasTarget || Receiver!=null && (life==0 || CombatTargetGroups.Life(Receiver)==life) && HitKey.Equals(new CombatHitKey(Receiver));

        public DamageImpact Apply(DamageInfo damage)
        {
            var result = zone != null ? zone.ApplyDamage(damage)
                : Receiver != null ? ((IDamageable)Receiver).ApplyDamage(damage)
                : DamageResult.Rejected(damage, DamageRejection.NoReceiver);
            return new DamageImpact(Collider, Point, Normal, result);
        }

        private sealed class ContactComparer : IComparer<DamageContact>
        {
            public int Compare(DamageContact a, DamageContact b)
            {
                var order = a.Distance.CompareTo(b.Distance);
                if (order != 0) return order;
                // At an equal travel distance a wall wins over a target, including initial overlaps.
                order = a.WasTarget.CompareTo(b.WasTarget);
                return order != 0 ? order : a.colliderId.CompareTo(b.colliderId);
            }
        }
    }
}
