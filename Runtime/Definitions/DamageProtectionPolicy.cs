using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Damage/Protection Policy", fileName = "DamageProtection")]
    public sealed class DamageProtectionPolicy : ScriptableObject
    {
        public bool BypassResistance;
        public bool BypassArmor;
        public bool BypassShields;
        public bool NonLethal;
        public bool IgnoreMinimumHealth;
        public bool PreventDownedState;
        [Range(0,1)] public float PercentResistancePenetration;
        [Range(0,1)] public float FlatResistancePenetration;
        public bool IsValid => CombatMath.Finite(PercentResistancePenetration) && PercentResistancePenetration >= 0 && PercentResistancePenetration <= 1
            && CombatMath.Finite(FlatResistancePenetration) && FlatResistancePenetration >= 0 && FlatResistancePenetration <= 1;
    }
    public readonly struct DamageProtection
    {
        public readonly bool BypassResistance, BypassArmor, BypassShields;
        public readonly bool NonLethal,IgnoreMinimumHealth,PreventDownedState;
        public readonly float PercentPenetration, FlatPenetration;
        public DamageProtection(DamageProtectionPolicy policy)
        {
            BypassResistance = policy != null && policy.BypassResistance;
            BypassArmor = policy != null && policy.BypassArmor;
            BypassShields = policy != null && policy.BypassShields;
            NonLethal = policy != null && policy.NonLethal;
            IgnoreMinimumHealth = policy != null && policy.IgnoreMinimumHealth;
            PreventDownedState = policy != null && policy.PreventDownedState;
            PercentPenetration = policy != null ? policy.PercentResistancePenetration : 0;
            FlatPenetration = policy != null ? policy.FlatResistancePenetration : 0;
        }
        public bool IsValid => CombatMath.Finite(PercentPenetration) && PercentPenetration >= 0 && PercentPenetration <= 1
            && CombatMath.Finite(FlatPenetration) && FlatPenetration >= 0 && FlatPenetration <= 1;
        public float ApplyResistance(float reduction) => BypassResistance ? 0 : reduction > 0
            ? Mathf.Max(0,reduction*(1-PercentPenetration)-FlatPenetration) : reduction;
    }
}
