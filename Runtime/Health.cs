using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("EasyCombat/Health")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.001f)] private float maximumHealth = 100f;
        [SerializeField, Tooltip("Optional reusable initial health/resistance configuration.")]
        private HealthDefinition definition;
        [SerializeField, Tooltip("Used when no Health Profile is assigned.")] private ResistanceProfile resistances;
        [SerializeField] private HitRulesDefinition hitRules;
        [SerializeField] private DefenseProfile defenseProfile;
        private DamageDefenseState defense;
        private float manualInvulnerability, hitInvulnerability;
        private bool invulnerable, applyingDamage, applyingHealing;
        internal int DefenseSchedulerIndex = -1;
        private float currentHealth;
        private bool initialized;
        private StatusController statuses;
        private CombatActorIdentity identity;
        public CombatActorIdentity Identity { get { EnsureInitialized(); return identity; } }

        public float Maximum { get { EnsureInitialized(); return maximumHealth; } }
        public HealthDefinition Definition => definition;
        public HitRulesDefinition HitRules => definition != null ? definition.HitRules : hitRules;
        public DefenseProfile DefenseProfile => definition != null ? definition.Defense : defenseProfile;
        public DamageDefenseState Defense { get { EnsureInitialized(); return defense; } }
        public float ShieldCurrent => Defense != null ? defense.ShieldCurrent : 0;
        public float ShieldMaximum => Defense != null ? defense.ShieldMaximum : 0;
        public float InvulnerabilityRemaining => Mathf.Max(manualInvulnerability, hitInvulnerability);
        public bool Invulnerable { get => invulnerable; set => invulnerable = value; }
        public ResistanceProfile Resistances => definition != null ? definition.Resistances : resistances;
        public StatusController Statuses => statuses;
        internal void AttachStatuses(StatusController controller) => statuses = controller;
        internal void DetachStatuses(StatusController controller) { if (statuses == controller) statuses = null; }
        public float Current { get { EnsureInitialized(); return currentHealth; } }
        public float Normalized => Current / Maximum;
        public bool IsAlive => Current > 0f;

        private readonly CombatSignal<HealthChange> changedSignal = new CombatSignal<HealthChange>();
        public event Action<HealthChange> Changed { add => changedSignal.Add(value); remove => changedSignal.Remove(value); }
        private readonly CombatSignal<DamageResult> damagedSignal = new CombatSignal<DamageResult>();
        public event Action<DamageResult> Damaged { add => damagedSignal.Add(value); remove => damagedSignal.Remove(value); }
        private readonly CombatSignal<Health> diedSignal = new CombatSignal<Health>();
        public event Action<Health> Died { add => diedSignal.Add(value); remove => diedSignal.Remove(value); }
        private readonly CombatSignal<Health> revivedSignal = new CombatSignal<Health>();
        public event Action<Health> Revived { add => revivedSignal.Add(value); remove => revivedSignal.Remove(value); }
        private readonly CombatSignal<Health> defenseChangedSignal = new CombatSignal<Health>();
        public event Action<Health> DefenseChanged { add => defenseChangedSignal.Add(value); remove => defenseChangedSignal.Remove(value); }
        private readonly CombatSignal<HealingResult> healedSignal = new CombatSignal<HealingResult>();
        public event Action<HealingResult> Healed { add => healedSignal.Add(value); remove => healedSignal.Remove(value); }

        private void Awake() => EnsureInitialized();
        private void OnEnable() { EnsureInitialized(); ReconcileDefenseScheduler(); }
        private void OnDisable() => DefenseScheduler.Unregister(this);
        private void OnValidate()
        {
            if (!CombatMath.Positive(maximumHealth)) maximumHealth = 100f;
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            if (definition != null && definition.IsValid) maximumHealth = definition.MaximumHealth;
            if (!CombatMath.Positive(maximumHealth)) maximumHealth = 100f;
            currentHealth = maximumHealth;
            identity = new CombatActorIdentity(CombatEvents.NextId(), gameObject.name);
            initialized = true;
            if (DefenseProfile != null && DefenseProfile.IsValid) defense = new DamageDefenseState(DefenseProfile);
        }

        public void ApplyDefinition(HealthDefinition profile, bool resetHealth = true)
        {
            if (profile == null || !profile.IsValid) throw new ArgumentException("A valid health profile is required.", nameof(profile));
            EnsureInitialized();
            definition = profile;
            defense = profile.Defense != null ? new DamageDefenseState(profile.Defense) : null;
            manualInvulnerability = hitInvulnerability = 0;
            SetMaximum(profile.MaximumHealth);
            if (resetHealth) ResetToFull();
            else { ReconcileDefenseScheduler(); defenseChangedSignal.Publish(this); }
        }

        public float GetResistance(DamageTypeDefinition type)
        {
            var baseReduction = Resistances != null ? Resistances.GetReduction(type) : 0f;
            var statusReduction = statuses != null ? statuses.GetResistanceModifier(type) : 0f;
            return Mathf.Clamp(baseReduction + statusReduction, -1f, 1f);
        }

        public DamageResult ApplyDamage(DamageInfo damage)
        {
            // Do not recursively publish rejections to a journal callback which caused the recursion.
            if (applyingDamage) return DamageResult.Rejected(damage, DamageRejection.Reentrant);
            applyingDamage = true;
            try { return ApplyDamageCore(damage); }
            finally { applyingDamage = false; }
        }
        private DamageResult ApplyDamageCore(DamageInfo damage)
        {
            EnsureInitialized();
            if (!damage.IsValid)
                return Reject(damage, DamageRejection.InvalidAmount);
            if (!isActiveAndEnabled)
                return Reject(damage, DamageRejection.Disabled);
            if (!IsAlive)
                return Reject(damage, DamageRejection.Dead);
            var rules = HitRules;
            if ((rules != null && !rules.IsValid) || (DefenseProfile != null && !DefenseProfile.IsValid))
                return Reject(damage, DamageRejection.InvalidDefense);
            if (invulnerable || manualInvulnerability > 0 || hitInvulnerability > 0
                && (!damage.IsPeriodic || rules != null && rules.HitInvulnerabilityAffectsPeriodic))
                return Reject(damage, DamageRejection.Invulnerable);
            if (rules != null)
            {
                var denied = rules.Check(this, damage);
                if (denied != DamageRejection.None) return Reject(damage, denied);
            }
            if (damage.HasFormula)
            {
                if (!damage.TryResolve(currentHealth, maximumHealth, out var resolved)) return Reject(damage, DamageRejection.InvalidAmount);
                damage = resolved;
            }

            // Resolve every channel before changing HP: invalid packets cannot apply partial damage.
            var incoming = damage.Parts;
            var shieldParts = new DamageAmounts(); var armorParts = new DamageAmounts();
            var defenses = new DefenseBreakdown(); var context = new DefenseContext(this, damage);
            defense?.Begin();
            if (defense != null && !defense.Process(DefenseStage.BeforeResistance, context, ref incoming, ref shieldParts, ref armorParts, ref defenses))
                return Reject(damage, DamageRejection.InvalidDefense);
            var reductions = new DamageAmounts();
            var effective = new DamageAmounts();
            for (var i = 0; i < incoming.Count; i++)
            {
                var part = incoming[i];
                var reduction = GetResistance(part.Type);
                reductions.Add(part.Type, reduction);
                effective.Add(part.Type, part.Amount * (1f - reduction));
            }
            var effectiveAmount = effective.Total;
            if (!CombatMath.Finite(effectiveAmount))
                return Reject(damage, DamageRejection.InvalidAmount);
            var healthParts = effective;
            if (defense != null && !defense.Process(DefenseStage.AfterResistance, context, ref healthParts, ref shieldParts, ref armorParts, ref defenses))
                return Reject(damage, DamageRejection.InvalidDefense);
            var healthDamage = healthParts.Total;
            var shieldAbsorbed = shieldParts.Total; var armorPrevented = armorParts.Total;
            var resistanceDelta = incoming.Total - effectiveAmount;
            defense?.Commit();
            var previous = currentHealth;
            currentHealth = Mathf.Max(0f, previous - healthDamage);
            var applied = previous - currentHealth;
            var killed = currentHealth == 0f;
            var breakdown = new DamageBreakdown();
            var remaining = applied;
            var lastPositive = -1;
            for (var i = 0; i < healthParts.Count; i++) if (healthParts[i].Amount > 0) lastPositive = i;
            for (var i = 0; i < effective.Count; i++)
            {
                var share = healthDamage > 0 ? Mathf.Min(remaining, applied * (healthParts[i].Amount / healthDamage)) : 0;
                if (i == lastPositive) share = remaining;
                remaining = Mathf.Max(0, remaining - share);
                breakdown.Add(new DamagePartResult(damage.Parts[i].Type, damage.Parts[i].Amount,
                    reductions[i].Amount, effective[i].Amount, share, healthParts[i].Amount,
                    shieldParts.Count > i ? shieldParts[i].Amount : 0, armorParts.Count > i ? armorParts[i].Amount : 0,
                    incoming[i].Amount - effective[i].Amount));
            }
            if (rules != null && (applied > 0 || shieldAbsorbed > 0) && (!damage.IsPeriodic || rules.HitInvulnerabilityAffectsPeriodic))
                hitInvulnerability = Mathf.Max(hitInvulnerability, rules.InvulnerabilityAfterHit);
            ReconcileDefenseScheduler();
            var appliedStatuses = 0;
            if (!killed && !damage.IsPeriodic && (applied > 0 || damage.StatusPolicy == StatusApplyPolicy.OnContact
                || damage.StatusPolicy == StatusApplyPolicy.AfterDamageOrShield && shieldAbsorbed > 0)
                && damage.OnHitStatuses != null && damage.OnHitStatuses.Length > 0)
            {
                if (statuses == null) statuses = GetComponent<StatusController>();
                if (statuses != null)
                {
                    var hit = new DamageResult(damage, applied, killed, breakdown: breakdown, effectiveAmount: effectiveAmount,
                        healthDamage: healthDamage, shieldAbsorbed: shieldAbsorbed, armorPrevented: armorPrevented, resistanceDelta: resistanceDelta, defenses: defenses);
                    appliedStatuses = statuses.ApplyOnHit(damage.OnHitStatuses, in hit);
                }
            }
            var rejection = damage.Amount > 0 && healthDamage == 0 && shieldAbsorbed == 0 ? armorPrevented > 0 ? DamageRejection.Blocked : DamageRejection.Immune
                : damage.Amount == 0 && appliedStatuses == 0 ? DamageRejection.NoEffect : DamageRejection.None;
            var result = new DamageResult(damage, applied, killed, rejection, breakdown, effectiveAmount, appliedStatuses,
                healthDamage, shieldAbsorbed, armorPrevented, resistanceDelta, defenses);
            CombatEvents.Publish(identity, result);
            if (shieldAbsorbed > 0) defenseChangedSignal.Publish(this);
            if (applied > 0)
            {
                changedSignal.Publish(new HealthChange(previous, currentHealth, maximumHealth, HealthChangeReason.Damage));
                damagedSignal.Publish(result);
                if (killed) diedSignal.Publish(this);
            }
            return result;
        }

        private DamageResult Reject(DamageInfo damage, DamageRejection reason)
        {
            var result = DamageResult.Rejected(damage, reason);
            CombatEvents.Publish(identity, result);
            return result;
        }
        public bool IsImmuneToStatus(StatusDefinition status)
        {
            var immunities = definition != null ? definition.StatusImmunities : null;
            return status != null && immunities != null && Array.IndexOf(immunities, status) >= 0;
        }

        /// <summary>Healing never revives. Use ResetToFull or RestoreState explicitly.</summary>
        public float Heal(float amount)
            => ApplyHealing(new HealingInfo(amount)).AppliedAmount;

        public HealingResult ApplyHealing(HealingInfo healing)
        {
            EnsureInitialized();
            if (applyingHealing) return new HealingResult(healing,rejection:HealingRejection.Reentrant);
            if (!healing.IsValid) return new HealingResult(healing,rejection:HealingRejection.Invalid);
            if (!isActiveAndEnabled) return new HealingResult(healing,rejection:HealingRejection.Disabled);
            if (!IsAlive) return new HealingResult(healing,rejection:HealingRejection.Dead);
            if (definition != null && definition.PreventHealing) return new HealingResult(healing,rejection:HealingRejection.Restricted);
            double amount = healing.Amount;
            if (definition != null && definition.ReceivedHealingMultiplierStat != null)
            {
                var stats = GetComponent<CombatStats>();
                if (stats == null || !stats.TryGetValue(definition.ReceivedHealingMultiplierStat,out var multiplier))
                    return new HealingResult(healing,rejection:HealingRejection.Invalid);
                amount *= multiplier;
            }
            if (healing.Maximum > 0) amount = Math.Min(amount,healing.Maximum);
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount > float.MaxValue) return new HealingResult(healing,rejection:HealingRejection.Invalid);
            applyingHealing = true;
            try
            {
                var previous = currentHealth;
                currentHealth += Mathf.Min((float)amount, maximumHealth-currentHealth);
                var applied = currentHealth-previous;
                var excess = Mathf.Max(0,(float)amount-applied);
                // Commit both HP and shield before any notification.
                var shield = healing.Overheal == OverhealPolicy.RestoreShield && defense != null
                    ? defense.RestoreShield(healing.ShieldIndex,excess) : 0;
                if (shield > 0) ReconcileDefenseScheduler();
                var result = new HealingResult(healing,applied,shield,excess-shield);
                if (applied > 0) changedSignal.Publish(new HealthChange(previous,currentHealth,maximumHealth,HealthChangeReason.Healing));
                if (shield > 0) defenseChangedSignal.Publish(this);
                healedSignal.Publish(result);
                return result;
            }
            finally { applyingHealing = false; }
        }

        public void SetMaximum(float maximum, bool preserveRatio = true)
        {
            if (!CombatMath.Positive(maximum)) throw new ArgumentOutOfRangeException(nameof(maximum));
            EnsureInitialized();
            var previous = currentHealth;
            currentHealth = preserveRatio ? (currentHealth / maximumHealth) * maximum : Mathf.Min(currentHealth, maximum);
            maximumHealth = maximum;
            changedSignal.Publish(new HealthChange(previous, currentHealth, maximumHealth, HealthChangeReason.MaximumChanged));
        }

        /// <summary>Explicit spawn/pool reset. OnEnable deliberately preserves health.</summary>
        public void ResetToFull()
        {
            EnsureInitialized();
            var previous = currentHealth;
            currentHealth = maximumHealth;
            identity = new CombatActorIdentity(CombatEvents.NextId(), identity.Name);
            defense?.Reset(); manualInvulnerability = hitInvulnerability = 0; invulnerable = false;
            ReconcileDefenseScheduler(); defenseChangedSignal.Publish(this);
            changedSignal.Publish(new HealthChange(previous, currentHealth, maximumHealth, HealthChangeReason.Reset));
            if (previous == 0f) revivedSignal.Publish(this);
        }

        public HealthSnapshot CaptureState() => new HealthSnapshot(Maximum, Current);

        /// <summary>Invalid snapshots leave the existing state untouched. Restore emits no damage/death rewards.</summary>
        public bool RestoreState(HealthSnapshot snapshot)
        {
            if (snapshot.Version != 1 || !CombatMath.Positive(snapshot.Maximum) || !CombatMath.Finite(snapshot.Current))
                return false;
            EnsureInitialized();
            var previous = currentHealth;
            maximumHealth = snapshot.Maximum;
            currentHealth = Mathf.Clamp(snapshot.Current, 0f, maximumHealth);
            ReconcileDefenseScheduler();
            changedSignal.Publish(new HealthChange(previous, currentHealth, maximumHealth, HealthChangeReason.Restore));
            if (previous == 0f && currentHealth > 0f) revivedSignal.Publish(this);
            return true;
        }

        public void GrantInvulnerability(float seconds)
        {
            if (!CombatMath.Finite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            manualInvulnerability = Mathf.Max(manualInvulnerability, seconds); ReconcileDefenseScheduler();
        }
        public void ClearInvulnerability() { manualInvulnerability = hitInvulnerability = 0; invulnerable = false; ReconcileDefenseScheduler(); }
        public bool SetDefenseLayerEnabled(int index, bool enabled)
        {
            EnsureInitialized(); if (defense == null || !defense.SetEnabled(index, enabled)) return false;
            ReconcileDefenseScheduler(); defenseChangedSignal.Publish(this); return true;
        }
        public float RestoreShield(int index, float amount)
        {
            EnsureInitialized(); var restored = defense != null ? defense.RestoreShield(index, amount) : 0;
            if (restored > 0) { ReconcileDefenseScheduler(); defenseChangedSignal.Publish(this); } return restored;
        }
        internal bool NeedsDefenseTick => isActiveAndEnabled && IsAlive
            && (manualInvulnerability > 0 || hitInvulnerability > 0 || defense != null && defense.NeedsAdvance);
        private void ReconcileDefenseScheduler()
        { if (NeedsDefenseTick) DefenseScheduler.Register(this); else DefenseScheduler.Unregister(this); }
        public void AdvanceDefense(float deltaTime)
        {
            if (!CombatMath.Finite(deltaTime) || deltaTime < 0 || !isActiveAndEnabled || !IsAlive) return;
            manualInvulnerability = Mathf.Max(0, manualInvulnerability - deltaTime);
            hitInvulnerability = Mathf.Max(0, hitInvulnerability - deltaTime);
            var changed = defense != null && defense.Advance(deltaTime);
            ReconcileDefenseScheduler(); if (changed) defenseChangedSignal.Publish(this);
        }
    }
}
