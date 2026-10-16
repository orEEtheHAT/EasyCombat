using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Reactions/Actions/Combine Statuses",fileName="CombineStatusesReaction")]
    public sealed class ReactionStatusCombinationAction : CombatReactionAction
    {
        public StatusCombinationDefinition Combination;
        public override bool IsValid => Combination!=null && Combination.IsValid;
        public override void Execute(in ReactionContext context)
        {
            if(!IsValid || context.Target==null || context.Owner==null) return;
            context.Target.GetComponent<StatusController>()?.TryCombine(Combination,context.Source,context.Trigger.Result,true,context.Origin());
        }
    }
}
