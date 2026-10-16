using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Defense Layers/Temporary Shield",fileName="TemporaryShield")]
    public sealed class TemporaryShieldLayer : DefenseLayerDefinition
    {
        [Min(.001f)] public float Capacity=50;
        [Min(.001f)] public float Duration=5;
        [Min(0)] public float DecayPerSecond;
        [Range(0,1)] public float Absorption=1;
        public override bool IsShield=>true;
        public override bool IsValid=>base.IsValid && CombatMath.Positive(Capacity) && CombatMath.Positive(Duration)
            && CombatMath.Finite(DecayPerSecond) && DecayPerSecond>=0 && CombatMath.Finite(Absorption) && Absorption>=0 && Absorption<=1;
        public override DefenseLayerState CreateState()=>new DefenseLayerState { Current=Capacity,Maximum=Capacity,RechargeDelay=Duration,Enabled=true };
        public override bool NeedsAdvance(in DefenseLayerState state)=>state.Enabled && state.RechargeDelay>0;
        public override void Advance(ref DefenseLayerState state,float deltaTime)
        {
            if(!state.Enabled) return;
            state.RechargeDelay=Mathf.Max(0,state.RechargeDelay-deltaTime);
            state.Current=(float)System.Math.Max(0,state.Current-(double)DecayPerSecond*deltaTime);
            if(state.RechargeDelay==0) { state.Current=0; state.Enabled=false; }
        }
        public override DamageAmounts Reduce(in DefenseContext context,DamageAmounts incoming,ref DefenseLayerState state)
        {
            var total=MatchingTotal(incoming);
            if(total<=0 || state.Current<=0 || state.RechargeDelay<=0 || Absorption<=0) return incoming;
            var absorbed=Mathf.Min(state.Current,total*Absorption); state.Current-=absorbed;
            return ScaleMatching(incoming,Mathf.Max(0,1-absorbed/total));
        }
    }
}
