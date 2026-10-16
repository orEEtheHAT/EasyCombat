using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Abilities/Actions/Beam",fileName="AbilityBeam")]
    public sealed class AbilityBeamAction:CombatAbilityAction
    {
        public BeamDefinition Beam;
        public override bool IsValid=>Beam!=null && Beam.IsValid;
        public override bool CanExecute(in AbilityContext context)
        { var delivery=context.Owner!=null ? context.Owner.GetComponent<CombatBeam>() : null; return IsValid && delivery!=null && delivery.CanBegin(Beam); }
        public override bool Execute(in AbilityContext context)
        { var delivery=context.Owner!=null ? context.Owner.GetComponent<CombatBeam>() : null; return delivery!=null && delivery.Begin(Beam,context.OriginFor(Beam.Attack)); }
    }
}
