using UnityEngine;

namespace EasyCombat
{
    public enum ImpactMeasure { NormalSpeed, NormalImpulse, DropHeight, SuspensionLoad }
    [CreateAssetMenu(menuName="EasyCombat/Environment/Impact Damage")]
    public sealed class ImpactDamageDefinition : ScriptableObject
    {
        public ImpactMeasure Measure;
        public DamageTypeDefinition DamageType;
        [Min(0)] public float SafeThreshold = 3;
        [Tooltip("Input: measured amount above Safe Threshold. Output: raw damage before the target's defenses.")]
        public AnimationCurve DamageByExcess = AnimationCurve.Linear(0, 0, 10, 50);
        [Min(0)] public float MaximumDamage = 100;
        [Min(0)] public float RepeatDelay = .15f;
        public bool IsValid => Measure>=ImpactMeasure.NormalSpeed && Measure<=ImpactMeasure.SuspensionLoad
            && CombatMath.Finite(SafeThreshold) && SafeThreshold>=0 && CombatMath.Positive(MaximumDamage)
            && CombatMath.Finite(RepeatDelay) && RepeatDelay>=0 && DamageByExcess!=null && DamageByExcess.length>0;
        public float Evaluate(float magnitude)
        {
            if(!IsValid || !CombatMath.Finite(magnitude) || magnitude<=SafeThreshold) return 0;
            float amount=DamageByExcess.Evaluate(magnitude-SafeThreshold);
            return CombatMath.Finite(amount) ? Mathf.Clamp(amount,0,MaximumDamage) : 0;
        }
    }
}
