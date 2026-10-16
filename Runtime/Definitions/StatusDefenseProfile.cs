using System;
using UnityEngine;

namespace EasyCombat
{
    [Serializable]
    public struct StatusDefenseRule
    {
        public StatusDefinition Status;
        public CombatTagDefinition Tag;
        [Range(0,1)] public float ApplicationChanceMultiplier;
        [Min(0)] public float DurationMultiplier;
        [Min(0)] public float PeriodicPowerMultiplier;
        public StatusDefenseRule(StatusDefinition status, float chance = 1,float duration = 1,float power = 1)
        { Status = status; Tag = null; ApplicationChanceMultiplier = chance; DurationMultiplier = duration; PeriodicPowerMultiplier = power; }
        public bool IsValid => CombatMath.Finite(ApplicationChanceMultiplier) && ApplicationChanceMultiplier >= 0 && ApplicationChanceMultiplier <= 1
            && CombatMath.Finite(DurationMultiplier) && DurationMultiplier >= 0 && CombatMath.Finite(PeriodicPowerMultiplier) && PeriodicPowerMultiplier >= 0;
        public bool Matches(StatusDefinition status) => (Status == null || Status == status) && (Tag == null || status != null && status.HasTag(Tag));
    }
    [CreateAssetMenu(menuName = "EasyCombat/Statuses/Defense Profile", fileName = "StatusDefense")]
    public sealed class StatusDefenseProfile : ScriptableObject
    {
        public StatusDefenseRule[] Rules = Array.Empty<StatusDefenseRule>();
        public bool IsValid
        {
            get
            {
                if (Rules == null || Rules.Length > 16) return false;
                foreach (var rule in Rules) if (!rule.IsValid) return false;
                return true;
            }
        }
        public bool TryGetMultipliers(StatusDefinition status,out float chance,out float duration,out float power)
        {
            chance = duration = power = 1;
            if (!IsValid) return false;
            foreach (var rule in Rules)
            {
                if (!rule.Matches(status)) continue;
                chance *= rule.ApplicationChanceMultiplier; duration *= rule.DurationMultiplier; power *= rule.PeriodicPowerMultiplier;
            }
            return CombatMath.Finite(chance) && CombatMath.Finite(duration) && CombatMath.Finite(power);
        }
    }
}
