using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Abilities/Actions/Melee",fileName="AbilityMelee")]
    public sealed class AbilityMeleeAction:CombatAbilityAction
    {
        public MeleeDefinition Melee;
        public override bool IsValid=>Melee!=null && Melee.IsValid;
        public override bool CanExecute(in AbilityContext context)
        { var delivery=context.Owner!=null ? context.Owner.GetComponent<CombatMelee>() : null; return IsValid && delivery!=null && delivery.CanBegin(Melee); }
        public override bool Execute(in AbilityContext context)
        { var delivery=context.Owner!=null ? context.Owner.GetComponent<CombatMelee>() : null; return delivery!=null && delivery.Begin(Melee,context.OriginFor(Melee.Attack)); }
    }
}
