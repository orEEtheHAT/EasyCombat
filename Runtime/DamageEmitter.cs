using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct ShotInfo
    {
        public readonly Vector3 Origin, End;
        public readonly DamageDelivery Delivery;
        public readonly float Radius;
        public ShotInfo(Vector3 origin, Vector3 end, DamageDelivery delivery, float radius = 0)
        { Origin = origin; End = end; Delivery = delivery; Radius = radius; }
    }

    [AddComponentMenu("EasyCombat/Damage Emitter")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageEmitter : MonoBehaviour
    {
        [SerializeField] private AttackDefinition attack;
        [Tooltip("All colliders under this transform are excluded. Assign the character root, not a common scene parent.")]
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private DamageAreaPool areaPool;
        private readonly AttackTrace trace = new AttackTrace();
        private readonly AreaDamageQuery areaQuery = new AreaDamageQuery();
        private double nextShotTime = double.NegativeInfinity;
        private Transform resolvedOwner;
        private StatusController ownerStatuses;
        private Action<DamageImpact> impactCallback;
        private bool firing;
        public AttackDefinition Attack { get => attack; set => attack = value; }
        public Transform OwnerRoot { get => ownerRoot != null ? ownerRoot : transform; set => ownerRoot = value; }
        public ProjectilePool Pool { get => projectilePool; set => projectilePool = value; }
        public DamageAreaPool AreaPool { get => areaPool; set => areaPool = value; }
        public event Action<ShotInfo> Fired;
        public event Action<DamageImpact> Impacted;

        public bool TryFire(Vector3 origin, Vector3 direction)
        {
            // A callback may request another shot with zero cooldown. Keep the current trace atomic.
            if (firing) return false;
            firing = true;
            try { return Fire(origin, direction); }
            finally { trace.Clear(); firing = false; }
        }

        private bool Fire(Vector3 origin, Vector3 direction)
        {
            if (!isActiveAndEnabled || attack == null || !attack.IsValid || CombatClock.Now < nextShotTime
                || !CombatMath.Finite(origin) || !CombatMath.Finite(direction) || direction.sqrMagnitude < 0.000001f) return false;
            if (attack.ImpactArea != null && (areaPool == null || !areaPool.isActiveAndEnabled)) return false;
            direction.Normalize();
            var sourceRoot = OwnerRoot;
            if (resolvedOwner != sourceRoot)
            {
                resolvedOwner = sourceRoot;
                ownerStatuses = sourceRoot.GetComponent<StatusController>();
            }
            var damageMultiplier = ownerStatuses != null ? ownerStatuses.GetStatMultiplier(attack.DamageModifierStat) : 1f;
            var rateMultiplier = ownerStatuses != null ? ownerStatuses.GetStatMultiplier(attack.FireRateModifierStat) : 1f;
            if (!CombatMath.Positive(damageMultiplier) || !CombatMath.Positive(rateMultiplier)) return false;
            var delivery = attack.Delivery;
            var radius = delivery == DamageDelivery.Area ? 0 : attack.HitRadius;
            var damage = attack.RollRandom(sourceRoot.gameObject, origin, direction, damageMultiplier);
            if (!damage.IsValid) return false;
            var area = new AreaSettings(attack.Area);
            if (impactCallback == null) impactCallback = ReportImpact;
            var end = origin + direction * attack.Range;
            // Reserve cooldown before invoking any damage/event callbacks.
            nextShotTime = CombatClock.Now + attack.Cooldown / rateMultiplier;
            if (delivery == DamageDelivery.Area)
            {
                end = origin;
                areaQuery.Apply(origin, direction, damage, area, sourceRoot, impactCallback);
            }
            else if (delivery == DamageDelivery.Projectile)
            {
                if (projectilePool == null || projectilePool.Spawn(origin, direction, damage, sourceRoot, attack, impactCallback, areaPool) == null)
                {
                    nextShotTime = double.NegativeInfinity;
                    return false;
                }
            }
            else
            {
                trace.Begin(attack, damage, sourceRoot, direction, areaPool);
                trace.Advance(origin, attack.Range, impactCallback, out end);
                trace.CompleteHitscan(end, impactCallback);
            }
            Fired?.Invoke(new ShotInfo(origin, end, delivery, radius));
            return true;
        }

        private void ReportImpact(DamageImpact impact) => Impacted?.Invoke(impact);
        public void ResetCooldown() => nextShotTime = double.NegativeInfinity;
        public void RefreshOwner() => resolvedOwner = null;
    }
}
