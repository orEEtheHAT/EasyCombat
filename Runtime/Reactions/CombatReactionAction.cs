using UnityEngine;

namespace EasyCombat
{
    public readonly struct ReactionContext
    {
        public readonly Health Owner, Target;
        public readonly CombatRecord Trigger;
        public readonly CombatActionOrigin Action;
        public ReactionContext(Health owner, Health target, CombatRecord trigger, CombatActionOrigin action)
        { Owner = owner; Target = target; Trigger = trigger; Action = action; }
        public DamageOrigin Origin(AttackDefinition attack = null)
        {
            var team = Owner != null ? Owner.GetComponentInParent<CombatTeam>() : null;
            return new DamageOrigin(Owner != null ? Owner.Identity : default, attack,Trigger.Result.Damage.Origin.CastId,
                hasTeam:team != null,team:team != null ? team.Team : 0,action:Action);
        }
    }
    public abstract class CombatReactionAction : ScriptableObject
    {
        public abstract bool IsValid { get; }
        public abstract void Execute(in ReactionContext context);
    }
}
