using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Stats/Damage Modifiers")]
    public sealed class CombatDamageModifiers : MonoBehaviour
    {
        public DamageModifierProfile Outgoing;
        public DamageModifierProfile Incoming;
        public static DamageInfo CaptureOutgoing(DamageInfo damage)
        {
            return damage.Source!=null && damage.Source.TryGetComponent<CombatDamageModifiers>(out var component) && component.isActiveAndEnabled
                ? damage.Adjust(component.Outgoing) : damage;
        }
    }
}
