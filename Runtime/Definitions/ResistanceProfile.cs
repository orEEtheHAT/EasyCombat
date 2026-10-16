using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DamageResistance
    {
        public DamageTypeDefinition Type;
        [Range(-1f, 1f), Tooltip("0 = normal; 0.5 = half damage; 1 = immune; -0.5 = 50% extra damage.")]
        public float Reduction;
        public DamageResistance(DamageTypeDefinition type, float reduction) { Type = type; Reduction = reduction; }
    }

    [CreateAssetMenu(menuName = "EasyCombat/Resistance Profile", fileName = "Resistances")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class ResistanceProfile : ScriptableObject
    {
        [SerializeField] private DamageResistance[] entries = Array.Empty<DamageResistance>();
        // Derived lookup only: no entity state lives on this shared asset.
        [NonSerialized] private Dictionary<DamageTypeDefinition, float> lookup;
        public IReadOnlyList<DamageResistance> Entries => entries;

        public float GetReduction(DamageTypeDefinition type)
        {
            if (type == null) return 0f;
            if (lookup == null)
            {
                lookup = new Dictionary<DamageTypeDefinition, float>(entries?.Length ?? 0);
                if (entries != null)
                    for (var i = 0; i < entries.Length; i++)
                        if (entries[i].Type != null)
                            lookup[entries[i].Type] = CombatMath.Finite(entries[i].Reduction)
                                ? Mathf.Clamp(entries[i].Reduction, -1f, 1f) : 0f;
            }
            return lookup.TryGetValue(type, out var reduction) ? reduction : 0f;
        }

        public void Configure(params DamageResistance[] values)
        {
            entries = values == null ? Array.Empty<DamageResistance>() : (DamageResistance[])values.Clone();
            lookup = null;
        }
        private void OnEnable() => lookup = null;
        private void OnValidate() => lookup = null;
    }
}
