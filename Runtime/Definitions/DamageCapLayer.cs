using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Defense Layers/Damage Cap",fileName="DamageCap")]
    public sealed class DamageCapLayer : DefenseLayerDefinition
    {
        [Min(0)] public float MaximumDamage=20;
        [Range(0,1)] public float MaximumHealthFraction;
        public override bool IsValid=>base.IsValid && CombatMath.Finite(MaximumDamage) && MaximumDamage>=0
            && CombatMath.Finite(MaximumHealthFraction) && MaximumHealthFraction>=0 && MaximumHealthFraction<=1
            && (MaximumDamage>0 || MaximumHealthFraction>0);
        public override DamageAmounts Reduce(in DefenseContext context,DamageAmounts incoming,ref DefenseLayerState state)
        {
            var total=MatchingTotal(incoming); if(total<=0) return incoming;
            var limit=MaximumDamage>0 ? MaximumDamage : float.PositiveInfinity;
            if(MaximumHealthFraction>0) limit=Mathf.Min(limit,context.Target.Maximum*MaximumHealthFraction);
            return total>limit ? ScaleMatching(incoming,limit/total) : incoming;
        }
    }
}
