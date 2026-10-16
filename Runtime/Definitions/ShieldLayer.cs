using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Defense Layers/Shield", fileName = "Shield")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class ShieldLayer : DefenseLayerDefinition
    {
        [Min(.001f)] public float Capacity = 50;
        [Range(0, 1)] public float InitialFraction = 1;
        [Range(0, 1)] public float Absorption = 1;
        [Min(0)] public float RechargePerSecond;
        [Min(0)] public float DelayAfterAbsorption = 3;
        public override bool IsShield => true;
        public override bool IsValid => base.IsValid && CombatMath.Positive(Capacity) && CombatMath.Finite(InitialFraction)
            && InitialFraction >= 0 && InitialFraction <= 1 && CombatMath.Finite(Absorption) && Absorption >= 0 && Absorption <= 1
            && CombatMath.Finite(RechargePerSecond) && RechargePerSecond >= 0 && CombatMath.Finite(DelayAfterAbsorption) && DelayAfterAbsorption >= 0;
        public override DefenseLayerState CreateState() => new DefenseLayerState { Maximum = Capacity, Current = Capacity * InitialFraction, Enabled = true };
        public override bool NeedsAdvance(in DefenseLayerState state) => state.Enabled && RechargePerSecond > 0 && state.Current < state.Maximum;
        public override void Advance(ref DefenseLayerState state, float deltaTime)
        {
            if (!NeedsAdvance(state)) return;
            var rechargeTime = Mathf.Max(0, deltaTime - state.RechargeDelay);
            state.RechargeDelay = Mathf.Max(0, state.RechargeDelay - deltaTime);
            state.Current = (float)System.Math.Min(state.Maximum, state.Current + (double)RechargePerSecond * rechargeTime);
        }
        public override DamageAmounts Reduce(in DefenseContext context, DamageAmounts incoming, ref DefenseLayerState state)
        {
            var total = MatchingTotal(incoming);
            if (total <= 0 || state.Current <= 0 || Absorption <= 0) return incoming;
            var absorbed = Mathf.Min(state.Current, total * Absorption);
            var result = ScaleMatching(incoming, Mathf.Max(0, 1 - absorbed / total));
            state.Current = Mathf.Max(0, state.Current - absorbed);
            state.RechargeDelay = DelayAfterAbsorption;
            return result;
        }
    }
}
