using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DefenseLayerResult
    {
        public readonly string Name;
        public readonly int Index;
        public readonly DefenseStage Stage;
        public readonly bool IsShield;
        public readonly float Prevented, Remaining;
        public DefenseLayerResult(string name, int index, DefenseStage stage, bool shield, float prevented, float remaining)
        { Name = name; Index = index; Stage = stage; IsShield = shield; Prevented = prevented; Remaining = remaining; }
    }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DefenseBreakdown
    {
        private DefenseLayerResult a, b, c, d, e, f, g, h;
        public int Count { get; private set; }
        public DefenseLayerResult this[int i] => i >= 0 && i < Count ? i switch
        { 0 => a, 1 => b, 2 => c, 3 => d, 4 => e, 5 => f, 6 => g, _ => h } : throw new ArgumentOutOfRangeException(nameof(i));
        internal void Add(DefenseLayerResult value)
        {
            switch (Count++) { case 0: a = value; break; case 1: b = value; break; case 2: c = value; break; case 3: d = value; break;
                case 4: e = value; break; case 5: f = value; break; case 6: g = value; break; case 7: h = value; break;
                default: throw new InvalidOperationException("Defense capacity exceeded."); }
        }
    }
    /// <summary>One optional, bounded state container per receiver. Scratch states make the whole hit transactional.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageDefenseState
    {
        private readonly DefenseSlot[] slots;
        private readonly DefenseLayerState[] states, pending;
        private readonly string[] names;
        public int Count => slots.Length;
        public DamageDefenseState(DefenseProfile profile)
        {
            if (profile == null || !profile.IsValid) throw new ArgumentException("A valid defense profile is required.");
            slots = (DefenseSlot[])profile.Layers.Clone(); states = new DefenseLayerState[slots.Length];
            pending = new DefenseLayerState[slots.Length]; names = new string[slots.Length];
            for (var i = 0; i < Count; i++) names[i] = string.IsNullOrWhiteSpace(slots[i].Layer.DisplayName) ? slots[i].Layer.name : slots[i].Layer.DisplayName;
            Reset();
        }
        public DefenseLayerState GetState(int index) => states[index];
        public DefenseLayerDefinition GetLayer(int index) => slots[index].Layer;
        public float ShieldCurrent => ShieldTotal(false);
        public float ShieldMaximum => ShieldTotal(true);
        private float ShieldTotal(bool maximum)
        { var sum = 0f; for (var i = 0; i < Count; i++) if (states[i].Enabled && slots[i].Layer != null && slots[i].Layer.IsShield) sum += maximum ? states[i].Maximum : states[i].Current; return sum; }
        internal void Reset()
        {
            for (var i = 0; i < Count; i++)
            { states[i] = slots[i].Layer.CreateState(); states[i].Enabled = !slots[i].DisabledAtStart; }
        }
        internal bool SetEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= Count || states[index].Enabled == enabled) return false;
            states[index].Enabled = enabled; return true;
        }
        internal float RestoreShield(int index, float amount)
        {
            if (index < 0 || index >= Count || slots[index].Layer == null || !slots[index].Layer.IsShield || !CombatMath.Positive(amount)) return 0;
            var restored = Mathf.Min(amount, states[index].Maximum - states[index].Current);
            states[index].Current += restored; return restored;
        }
        internal bool NeedsAdvance
        { get { for (var i = 0; i < Count; i++) if (slots[i].Layer != null && slots[i].Layer.NeedsAdvance(states[i])) return true; return false; } }
        internal bool Advance(float delta)
        {
            var changed = false;
            for (var i = 0; i < Count; i++)
            {
                var layer = slots[i].Layer; if (layer == null || !states[i].Enabled) continue;
                var state = states[i]; layer.Advance(ref state, delta);
                if (!Valid(state)) continue;
                changed |= state.Current != states[i].Current;
                states[i] = state;
            }
            return changed;
        }
        internal void Begin() => Array.Copy(states, pending, Count);
        internal void Commit() => Array.Copy(pending, states, Count);
        internal bool Process(DefenseStage stage, in DefenseContext context, ref DamageAmounts amounts,
            ref DamageAmounts shields, ref DamageAmounts armor, ref DefenseBreakdown breakdown)
        {
            for (var i = 0; i < Count; i++)
            {
                var slot = slots[i]; var layer = slot.Layer;
                if (slot.Stage != stage || !pending[i].Enabled) continue;
                if (layer == null || !layer.IsValid) return false;
                if (!layer.Accepts(context.Damage)) continue;
                var reduced = layer.Reduce(in context, amounts, ref pending[i]);
                if (!reduced.IsValid || reduced.Count != amounts.Count || !Valid(pending[i])) return false;
                var removed = new DamageAmounts();
                for (var c = 0; c < amounts.Count; c++)
                {
                    if (amounts[c].Type != reduced[c].Type || reduced[c].Amount > amounts[c].Amount) return false;
                    removed.Add(amounts[c].Type, amounts[c].Amount - reduced[c].Amount);
                }
                if (layer.IsShield) shields = Add(shields, removed); else armor = Add(armor, removed);
                breakdown.Add(new DefenseLayerResult(names[i], i, stage, layer.IsShield, removed.Total, pending[i].Current));
                amounts = reduced;
            }
            return true;
        }
        private static bool Valid(DefenseLayerState state) => CombatMath.Finite(state.Current) && CombatMath.Finite(state.Maximum)
            && CombatMath.Finite(state.RechargeDelay) && state.Current >= 0 && state.Maximum >= state.Current && state.RechargeDelay >= 0;
        private static DamageAmounts Add(DamageAmounts a, DamageAmounts b)
        { var sum = new DamageAmounts(); for (var i = 0; i < b.Count; i++) sum.Add(b[i].Type, b[i].Amount + (a.Count > i ? a[i].Amount : 0)); return sum; }
    }
}
