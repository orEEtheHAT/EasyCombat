using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Abilities/Actions/Reusable Effect",fileName="AbilityEffect")]
    public sealed class AbilityEffectAction:CombatAbilityAction
    {
        public CombatReactionAction Effect;
        public bool AffectCaster;
        public override bool IsValid=>Effect!=null && Effect.IsValid;
        public override bool CanExecute(in AbilityContext context)=>IsValid && context.Owner!=null && (AffectCaster || context.Target!=null);
        public override bool Execute(in AbilityContext context)
        {
            if(!CanExecute(in context)) return false;
            var target=AffectCaster ? context.Owner : context.Target;
            // No triggering hit exists: lifesteal/hit-relative formula bases are zero.
            var request=new DamageInfo(0,context.Owner.gameObject,context.Point,context.Direction,origin:context.SourceOrigin);
            var record=new CombatRecord(0,CombatClock.Now,target.Identity,new DamageResult(request,0,false),target);
            var effectContext=new ReactionContext(context.Owner,target,record,context.SourceOrigin.Action.Child(CombatOriginKind.Ability));
            Effect.Execute(in effectContext); return true;
        }
    }
}
