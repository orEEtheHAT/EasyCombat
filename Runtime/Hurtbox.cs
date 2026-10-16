using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("EasyCombat/Hurtbox")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class Hurtbox : MonoBehaviour, IDamageable
    {
        [Tooltip("Component implementing IDamageable. If empty, looks for a Health in parents.")]
        [SerializeField] private MonoBehaviour receiver;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        public float Multiplier => damageMultiplier;
        public MonoBehaviour Receiver { get { if (receiver == null) receiver = GetComponentInParent<Health>(); return receiver; } }
        public void Configure(MonoBehaviour target, float multiplier = 1f)
        {
            if (!(target is IDamageable) || target is Hurtbox)
                throw new System.ArgumentException("Receiver must implement IDamageable and must not be another hurtbox.", nameof(target));
            if (!CombatMath.Finite(multiplier) || multiplier < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(multiplier));
            receiver = target;
            damageMultiplier = multiplier;
        }

        public DamageResult ApplyDamage(DamageInfo damage)
        {
            if (!isActiveAndEnabled) return DamageResult.Rejected(damage, DamageRejection.Disabled);
            if (!damage.IsValid || !CombatMath.Finite(damageMultiplier) || damageMultiplier < 0f)
                return DamageResult.Rejected(damage, DamageRejection.InvalidAmount);
            if (receiver == null) receiver = GetComponentInParent<Health>();
            if (receiver == null || receiver is Hurtbox || !(receiver is IDamageable target))
                return DamageResult.Rejected(damage, DamageRejection.NoReceiver);
            if (!receiver.isActiveAndEnabled) return DamageResult.Rejected(damage, DamageRejection.Disabled);
            return target.ApplyDamage(damage.Scale(damageMultiplier));
        }
    }
}
