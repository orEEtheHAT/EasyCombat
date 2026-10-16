using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Defense Layers/Percentage Reduction", fileName = "Reduction")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class ReductionLayer : DefenseLayerDefinition
    {
        [Range(0, 1)] public float Reduction = .2f;
        public override bool IsValid => base.IsValid && CombatMath.Finite(Reduction) && Reduction >= 0 && Reduction <= 1;
        public override DamageAmounts Reduce(in DefenseContext context, DamageAmounts incoming, ref DefenseLayerState state)
            => ScaleMatching(incoming, 1 - Reduction);
    }
}
