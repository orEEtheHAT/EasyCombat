using System;
using UnityEngine;

namespace EasyCombat
{
    public enum StatModifierOperation { Flat, AdditivePercent, Multiply }

    [Serializable]
    public struct StatModifier
    {
        public CombatStatDefinition Stat;
        public StatModifierOperation Operation;
        [Tooltip("Flat: units. AdditivePercent: 0.2 = +20%. Multiply: 1.2 = x1.2.")]
        public float Value;
        public bool IsValid => Stat != null && CombatMath.Finite(Value)
            && (Operation == StatModifierOperation.Flat || Operation == StatModifierOperation.AdditivePercent
                || Operation == StatModifierOperation.Multiply && Value >= 0);
        public StatModifier(CombatStatDefinition stat, StatModifierOperation operation, float value)
        { Stat = stat; Operation = operation; Value = value; }
    }

    [CreateAssetMenu(menuName = "EasyCombat/Stats/Modifier Set", fileName = "StatModifiers")]
    public sealed class StatModifierDefinition : ScriptableObject
    {
        public StatModifier[] Modifiers = Array.Empty<StatModifier>();
        public bool IsValid
        {
            get
            {
                if (Modifiers == null || Modifiers.Length == 0) return false;
                foreach (var modifier in Modifiers) if (!modifier.IsValid) return false;
                return true;
            }
        }
    }

    /// <summary>One owned application, possibly containing several modifiers. Stale handles never remove a new application.</summary>
    public readonly struct StatModifierHandle : IEquatable<StatModifierHandle>
    {
        internal readonly CombatStats Owner;
        internal readonly long Id;
        public long SaveKey=>Id;
        internal StatModifierHandle(CombatStats owner, long id) { Owner = owner; Id = id; }
        public bool IsActive => Owner != null && Owner.Contains(this);
        public bool Remove() => Owner != null && Owner.Remove(this);
        public bool Equals(StatModifierHandle other) => ReferenceEquals(Owner, other.Owner) && Id == other.Id;
        public override bool Equals(object obj) => obj is StatModifierHandle other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }

    public readonly struct StatExplanation
    {
        public readonly float Base, Flat, AdditivePercent, Multiplier, StatusMultiplier, Value;
        public StatExplanation(float basis, float flat, float additive, float multiply, float status, float value)
        { Base = basis; Flat = flat; AdditivePercent = additive; Multiplier = multiply; StatusMultiplier = status; Value = value; }
    }
}
