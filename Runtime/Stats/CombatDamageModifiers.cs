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
            var component = damage.Source != null ? damage.Source.GetComponent<CombatDamageModifiers>() : null;
            return component != null && component.isActiveAndEnabled ? damage.Adjust(component.Outgoing) : damage;
        }
    }
}
