using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Reactions/Actions/Activate Defense Layer",fileName="DefenseLayerReaction")]
    public sealed class ReactionDefenseLayerAction : CombatReactionAction
    {
        [Min(0)] public int LayerIndex;
        public bool ResetState=true;
        public bool Enable=true;
        public override bool IsValid=>LayerIndex>=0;
        public override void Execute(in ReactionContext context)
        {
            if(context.Target==null || !IsValid) return;
            if(ResetState) context.Target.ResetDefenseLayer(LayerIndex,Enable);
            else context.Target.SetDefenseLayerEnabled(LayerIndex,Enable);
        }
    }
}
