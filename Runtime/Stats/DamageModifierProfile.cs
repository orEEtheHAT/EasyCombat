using System;
using UnityEngine;

namespace EasyCombat
{
    public enum DamageModifierKind { All, Direct, Periodic }
    [Serializable]
    public struct DamageModifierRule
    {
        public DamageModifierKind Kind;
        public DamageTypeDefinition Type;
        public CombatTagDefinition RequiredTag;
        public CombatTagDefinition ExcludedTag;
        [Min(0)] public float Multiplier;
        public bool ConvertType;
        public DamageTypeDefinition DestinationType;
        public bool IsValid => Kind >= DamageModifierKind.All && Kind <= DamageModifierKind.Periodic
            && CombatMath.Finite(Multiplier) && Multiplier >= 0 && (!ConvertType || DestinationType != null);
        public DamageModifierRule(float multiplier, DamageTypeDefinition type = null)
        { Kind = DamageModifierKind.All; Type = type; RequiredTag = ExcludedTag = null; Multiplier = multiplier; ConvertType = false; DestinationType = null; }
        public bool Matches(DamageTypeDefinition type, bool periodic, CombatTagSet tags) => (Type == null || Type == type)
            && (Kind == DamageModifierKind.All || periodic == (Kind == DamageModifierKind.Periodic))
            && (RequiredTag == null || tags.Contains(RequiredTag)) && (ExcludedTag == null || !tags.Contains(ExcludedTag));
    }
    [CreateAssetMenu(menuName = "EasyCombat/Damage/Modifier Profile", fileName = "DamageModifiers")]
    public sealed class DamageModifierProfile : ScriptableObject
    {
        public DamageModifierRule[] Rules = Array.Empty<DamageModifierRule>();
        public bool IsValid
        {
            get
            {
                if (Rules == null || Rules.Length > 16) return false;
                foreach (var rule in Rules) if (!rule.IsValid) return false;
                return true;
            }
        }
        internal bool TryAdjust(DamageTypeDefinition original, bool periodic, CombatTagSet tags, out DamageTypeDefinition type, out float multiplier)
        {
            type = original; multiplier = 1;
            if (!IsValid) return false;
            foreach (var rule in Rules)
            {
                if (!rule.Matches(type,periodic,tags)) continue;
                multiplier *= rule.Multiplier;
                if (rule.ConvertType) type = rule.DestinationType;
                if (!CombatMath.Finite(multiplier)) return false;
            }
            return true;
        }
    }
}
