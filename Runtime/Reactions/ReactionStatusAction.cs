using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Reactions/Actions/Apply Status",fileName="ApplyStatusReaction")]
    public sealed class ReactionStatusAction : CombatReactionAction
    {
        public StatusDefinition Status;
        public override bool IsValid=>Status!=null && Status.IsValid;
        public override void Execute(in ReactionContext context)
        {
            if(!IsValid || context.Owner==null || context.Target==null) return;
            context.Target.GetComponent<StatusController>()?.ApplyFromHit(Status,in context.Trigger.Result,context.Source,context.Origin());
        }
    }
}
