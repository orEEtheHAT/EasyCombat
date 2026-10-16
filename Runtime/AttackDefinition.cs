using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum DamageDelivery { Hitscan, Projectile, Area }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum ContactBehavior { StopOnFirstHit, PierceTargets }

    [CreateAssetMenu(menuName = "EasyCombat/Weapon", fileName = "Weapon")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AttackDefinition : ScriptableObject
    {
        public string DisplayName;
        public CombatTagDefinition[] Tags = Array.Empty<CombatTagDefinition>();
        public string Label => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;
        public DamageDelivery Delivery = DamageDelivery.Hitscan;
        [Header("Damage")]
        public DamageTypeDefinition DamageType;
        [FormerlySerializedAs("Damage"), Min(0.001f)] public float MinimumDamage = 25f;
        [Min(0.001f)] public float MaximumDamage = 25f;
        [Tooltip("When nonempty, replaces the legacy single channel above. At most eight independent channels.")]
        public DamageChannel[] DamageChannels = Array.Empty<DamageChannel>();
        [Tooltip("Optional shared formula. Replaces numeric channels. Target HP is resolved at each receiver.")]
        public DamageFormulaDefinition Formula;
        public bool UsesFormula => !StatusOnly && Formula != null;
        public bool StatusOnly;
        [Range(0f, 1f)] public float CriticalChance;
        [Min(1f)] public float CriticalMultiplier = 2f;
        public StatusApplication[] OnHitStatuses = Array.Empty<StatusApplication>();
        public StatusApplyPolicy StatusPolicy;
        [Tooltip("Explosion at each unique contact, or geometry for Area delivery. Replaces direct damage. Separate explosions may overlap.")]
        public AreaDefinition Area;
        [Tooltip("Optional persistent field created once per qualifying contact, not once per explosion victim. Requires an Area Pool on the emitter.")]
        public AreaSpawnDefinition ImpactArea;
        [Header("Fire")]
        [Min(0f), Tooltip("Shots per minute. Zero disables the cooldown (useful for scripted attacks/tests).")]
        public float RoundsPerMinute = 300f;
        [Min(0.001f)] public float Range = 40f;
        public LayerMask HitMask = ~0;
        public QueryTriggerInteraction Triggers = QueryTriggerInteraction.Ignore;
        [Header("Contact")]
        [Min(0f)] public float HitscanRadius;
        public ContactBehavior ContactBehavior;
        [Min(0), Tooltip("Total distinct targets, including the first. Zero means no target limit. World geometry always stops the attack.")]
        public int MaximumTargets;
        [Header("Projectile")]
        [Min(0.001f)] public float ProjectileSpeed = 18f;
        [Min(0.001f)] public float ProjectileLifetime = 4f;
        [Min(0f)] public float ProjectileRadius = 0.12f;
        [Tooltip("Optional projectile prefab owned by this weapon. Falls back to the pool's prefab.")]
        public DamageProjectile ProjectilePrefab;
        public float HitRadius => Delivery == DamageDelivery.Projectile ? ProjectileRadius : HitscanRadius;
        [Header("Optional status stat keys")]
        public CombatStatDefinition DamageModifierStat;
        public CombatStatDefinition FireRateModifierStat;
        [SerializeField, HideInInspector, FormerlySerializedAs("Cooldown")] private float legacyCooldown = -1f;
        [SerializeField, HideInInspector] private int schemaVersion;

        // Source-compatible convenience properties for the first foundation's API.
        public float Damage { get => MinimumDamage; set { MinimumDamage = value; MaximumDamage = value; } }
        public float Cooldown { get => RoundsPerMinute > 0f ? 60f / RoundsPerMinute : 0f;
            set => RoundsPerMinute = value > 0f ? 60f / value : 0f; }

        private void OnEnable() => UpgradeLegacyData();
        public void UpgradeLegacyData()
        {
            if (schemaVersion >= 1) return;
            MaximumDamage = MinimumDamage;
            if (legacyCooldown >= 0f) Cooldown = legacyCooldown;
            legacyCooldown = -1f;
            schemaVersion = 1;
        }

        /// <summary>Roll once per shot. Samples are explicit for deterministic simulation/testing.</summary>
        public DamageInfo RollDamage(GameObject source, Vector3 point, Vector3 direction,
            float damageSample, float criticalSample, float outgoingMultiplier = 1f)
            => Roll(source, point, direction, default, damageSample, criticalSample, outgoingMultiplier, false, default);

        public DamageInfo RollDamage(GameObject source, Vector3 point, Vector3 direction,
            ReadOnlySpan<float> damageSamples, float criticalSample, float outgoingMultiplier = 1f)
        {
            if (damageSamples.Length < ChannelCount) throw new ArgumentException("Provide one sample per damage channel.");
            return Roll(source, point, direction, damageSamples, 0, criticalSample, outgoingMultiplier, false, default);
        }
        public DamageInfo RollRandom(GameObject source, Vector3 point, Vector3 direction, float outgoingMultiplier = 1f, DamageOrigin origin = default)
            => Roll(source, point, direction, default, 0, CombatRandom.Value, outgoingMultiplier, true, origin);

        private DamageInfo Roll(GameObject source, Vector3 point, Vector3 direction, ReadOnlySpan<float> samples,
            float sample, float criticalSample, float outgoingMultiplier, bool random, DamageOrigin origin)
        {
            if (ChannelCount > DamageAmounts.Capacity) throw new InvalidOperationException("At most eight damage channels per attack.");
            var critical = !StatusOnly && (CriticalChance >= 1f || (CriticalChance > 0f && criticalSample < CriticalChance));
            var multiplier = outgoingMultiplier * (critical ? CriticalMultiplier : 1);
            if (origin.CastId == 0) origin = DamageOrigin.Capture(source, this);
            if (UsesFormula)
            {
                if (!Formula.IsValidForAttack || !Formula.TryPrepare(source, null, default, false, false, critical, CriticalMultiplier,
                    outgoingMultiplier, out var prepared, samples, sample, random)) return default;
                return new DamageInfo(prepared, source, point, direction, false, OnHitStatuses, origin, StatusPolicy);
            }
            var parts = new DamageAmounts();
            for (var i = 0; i < ChannelCount; i++)
            {
                var channel = GetChannel(i);
                var value = random ? CombatRandom.Value : samples.Length > 0 ? samples[i] : sample;
                parts.Add(channel.Type, Mathf.Lerp(channel.Minimum, channel.Maximum, Mathf.Clamp01(value)) * multiplier);
            }
            if (origin.CastId == 0) origin = DamageOrigin.Capture(source, this);
            return new DamageInfo(parts, source, point, direction, critical, false, OnHitStatuses, origin,
                StatusOnly ? StatusApplyPolicy.OnContact : StatusPolicy);
        }

        public int ChannelCount => StatusOnly ? 0 : UsesFormula ? Formula.Terms?.Length ?? 0 : DamageChannels != null && DamageChannels.Length > 0 ? DamageChannels.Length : 1;
        public DamageChannel GetChannel(int index) => DamageChannels != null && DamageChannels.Length > 0
            ? DamageChannels[index] : index == 0 ? new DamageChannel(DamageType, MinimumDamage, MaximumDamage) : throw new ArgumentOutOfRangeException(nameof(index));
        public float TotalMinimum { get { if (UsesFormula) return 0; var total = 0f; for (var i = 0; i < ChannelCount; i++) total += GetChannel(i).Minimum; return total; } }
        public float TotalMaximum { get { if (UsesFormula) return 0; var total = 0f; for (var i = 0; i < ChannelCount; i++) total += GetChannel(i).Maximum; return total; } }
        private bool ValidPayload
        {
            get
            {
                if (UsesFormula) return Formula.IsValidForAttack;
                if (ChannelCount > DamageAmounts.Capacity) return false;
                for (var i = 0; i < ChannelCount; i++) if (!GetChannel(i).IsValid) return false;
                if (StatusOnly)
                {
                    if (OnHitStatuses == null) return false;
                    foreach (var application in OnHitStatuses)
                        if (application.Status != null && application.Status.IsValid && application.Chance > 0 && application.Chance <= 1) return true;
                    return false;
                }
                return CombatMath.Positive(TotalMinimum) && CombatMath.Positive(TotalMaximum)
                    && CombatMath.Finite(TotalMaximum * CriticalMultiplier);
            }
        }
        public bool IsValid => (Tags == null || Tags.Length <= CombatTagSet.Capacity) && ValidPayload && CombatMath.Positive(Range)
            && CombatMath.Finite(RoundsPerMinute) && RoundsPerMinute >= 0f
            && CombatMath.Finite(CriticalChance) && CriticalChance >= 0f && CriticalChance <= 1f
            && CombatMath.Finite(CriticalMultiplier) && CriticalMultiplier >= 1f
            && (Area == null || Area.IsValid)
            // Area pulse attacks cannot spawn more fields; also breaks invalid recipe cycles during validation.
            && (ImpactArea == null || (Delivery != DamageDelivery.Area && ImpactArea.IsValid))
            && (Delivery == DamageDelivery.Area || (CombatMath.Finite(HitRadius) && HitRadius >= 0f
                && (ContactBehavior == ContactBehavior.StopOnFirstHit || ContactBehavior == ContactBehavior.PierceTargets)
                && MaximumTargets >= 0))
            && (Delivery == DamageDelivery.Hitscan || (Delivery == DamageDelivery.Area && Area != null) || (Delivery == DamageDelivery.Projectile
                && CombatMath.Positive(ProjectileSpeed) && CombatMath.Positive(ProjectileLifetime)
                && CombatMath.Finite(ProjectileRadius) && ProjectileRadius >= 0f));
    }
}
