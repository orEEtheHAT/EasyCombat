using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct CombatStatValue
    {
        public CombatStatDefinition Stat;
        [Min(0)] public float Value;
        public CombatStatValue(CombatStatDefinition stat, float value) { Stat = stat; Value = value; }
    }

    /// <summary>Actor-owned stats: max(0, base + flat) * max(0, 1 + additivePercent) * multipliers * statuses.</summary>
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Combat Stats")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed partial class CombatStats : MonoBehaviour
    {
        [SerializeField] private CombatStatsDefinition profile;
        [SerializeField] private CombatStatValue[] values = Array.Empty<CombatStatValue>();
        [SerializeField, Min(1)] private int maximumModifiers = 256;
        private struct Entry { public long Id; public object Source; public StatModifier Modifier; }
        private readonly List<Entry> modifiers = new List<Entry>(16);
        public int ModifierCount => modifiers.Count;
        public int MaximumModifiers { get => maximumModifiers; set => maximumModifiers = Mathf.Max(1, value); }
        public long Revision { get; private set; }
        public CombatStatsDefinition Profile { get => profile; set => profile = value; }
        public bool TryGetValue(CombatStatDefinition stat, out float value)
        {
            var success = TryExplain(stat, out var explanation);
            value = success ? explanation.Value : 0;
            return success;
        }
        public bool TryGetBaseValue(CombatStatDefinition stat, out float value)
        {
            value = 0;
            if (stat == null || !isActiveAndEnabled) return false;
            var found = false;
            if (values != null) for (var i = values.Length - 1; i >= 0; i--)
            {
                if (values[i].Stat != stat) continue;
                value = values[i].Value; found = true; break;
            }
            if (!found && (profile == null || !profile.TryGetValue(stat, out value))) return false;
            return CombatMath.Finite(value) && value >= 0;
        }
        public bool TryExplain(CombatStatDefinition stat, out StatExplanation explanation)
        {
            explanation = default;
            if (!TryGetBaseValue(stat, out var basis)) return false;
            double flat = 0, additive = 0, multiply = 1;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i].Modifier; if (m.Stat != stat) continue;
                switch (m.Operation)
                {
                    case StatModifierOperation.Flat: flat += m.Value; break;
                    case StatModifierOperation.AdditivePercent: additive += m.Value; break;
                    case StatModifierOperation.Multiply: multiply *= m.Value; break;
                }
            }
            var statuses = GetComponent<StatusController>();
            var status = statuses != null ? statuses.GetStatMultiplier(stat) : 1;
            var result = Math.Max(0, basis + flat) * Math.Max(0, 1 + additive) * multiply * status;
            if (double.IsNaN(result) || double.IsInfinity(result) || result > float.MaxValue) return false;
            explanation = new StatExplanation(basis, (float)flat, (float)additive, (float)multiply, status, (float)result);
            return true;
        }
        public bool TryAdd(StatModifier modifier, object source, out StatModifierHandle handle)
        {
            handle = default;
            if (!modifier.IsValid || modifiers.Count >= maximumModifiers) return false;
            var id = CombatEvents.NextId();
            modifiers.Add(new Entry { Id = id, Source = source, Modifier = modifier });
            handle = new StatModifierHandle(this, id); Revision++; return true;
        }
        /// <summary>Copies all values atomically. Editing the shared SO later cannot change this application.</summary>
        public bool TryAdd(StatModifierDefinition definition, object source, out StatModifierHandle handle)
        {
            handle = default;
            if (definition == null || !definition.IsValid || definition.Modifiers.Length > maximumModifiers - modifiers.Count) return false;
            var id = CombatEvents.NextId();
            foreach (var modifier in definition.Modifiers) modifiers.Add(new Entry { Id = id, Source = source, Modifier = modifier });
            handle = new StatModifierHandle(this, id); Revision++; return true;
        }
        public bool Contains(StatModifierHandle handle)
        {
            if (!ReferenceEquals(handle.Owner, this) || handle.Id == 0) return false;
            for (var i = 0; i < modifiers.Count; i++) if (modifiers[i].Id == handle.Id) return true;
            return false;
        }
        public bool Remove(StatModifierHandle handle)
        {
            if (!ReferenceEquals(handle.Owner, this) || handle.Id == 0) return false;
            var count = 0;
            for (var i = modifiers.Count - 1; i >= 0; i--)
                if (modifiers[i].Id == handle.Id) { modifiers.RemoveAt(i); count++; }
            if (count > 0) Revision++;
            return count > 0;
        }
        public int RemoveSource(object source)
        {
            if (source == null) return 0; // Null means unowned; it is never a wildcard.
            var count = 0;
            for (var i = modifiers.Count - 1; i >= 0; i--)
                if (ReferenceEquals(modifiers[i].Source, source)) { modifiers.RemoveAt(i); count++; }
            if (count > 0) Revision++;
            return count;
        }
        public void ClearModifiers() { if (modifiers.Count == 0) return; modifiers.Clear(); Revision++; }
        public void SetValue(CombatStatDefinition stat, float value)
        {
            if (stat == null || !CombatMath.Finite(value) || value < 0) throw new ArgumentException("A stat and nonnegative finite value are required.");
            if (values == null) values = Array.Empty<CombatStatValue>();
            for (var i = values.Length - 1; i >= 0; i--)
                if (values[i].Stat == stat) { values[i].Value = value; Revision++; return; }
            Array.Resize(ref values, values.Length + 1);
            values[values.Length - 1] = new CombatStatValue(stat, value);
            Revision++;
        }
    }
}
