using UnityEngine;

namespace EasyCombat
{
    public readonly struct ReactionContext
    {
        public readonly Health Owner, Target;
        public readonly CombatRecord Trigger;
        public readonly CombatActionOrigin Action;
        public readonly bool UseTriggerSource;
        public ReactionContext(Health owner, Health target, CombatRecord trigger, CombatActionOrigin action,bool useTriggerSource=false)
        { Owner = owner; Target = target; Trigger = trigger; Action = action; UseTriggerSource=useTriggerSource; }
        public GameObject Source
        {
            get
            {
                if(!UseTriggerSource) return Owner!=null ? Owner.gameObject : null;
                return CombatSources.ForLife(Trigger.Result.Damage.Source,Trigger.Result.Damage.Origin.Actor.Id);
            }
        }
        public DamageOrigin Origin(AttackDefinition attack = null)
        {
            if(UseTriggerSource)
            {
                var origin=Trigger.Result.Damage.Origin;
                return new DamageOrigin(origin.Actor,attack,origin.CastId,hasTeam:origin.HasTeam,team:origin.Team,action:Action);
            }
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
