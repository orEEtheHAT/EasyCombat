using UnityEngine;

namespace EasyCombat
{
    [System.Serializable]
    public struct ShieldDamageCost
    {
        public DamageTypeDefinition Type;
        [Min(.001f)] public float CostMultiplier;
        public ShieldDamageCost(DamageTypeDefinition type,float multiplier) { Type=type; CostMultiplier=multiplier; }
    }
    [CreateAssetMenu(menuName = "EasyCombat/Defense Layers/Shield", fileName = "Shield")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class ShieldLayer : DefenseLayerDefinition
    {
        [Min(.001f)] public float Capacity = 50;
        [Range(0, 1)] public float InitialFraction = 1;
        [Range(0, 1)] public float Absorption = 1;
        [Min(0)] public float RechargePerSecond;
        [Min(0)] public float DelayAfterAbsorption = 3;
        [Tooltip("Optional shield-energy cost per absorbed damage point, by type. Empty = 1:1.")]
        public ShieldDamageCost[] DamageCosts=System.Array.Empty<ShieldDamageCost>();
        public override bool IsShield => true;
        public override bool IsValid => base.IsValid && CombatMath.Positive(Capacity) && CombatMath.Finite(InitialFraction)
            && InitialFraction >= 0 && InitialFraction <= 1 && CombatMath.Finite(Absorption) && Absorption >= 0 && Absorption <= 1
            && CombatMath.Finite(RechargePerSecond) && RechargePerSecond >= 0 && CombatMath.Finite(DelayAfterAbsorption) && DelayAfterAbsorption >= 0 && ValidCosts;
        private bool ValidCosts
        {
            get
            {
                if(DamageCosts==null || DamageCosts.Length>8) return false;
                foreach(var cost in DamageCosts) if(!CombatMath.Positive(cost.CostMultiplier)) return false;
                return true;
            }
        }
        private float Cost(DamageTypeDefinition type)
        { for(var i=DamageCosts.Length-1;i>=0;i--) if(DamageCosts[i].Type==type) return DamageCosts[i].CostMultiplier; return 1; }
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
            double requested=0;
            for(var i=0;i<incoming.Count;i++) if(AcceptsType(incoming[i].Type)) requested+=(double)incoming[i].Amount*Cost(incoming[i].Type)*Absorption;
            if(requested<=0) return incoming;
            var spent=System.Math.Min(state.Current,requested);
            var result = ScaleMatching(incoming, Mathf.Max(0, 1 - Absorption*(float)(spent/requested)));
            state.Current = Mathf.Max(0, state.Current - (float)spent);
            state.RechargeDelay = DelayAfterAbsorption;
            return result;
        }
    }
}
