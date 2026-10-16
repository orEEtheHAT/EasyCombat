using UnityEngine;

namespace EasyCombat
{
    public enum BodyPartBreakBehavior { KeepColliders, DisableColliders, DisableObject }
    [CreateAssetMenu(menuName="EasyCombat/Composite/Body Part",fileName="BodyPart")]
    public sealed class BodyPartDefinition : ScriptableObject
    {
        public HealthDefinition Health;
        public bool GroupWithCore=true;
        public bool RespectCoreEligibility=true;
        public bool PercentageUsesCoreHealth=true;
        [Range(0,1)] public float CoreDamageFraction=1;
        public bool ShareBeforeLocalHpClamp=true;
        public bool TransferStatusesToCore=true;
        public bool SuppressLocalStatuses=true;
        public BodyPartBreakBehavior OnBreak=BodyPartBreakBehavior.DisableColliders;
        [Tooltip("Optional direct payload applied to the core when this part breaks. Delivery geometry is not used.")]
        public AttackDefinition CoreBreakAttack;
        public bool DisableWithCoreDeath=true;
        public bool ResetWithCore=true;
        public bool IsValid=>Health!=null && Health.IsValid && (Health.LifeRules==null || !Health.LifeRules.AllowDowned)
            && CombatMath.Finite(CoreDamageFraction) && CoreDamageFraction>=0 && CoreDamageFraction<=1
            && OnBreak>=BodyPartBreakBehavior.KeepColliders && OnBreak<=BodyPartBreakBehavior.DisableObject
            && (CoreBreakAttack==null || CoreBreakAttack.IsValid);
    }
}
