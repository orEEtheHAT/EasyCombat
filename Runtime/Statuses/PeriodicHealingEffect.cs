using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Status Effects/Periodic Healing", fileName = "PeriodicHealing")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class PeriodicHealingEffect : StatusTickEffect
    {
        [Min(0f)] public float HealingPerStack = 5f;
        public override void Execute(in StatusTickContext context)
        {
            if (CombatMath.Positive(HealingPerStack)) context.Target.Heal(HealingPerStack * context.Stacks);
        }
    }
}
