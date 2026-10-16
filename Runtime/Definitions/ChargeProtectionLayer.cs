using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Defense Layers/Protection Charges",fileName="ProtectionCharges")]
    public sealed class ChargeProtectionLayer : DefenseLayerDefinition
    {
        [Range(1,64)] public int Charges=3;
        [Range(0,1)] public float Reduction=1;
        [Min(0),Tooltip("Zero disables regeneration. Otherwise one charge is restored per interval.")]
        public float RechargeInterval;
        public override bool IsValid=>base.IsValid && Charges>=1 && Charges<=64 && CombatMath.Finite(Reduction) && Reduction>=0 && Reduction<=1
            && CombatMath.Finite(RechargeInterval) && RechargeInterval>=0;
        public override DefenseLayerState CreateState()=>new DefenseLayerState { Current=Charges,Maximum=Charges,Enabled=true };
        public override bool NeedsAdvance(in DefenseLayerState state)=>state.Enabled && RechargeInterval>0 && state.Current<state.Maximum;
        public override void Advance(ref DefenseLayerState state,float deltaTime)
        {
            if(!NeedsAdvance(in state)) return;
            var remaining=(double)state.RechargeDelay-deltaTime;
            if(remaining>0) { state.RechargeDelay=(float)remaining; return; }
            var gained=1+System.Math.Floor(-remaining/RechargeInterval);
            state.Current=(float)System.Math.Min(state.Maximum,state.Current+gained);
            state.RechargeDelay=state.Current<state.Maximum ? (float)(remaining+gained*RechargeInterval) : 0;
        }
        public override DamageAmounts Reduce(in DefenseContext context,DamageAmounts incoming,ref DefenseLayerState state)
        {
            if(state.Current<1 || Reduction<=0 || MatchingTotal(incoming)<=0) return incoming;
            state.Current-=1;
            if(RechargeInterval>0 && state.RechargeDelay<=0) state.RechargeDelay=RechargeInterval;
            return ScaleMatching(incoming,1-Reduction);
        }
    }
}
