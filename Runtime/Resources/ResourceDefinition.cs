using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Resources/Resource", fileName = "Resource")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        public string DisplayName;
        [Min(.001f)] public float Maximum = 100;
        public CombatStatDefinition MaximumStat;
        [Range(0,1)] public float InitialFraction = 1;
        [Min(0)] public float RegenerationPerSecond;
        [Min(0)] public float DelayAfterSpend;
        public bool IsValid => CombatMath.Positive(Maximum) && CombatMath.Finite(InitialFraction) && InitialFraction >= 0 && InitialFraction <= 1
            && CombatMath.Finite(RegenerationPerSecond) && RegenerationPerSecond >= 0
            && CombatMath.Finite(DelayAfterSpend) && DelayAfterSpend >= 0;
    }
    [Serializable]
    public struct ResourceCost
    {
        public ResourceDefinition Resource;
        [Min(0)] public float Amount;
        public ResourceCost(ResourceDefinition resource, float amount) { Resource = resource; Amount = amount; }
        public bool IsValid => Resource != null && CombatMath.Finite(Amount) && Amount >= 0;
    }
    public enum ResourceChangeReason { Spend, Restore, Regeneration, MaximumChanged, Reset }
    public readonly struct ResourceView
    {
        public readonly ResourceDefinition Definition;
        public readonly float Current, Maximum, RechargeDelay;
        public ResourceView(ResourceDefinition definition, float current, float maximum, float delay)
        { Definition = definition; Current = current; Maximum = maximum; RechargeDelay = delay; }
    }
    public readonly struct ResourceChange
    {
        public readonly ResourceView Resource;
        public readonly float Previous;
        public readonly ResourceChangeReason Reason;
        public ResourceChange(ResourceView resource, float previous, ResourceChangeReason reason)
        { Resource = resource; Previous = previous; Reason = reason; }
    }
}
