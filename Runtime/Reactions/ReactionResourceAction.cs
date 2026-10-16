using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Reactions/Actions/Restore Resource", fileName = "ResourceReaction")]
    public sealed class ReactionResourceAction : CombatReactionAction
    {
        public ResourceDefinition Resource;
        [Min(0)] public float Amount = 10;
        public override bool IsValid => Resource != null && CombatMath.Positive(Amount);
        public override void Execute(in ReactionContext context)
        { if (IsValid && context.Target != null) context.Target.GetComponent<CombatResources>()?.Restore(Resource,Amount); }
    }
}
