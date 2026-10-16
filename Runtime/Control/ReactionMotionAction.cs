using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Reactions/Actions/Knockback or Pull",fileName="MotionReaction")]
    public sealed class ReactionMotionAction : CombatReactionAction
    {
        public bool PullTowardOwner;
        [Min(0)] public float Speed=5;
        public float UpwardSpeed;
        public override bool IsValid => CombatMath.Finite(Speed) && Speed>=0 && CombatMath.Finite(UpwardSpeed);
        public override void Execute(in ReactionContext context)
        {
            if (!IsValid || context.Owner==null || context.Target==null) return;
            var direction=context.Target.transform.position-context.Owner.transform.position;
            if (PullTowardOwner) direction=-direction;
            var intent=new CombatMotionIntent(direction.normalized*Speed+Vector3.up*UpwardSpeed,context.Owner.gameObject);
            context.Target.GetComponent<ICombatMotionReceiver>()?.ApplyMotion(in intent);
        }
    }
}
