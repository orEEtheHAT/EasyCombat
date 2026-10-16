using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Abilities/Actions/Attack",fileName="AbilityAttack")]
    public sealed class AbilityAttackAction:CombatAbilityAction
    {
        public AttackDefinition Attack;
        [Tooltip("Apply to the selected Health directly, without delivery geometry.")] public bool DirectToTarget;
        [Tooltip("For Area delivery, place the area at the selected point; otherwise use the caster muzzle.")] public bool AreaAtPoint=true;
        public override bool IsValid=>Attack!=null && Attack.IsValid;
        public override bool CanExecute(in AbilityContext context)=>IsValid && context.Controller!=null
            && context.Controller.CanPrepareAttack(Attack,in context)
            && (DirectToTarget ? context.Target!=null : context.Controller.CanDeliver(Attack));
        public override bool Execute(in AbilityContext context)=>context.Controller.Deliver(this,in context);
    }
}
