using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Reactions/Actions/Heal or Lifesteal", fileName = "HealingReaction")]
    public sealed class ReactionHealingAction : CombatReactionAction
    {
        public HealingDefinition Healing;
        public override bool IsValid => Healing != null && Healing.IsValid;
        public override void Execute(in ReactionContext context)
        {
            if (IsValid && context.Owner != null && context.Target != null && Healing.TryPrepare(context.Owner.gameObject,context.Target,out var healing,context.Trigger.Result))
                context.Target.ApplyHealing(healing);
        }
    }
}
