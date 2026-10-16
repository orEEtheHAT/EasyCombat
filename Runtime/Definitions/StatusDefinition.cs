using System;
using UnityEngine;

namespace EasyCombat
{
    // Serialized values 0–2 are stable: existing assets keep their chosen behavior.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum StatusStacking { RefreshDuration = 0, StackAndRefresh = 1, IgnoreWhileActive = 2, Independent = 3 }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum StatusStackOverflow { ReplaceSoonestExpiring, IgnoreNew }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct StatusApplication
    {
        public StatusDefinition Status;
        [Range(0f, 1f)] public float Chance;
        public StatusApplication(StatusDefinition status, float chance = 1f) { Status = status; Chance = chance; }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct StatusStatModifier
    {
        public CombatStatDefinition Stat;
        [Min(0f), Tooltip("Applied per stack. 0.8 gives 80% of the stat; two stacks give 64%.")]
        public float MultiplierPerStack;
        public StatusStatModifier(CombatStatDefinition stat, float multiplier)
        { Stat = stat; MultiplierPerStack = multiplier; }
    }

    [CreateAssetMenu(menuName = "EasyCombat/Status", fileName = "Status")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class StatusDefinition : ScriptableObject
    {
        public string DisplayName;
        public Color Color = new Color(1f, 0.5f, 0.15f);
        public Sprite Icon;
        [Min(0.001f)] public float Duration = 4f;
        [Min(0.01f)] public float TickInterval = 1f;
        public StatusStacking Stacking = StatusStacking.Independent;
        [Min(1)] public int MaximumStacks = 3;
        public StatusStackOverflow StackOverflow = StatusStackOverflow.IgnoreNew;
        public StatusTickEffect[] TickEffects = Array.Empty<StatusTickEffect>();
        public StatusStatModifier[] StatModifiers = Array.Empty<StatusStatModifier>();
        [Tooltip("Additive resistance per stack. Total resistance is clamped between -1 and 1.")]
        public DamageResistance[] ResistanceModifiers = Array.Empty<DamageResistance>();
        public string Label => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;
        public bool IsValid => CombatMath.Positive(Duration) && CombatMath.Finite(TickInterval)
            && TickInterval >= 0.01f && MaximumStacks >= 1
            && (Stacking == StatusStacking.RefreshDuration || Stacking == StatusStacking.StackAndRefresh
                || Stacking == StatusStacking.IgnoreWhileActive || Stacking == StatusStacking.Independent)
            && (Stacking != StatusStacking.Independent || StackOverflow == StatusStackOverflow.ReplaceSoonestExpiring || StackOverflow == StatusStackOverflow.IgnoreNew)
            && ValidEffects;
        private bool ValidEffects
        {
            get
            {
                if (TickEffects == null) return true;
                foreach (var effect in TickEffects)
                {
                    if (effect == null) continue;
                    if (!effect.IsValid) return false;
                    if (effect is PeriodicDamageEffect periodic && periodic.Unit == StatusDamageUnit.OverDuration
                        && PeriodicDamageEffect.PlannedTicks(Duration, TickInterval) < 1) return false;
                }
                return true;
            }
        }
    }
}
