using UnityEngine;

namespace EasyCombat
{
    public readonly struct AbilityContext
    {
        public readonly CombatAbilities Controller;
        public readonly AbilityDefinition Definition;
        public readonly Health Owner,Target;
        public readonly long TargetLife;
        public readonly Vector3 Origin,Point,Direction;
        public readonly DamageOrigin SourceOrigin;
        public AbilityContext(CombatAbilities controller,AbilityDefinition definition,Health owner,Health target,Vector3 origin,Vector3 point,Vector3 direction,DamageOrigin source)
        { Controller=controller; Definition=definition; Owner=owner; Target=target; TargetLife=target!=null ? target.Identity.Id : 0; Origin=origin; Point=point; Direction=direction; SourceOrigin=source; }
        public DamageOrigin OriginFor(AttackDefinition attack=null)
        {
            var action=SourceOrigin.Action.Child(attack!=null ? CombatOriginKind.Attack : CombatOriginKind.Ability);
            if(attack!=null) action=new CombatActionOrigin(action.Id,action.RootId,action.ParentId,action.Depth,action.Kind,new CombatTagSet(attack.Tags),action.ReactionDepth);
            return new DamageOrigin(SourceOrigin.Actor,attack,SourceOrigin.CastId,hasTeam:SourceOrigin.HasTeam,team:SourceOrigin.Team,action:action);
        }
    }
    /// <summary>Shared definitions contain no actor state. Preflight must not mutate gameplay or consume randomness.</summary>
    public abstract class CombatAbilityAction:ScriptableObject
    {
        public abstract bool IsValid { get; }
        public abstract bool CanExecute(in AbilityContext context);
        public abstract bool Execute(in AbilityContext context);
    }
}
