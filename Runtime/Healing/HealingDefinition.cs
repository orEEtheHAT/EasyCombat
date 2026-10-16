using System;
using UnityEngine;

namespace EasyCombat
{
    public enum HealingBasis { Fixed, SourceStat, TargetMaximumHealth, TargetCurrentHealth, TargetMissingHealth, TriggeringHealthDamage, TriggeringHitIncludingShield }
    public enum OverhealPolicy { Discard, RestoreShield }
    [Serializable]
    public struct HealingTerm
    {
        public HealingBasis Basis;
        [Min(0)] public float Minimum, Maximum;
        public CombatStatDefinition SourceStat;
        public bool IsValid => Basis >= HealingBasis.Fixed && Basis <= HealingBasis.TriggeringHitIncludingShield
            && CombatMath.Finite(Minimum) && CombatMath.Finite(Maximum) && Minimum >= 0 && Maximum >= Minimum
            && (Basis != HealingBasis.SourceStat || SourceStat != null);
        public HealingTerm(HealingBasis basis, float value) { Basis = basis; Minimum = Maximum = value; SourceStat = null; }
    }
    [CreateAssetMenu(menuName = "EasyCombat/Healing/Healing", fileName = "Healing")]
    public sealed class HealingDefinition : ScriptableObject
    {
        public HealingTerm[] Terms = { new HealingTerm(HealingBasis.Fixed,25) };
        [Range(0,1)] public float CriticalChance;
        [Min(1)] public float CriticalMultiplier = 2;
        public CombatStatDefinition OutgoingMultiplierStat;
        [Min(0), Tooltip("Maximum after critical and outgoing/incoming multipliers. Zero = uncapped.")]
        public float MaximumHealing;
        public OverhealPolicy Overheal;
        [Min(0)] public int OverhealShieldIndex;
        public bool IsValid
        {
            get
            {
                if (Terms == null || Terms.Length == 0 || Terms.Length > 8 || !CombatMath.Finite(CriticalChance) || CriticalChance < 0 || CriticalChance > 1
                    || !CombatMath.Finite(CriticalMultiplier) || CriticalMultiplier < 1 || !CombatMath.Finite(MaximumHealing) || MaximumHealing < 0
                    || Overheal < OverhealPolicy.Discard || Overheal > OverhealPolicy.RestoreShield || OverhealShieldIndex < 0) return false;
                foreach (var term in Terms) if (!term.IsValid) return false;
                return true;
            }
        }
        public bool TryPrepare(GameObject source, Health target, out HealingInfo info, DamageResult trigger = default)
            => TryPrepare(source,target,CombatRandom.Value,CombatRandom.Value,out info,trigger);
        public bool TryPrepare(GameObject source, Health target, float amountSample, float criticalSample, out HealingInfo info, DamageResult trigger = default)
        {
            info = default;
            if (!IsValid || target == null || !CombatMath.Finite(amountSample) || !CombatMath.Finite(criticalSample)) return false;
            var sourceStats = source != null ? source.GetComponent<CombatStats>() : null;
            double total = 0;
            foreach (var term in Terms)
            {
                float basis;
                switch (term.Basis)
                {
                    case HealingBasis.Fixed: basis = 1; break;
                    case HealingBasis.SourceStat: if (sourceStats == null || !sourceStats.TryGetValue(term.SourceStat,out basis)) return false; break;
                    case HealingBasis.TargetMaximumHealth: basis = target.Maximum; break;
                    case HealingBasis.TargetCurrentHealth: basis = target.Current; break;
                    case HealingBasis.TargetMissingHealth: basis = target.Maximum-target.Current; break;
                    case HealingBasis.TriggeringHealthDamage: basis = trigger.AppliedAmount; break;
                    case HealingBasis.TriggeringHitIncludingShield: basis = trigger.AppliedAmount+trigger.ShieldAbsorbed; break;
                    default: return false;
                }
                total += basis * (double)Mathf.Lerp(term.Minimum,term.Maximum,Mathf.Clamp01(amountSample));
            }
            if (OutgoingMultiplierStat != null)
            {
                if (sourceStats == null || !sourceStats.TryGetValue(OutgoingMultiplierStat,out var multiplier)) return false;
                total *= multiplier;
            }
            var critical = CriticalChance >= 1 || CriticalChance > 0 && criticalSample < CriticalChance;
            if (critical) total *= CriticalMultiplier;
            if (double.IsInfinity(total) || double.IsNaN(total) || total > float.MaxValue) return false;
            info = new HealingInfo((float)total, source,critical,MaximumHealing,Overheal,OverhealShieldIndex); return true;
        }
    }
    public enum HealingRejection { None, Invalid, Disabled, Dead, Restricted, Reentrant, Downed }
    public readonly struct HealingInfo
    {
        public readonly float Amount, Maximum;
        public readonly GameObject Source;
        public readonly bool IsCritical;
        public readonly OverhealPolicy Overheal;
        public readonly int ShieldIndex;
        public HealingInfo(float amount, GameObject source = null, bool critical = false, float maximum = 0, OverhealPolicy overheal = OverhealPolicy.Discard, int shieldIndex = 0)
        { Amount = amount; Source = source; IsCritical = critical; Maximum = maximum; Overheal = overheal; ShieldIndex = shieldIndex; }
        public bool IsValid => CombatMath.Finite(Amount) && Amount >= 0 && CombatMath.Finite(Maximum) && Maximum >= 0
            && Overheal >= OverhealPolicy.Discard && Overheal <= OverhealPolicy.RestoreShield && ShieldIndex >= 0;
    }
    public readonly struct HealingResult
    {
        public readonly HealingInfo Healing;
        public readonly float AppliedAmount, ShieldRestored, Discarded;
        public readonly HealingRejection Rejection;
        public bool Applied => AppliedAmount > 0 || ShieldRestored > 0;
        public HealingResult(HealingInfo healing, float applied = 0, float shield = 0, float discarded = 0, HealingRejection rejection = HealingRejection.None)
        { Healing = healing; AppliedAmount = applied; ShieldRestored = shield; Discarded = discarded; Rejection = rejection; }
    }
}
