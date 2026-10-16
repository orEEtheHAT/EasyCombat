using System;
using UnityEngine;

namespace EasyCombat
{
    internal readonly struct PreparedDamageTerm
    {
        public readonly DamageTypeDefinition Type;
        public readonly StatusDamageBasis Basis;
        public readonly float Value, Multiplier, Limit;
        public readonly CombatStatDefinition Stat;
        public PreparedDamageTerm(DamageTypeDefinition type, StatusDamageBasis basis, float value, CombatStatDefinition stat, float multiplier, float limit)
        { Type = type; Basis = basis; Value = value; Stat = stat; Multiplier = multiplier; Limit = limit; }
    }

    /// <summary>Immutable value recipe carried to a receiver. Target HP is read once, just before that hit changes it.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DamageFormulaSnapshot
    {
        private PreparedDamageTerm a, b, c, d, e, f, g, h;
        private GameObject source;
        private float scale;
        public int Count { get; private set; }
        public bool IsCritical { get; private set; }
        internal DamageFormulaSnapshot(GameObject source, bool critical) : this() { this.source = source; IsCritical = critical; scale = 1; }
        private PreparedDamageTerm Get(int index) => index switch { 0 => a, 1 => b, 2 => c, 3 => d, 4 => e, 5 => f, 6 => g, 7 => h, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
        internal void Add(PreparedDamageTerm term)
        {
            switch (Count)
            { case 0: a = term; break; case 1: b = term; break; case 2: c = term; break; case 3: d = term; break;
              case 4: e = term; break; case 5: f = term; break; case 6: g = term; break; case 7: h = term; break;
              default: throw new InvalidOperationException("At most eight formula terms."); }
            Count++;
        }
        public bool IsValid
        {
            get
            {
                if (Count < 1 || Count > 8 || !CombatMath.Finite(scale) || scale < 0) return false;
                for (var i = 0; i < Count; i++)
                {
                    var term = Get(i);
                    if (!CombatMath.Finite(term.Value) || term.Value < 0 || !CombatMath.Finite(term.Multiplier) || term.Multiplier < 0) return false;
                }
                return true;
            }
        }
        public DamageFormulaSnapshot Scale(float multiplier) { var copy = this; copy.scale *= multiplier; return copy; }
        public bool TryResolve(float currentHealth, float maximumHealth, out DamageAmounts amounts)
        {
            amounts = default;
            if (!IsValid || !CombatMath.Finite(currentHealth) || !CombatMath.Finite(maximumHealth)
                || currentHealth < 0 || maximumHealth < currentHealth) return false;
            for (var i = 0; i < Count; i++)
            {
                var term = Get(i);
                // A missing live source contributes zero. Captured values are independent of source lifetime.
                var input = new StatusDamageTerm(term.Basis, 1) { Stat = term.Stat };
                if (!DamageFormulaMath.TryValue(input, currentHealth, maximumHealth, true, source, default, false, out var basis)) basis = 0;
                var value = (double)basis * term.Value * term.Multiplier * scale;
                if (term.Limit > 0) value = Math.Min(value, term.Limit);
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > float.MaxValue) return false;
                amounts.Add(term.Type, (float)value);
            }
            return amounts.IsValid;
        }
    }

    internal static class DamageFormulaMath
    {
        internal static bool TryValue(in StatusDamageTerm term, float current, float maximum, bool hasTarget,
            GameObject source, in DamageResult hit, bool hasHit, out float value)
        {
            value = 0;
            if (!term.IsValid) return false;
            if (term.Value == 0) return true;
            float basis;
            switch (term.Basis)
            {
                case StatusDamageBasis.Fixed: basis = 1; break;
                case StatusDamageBasis.TriggeringHit: if (!hasHit) return false; basis = HitAmount(term, hit); break;
                case StatusDamageBasis.TargetCurrentHealth: if (!hasTarget) return false; basis = current; break;
                case StatusDamageBasis.TargetMaximumHealth: if (!hasTarget) return false; basis = maximum; break;
                case StatusDamageBasis.TargetMissingHealth: if (!hasTarget) return false; basis = maximum - current; break;
                case StatusDamageBasis.SourceStat:
                    if (source == null || !source.TryGetComponent<CombatStats>(out var stats) || !stats.TryGetValue(term.Stat, out basis)) return false;
                    break;
                default: return false;
            }
            value = basis * term.Value;
            return CombatMath.Finite(value) && value >= 0;
        }
        private static float HitAmount(in StatusDamageTerm term, in DamageResult hit)
        {
            if (!term.FilterHitType) return term.HitStage == HitDamageStage.Incoming ? hit.Damage.Amount
                : term.HitStage == HitDamageStage.AfterResistance ? hit.EffectiveAmount : hit.AppliedAmount;
            var sum = 0f;
            if (term.HitStage == HitDamageStage.Incoming)
            {
                for (var i = 0; i < hit.Damage.Parts.Count; i++) if (hit.Damage.Parts[i].Type == term.HitType) sum += hit.Damage.Parts[i].Amount;
            }
            else for (var i = 0; i < hit.Breakdown.Count; i++) if (hit.Breakdown[i].Type == term.HitType)
                sum += term.HitStage == HitDamageStage.AfterResistance ? hit.Breakdown[i].Effective : hit.Breakdown[i].Applied;
            return sum;
        }
    }
}
