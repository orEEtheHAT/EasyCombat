using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Status Effects/Periodic Damage", fileName = "PeriodicDamage")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class PeriodicDamageEffect : StatusTickEffect
    {
        public DamageTypeDefinition DamageType;
        [Min(0f)] public float DamagePerStack = 5f;
        [Tooltip("Empty = legacy fixed DamagePerStack. Nonempty = sum of these terms, replacing the legacy value.")]
        public StatusDamageTerm[] Terms = Array.Empty<StatusDamageTerm>();
        public DamageFormulaDefinition Formula;
        [Range(0, 1)] public float CriticalChance;
        [Min(1)] public float CriticalMultiplier = 2;
        public StatusDamageUnit Unit;
        [Min(0)] public float MinimumAmount;
        [Min(0), Tooltip("Zero disables the upper cap. Limits apply per stack in the selected unit, before resistance.")]
        public float MaximumAmount;
        public override bool IsValid
        {
            get
            {
                if (Formula != null) return Formula.IsValid && Unit >= StatusDamageUnit.PerTick && Unit <= StatusDamageUnit.OverDuration
                    && CombatMath.Finite(CriticalChance) && CriticalChance >= 0 && CriticalChance <= 1
                    && CombatMath.Finite(CriticalMultiplier) && CriticalMultiplier >= 1;
                if (Unit < StatusDamageUnit.PerTick || Unit > StatusDamageUnit.OverDuration
                    || !CombatMath.Finite(MinimumAmount) || MinimumAmount < 0 || !CombatMath.Finite(MaximumAmount)
                    || MaximumAmount < 0 || (MaximumAmount > 0 && MaximumAmount < MinimumAmount)) return false;
                if (Terms == null || Terms.Length == 0) return CombatMath.Finite(DamagePerStack) && DamagePerStack >= 0;
                foreach (var term in Terms) if (!term.IsValid) return false;
                return true;
            }
        }
        public static double PlannedTicks(float duration, float interval) => interval > 0
            ? Math.Floor(((double)duration + .0000001d) / interval) : 0;
        public override bool TryCapture(in StatusCaptureContext context, out float value)
        {
            value = 0;
            if (Formula != null)
            {
                if (!TryCaptureState(in context, out _, out var formula)
                    || !formula.TryResolve(context.Target.Current, context.Target.Maximum, out var parts)) return false;
                value = parts.Total;
                return true;
            }
            if (!IsValid || context.Target == null || context.Definition == null
                || (Unit == StatusDamageUnit.OverDuration && PlannedTicks(context.Definition.Duration, context.Definition.TickInterval) < 1)) return false;
            if (Terms == null || Terms.Length == 0) { value = DamagePerStack; return true; }
            foreach (var term in Terms)
            {
                // Validate live inputs on application too; a missing source stat must not silently create a weak stack.
                if (!term.TryEvaluate(in context, out var amount)) return false;
                if (!term.IsLive || Unit == StatusDamageUnit.OverDuration) value += amount;
            }
            return CombatMath.Finite(value);
        }

        public override bool TryCaptureState(in StatusCaptureContext context, out float value, out DamageFormulaSnapshot formula)
        {
            value = 0; formula = default;
            if (Formula == null) return TryCapture(in context, out value);
            if (!IsValid || context.Target == null || context.Definition == null
                || Unit == StatusDamageUnit.OverDuration && PlannedTicks(context.Definition.Duration, context.Definition.TickInterval) < 1) return false;
            var critical = CriticalChance >= 1 || CriticalChance > 0 && CombatRandom.Value < CriticalChance;
            return Formula.TryPrepare(context.Source, context.Target, context.Hit, context.HasHit, Unit == StatusDamageUnit.OverDuration,
                critical, CriticalMultiplier, 1, out formula);
        }

        private DamageAmounts SharedParts(in StatusTickContext context, out bool critical)
        {
            critical = false;
            if (context.Target == null) return default;
            var formula = context.Formula;
            if (formula.Count == 0)
            {
                var capture = new StatusCaptureContext(context.Target, context.Source, context.Definition);
                if (!TryCaptureState(in capture, out _, out formula)) return default;
            }
            if (!formula.TryResolve(context.Target.Current, context.Target.Maximum, out var parts)) return default;
            critical = formula.IsCritical;
            var scale = Unit == StatusDamageUnit.PerSecond ? context.Interval : Unit == StatusDamageUnit.OverDuration
                ? context.PlannedTickCount >= 1 ? (float)(1 / context.PlannedTickCount) : 0 : 1;
            return parts.Scale(scale);
        }

        public float CalculatePerStack(in StatusTickContext context)
        {
            if (!IsValid || context.Target == null) return 0;
            if (Formula != null || context.Formula.Count > 0) return SharedParts(in context, out _).Total;
            var capture = new StatusCaptureContext(context.Target, context.Source, context.Definition);
            var amount = context.CapturedValue;
            if (!context.HasCapturedValue && !TryCapture(in capture, out amount)) return 0;
            if (Unit != StatusDamageUnit.OverDuration && Terms != null)
                foreach (var term in Terms)
                    if (term.IsLive && term.TryEvaluate(in capture, out var live)) amount += live;
            if (!CombatMath.Finite(amount)) return 0;
            amount = Mathf.Max(amount, MinimumAmount);
            if (MaximumAmount > 0) amount = Mathf.Min(amount, MaximumAmount);
            if (Unit == StatusDamageUnit.PerSecond) amount *= context.Interval;
            else if (Unit == StatusDamageUnit.OverDuration)
            {
                var ticks = context.PlannedTickCount;
                amount = ticks >= 1 ? (float)(amount / ticks) : 0;
            }
            return CombatMath.Finite(amount) ? amount : 0;
        }
        public override void Execute(in StatusTickContext context)
        {
            if (Formula != null || context.Formula.Count > 0)
            {
                var parts = SharedParts(in context, out var critical).Scale(context.Stacks);
                if (!parts.IsValid || !CombatMath.Positive(parts.Total)) return;
                context.Target.ApplyDamage(new DamageInfo(parts, context.Source, context.Target.transform.position, default,
                    critical, true, origin: context.Origin));
                return;
            }
            var amount = CalculatePerStack(in context) * context.Stacks;
            if (!CombatMath.Positive(amount)) return;
            // Periodic damage is typed but cannot crit or apply on-hit statuses recursively.
            context.Target.ApplyDamage(new DamageInfo(amount, context.Source,
                context.Target.transform.position, default, DamageType, false, true, origin: context.Origin));
        }
    }
}
