using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Stat Profile", fileName = "StatProfile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class CombatStatsDefinition : ScriptableObject
    {
        public CombatStatValue[] Values = Array.Empty<CombatStatValue>();
        public bool TryGetValue(CombatStatDefinition stat, out float value)
        {
            value = 0;
            if (stat == null || Values == null) return false;
            for (var i = Values.Length - 1; i >= 0; i--)
                if (Values[i].Stat == stat)
                { value = Values[i].Value; return CombatMath.Finite(value) && value >= 0; }
            return false;
        }
        public bool IsValid
        {
            get
            {
                if (Values == null) return true;
                foreach (var entry in Values)
                    if (entry.Stat == null || !CombatMath.Finite(entry.Value) || entry.Value < 0) return false;
                return true;
            }
        }
    }
}
