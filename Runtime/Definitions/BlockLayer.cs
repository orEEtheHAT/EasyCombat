using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Defense Layers/Block", fileName = "Block")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class BlockLayer : DefenseLayerDefinition
    {
        [Range(0, 1)] public float Chance = 1;
        [Range(0, 1)] public float Reduction = 1;
        [Range(0, 360)] public float Arc = 120;
        public ResourceCost[] Costs=System.Array.Empty<ResourceCost>();
        public override bool IsValid => base.IsValid && CombatMath.Finite(Chance) && Chance >= 0 && Chance <= 1
            && CombatMath.Finite(Reduction) && Reduction >= 0 && Reduction <= 1 && CombatMath.Finite(Arc) && Arc >= 0 && Arc <= 360 && ValidCosts;
        private bool ValidCosts
        {
            get
            {
                if(Costs==null || Costs.Length>8) return false;
                foreach(var cost in Costs) if(!cost.IsValid) return false;
                return true;
            }
        }
        public override DamageAmounts Reduce(in DefenseContext context, DamageAmounts incoming, ref DefenseLayerState state)
        {
            if (MatchingTotal(incoming) <= 0 || Chance <= 0) return incoming;
            if (Arc < 360)
            {
                var direction = context.Damage.Direction;
                if (!CombatMath.Finite(direction) || direction.sqrMagnitude < .000001f
                    || Vector3.Dot(context.Target.transform.forward, -direction.normalized) < Mathf.Cos(Arc * .5f * Mathf.Deg2Rad)) return incoming;
            }
            return Chance >= 1 || CombatRandom.Value < Chance ? ScaleMatching(incoming, 1 - Reduction) : incoming;
        }
    }
}
