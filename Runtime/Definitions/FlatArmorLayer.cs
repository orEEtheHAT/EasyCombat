using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Defense Layers/Flat Armor", fileName = "FlatArmor")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class FlatArmorLayer : DefenseLayerDefinition
    {
        [Min(0)] public float Reduction = 5;
        public override bool IsValid => base.IsValid && CombatMath.Finite(Reduction) && Reduction >= 0;
        public override DamageAmounts Reduce(in DefenseContext context, DamageAmounts incoming, ref DefenseLayerState state)
        {
            var total = MatchingTotal(incoming);
            // One allowance per packet, distributed across eligible channels, so splitting a type cannot multiply armor.
            return total > 0 ? ScaleMatching(incoming, Mathf.Max(0, 1 - Reduction / total)) : incoming;
        }
    }
}
