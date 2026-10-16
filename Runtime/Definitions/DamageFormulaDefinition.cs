using System;
using UnityEngine;

namespace EasyCombat
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DamageFormulaTerm
    {
        public DamageTypeDefinition Type;
        public StatusDamageBasis Basis;
        [Min(0)] public float Minimum, Maximum;
        [Min(0), Tooltip("Hard limit for this part before resistance, after critical and hit multipliers. Zero = unlimited.")]
        public float MaximumDamage;
        public bool IgnoreCritical;
        public CombatStatDefinition Stat;
        public StatusValueSampling Sampling;
        public HitDamageStage HitStage;
        public bool FilterHitType;
        public DamageTypeDefinition HitType;
        public DamageFormulaTerm(DamageTypeDefinition type, StatusDamageBasis basis, float value)
        {
            Type = type; Basis = basis; Minimum = Maximum = value; MaximumDamage = 0; IgnoreCritical = false;
            Stat = null; Sampling = StatusValueSampling.OnApplication; HitStage = HitDamageStage.HealthLost;
            FilterHitType = false; HitType = null;
        }
        internal StatusDamageTerm AsValue(float value) => new StatusDamageTerm(Basis, value, Sampling)
        { Stat = Stat, HitStage = HitStage, FilterHitType = FilterHitType, HitType = HitType };
        public bool IsValid => AsValue(Minimum).IsValid && CombatMath.Finite(Maximum) && Maximum >= Minimum
            && CombatMath.Finite(MaximumDamage) && MaximumDamage >= 0;
    }

    [CreateAssetMenu(menuName = "EasyCombat/Damage Formula", fileName = "DamageFormula")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageFormulaDefinition : ScriptableObject
    {
        public DamageFormulaTerm[] Terms = Array.Empty<DamageFormulaTerm>();
        public bool IsValid
        {
            get
            {
                if (Terms == null || Terms.Length == 0 || Terms.Length > DamageAmounts.Capacity) return false;
                foreach (var term in Terms) if (!term.IsValid) return false;
                return true;
            }
        }
        public bool IsValidForAttack
        {
            get
            {
                if (!IsValid) return false;
                foreach (var term in Terms) if (term.Basis == StatusDamageBasis.TriggeringHit) return false;
                return true;
            }
        }
        public bool TryPrepare(GameObject source, Health target, in DamageResult hit, bool hasHit, bool captureAll,
            bool critical, float criticalMultiplier, float outgoingMultiplier, out DamageFormulaSnapshot snapshot,
            ReadOnlySpan<float> samples = default, float sample = 0, bool random = true)
        {
            snapshot = new DamageFormulaSnapshot(source, critical);
            if (!IsValid || !CombatMath.Finite(outgoingMultiplier) || outgoingMultiplier < 0
                || !CombatMath.Finite(criticalMultiplier) || criticalMultiplier < 1) return false;
            foreach (var term in Terms)
            {
                var index = snapshot.Count;
                var roll = random ? CombatRandom.Value : samples.Length > index ? samples[index] : sample;
                var value = Mathf.Lerp(term.Minimum, term.Maximum, Mathf.Clamp01(roll));
                var basis = term.Basis;
                var shouldCapture = basis == StatusDamageBasis.Fixed || basis == StatusDamageBasis.TriggeringHit
                    || (basis == StatusDamageBasis.SourceStat && (captureAll || term.Sampling == StatusValueSampling.OnApplication))
                    || (target != null && (captureAll || basis == StatusDamageBasis.TargetMaximumHealth && term.Sampling == StatusValueSampling.OnApplication));
                if (shouldCapture || basis == StatusDamageBasis.SourceStat)
                {
                    if (!DamageFormulaMath.TryValue(term.AsValue(value), target != null ? target.Current : 0,
                        target != null ? target.Maximum : 0, target != null, source, hit, hasHit, out var captured)) return false;
                    if (shouldCapture) { value = captured; basis = StatusDamageBasis.Fixed; }
                }
                var multiplier = outgoingMultiplier * (critical && !term.IgnoreCritical ? criticalMultiplier : 1);
                snapshot.Add(new PreparedDamageTerm(term.Type, basis, value, term.Stat, multiplier, term.MaximumDamage));
            }
            return snapshot.IsValid;
        }
    }
}
