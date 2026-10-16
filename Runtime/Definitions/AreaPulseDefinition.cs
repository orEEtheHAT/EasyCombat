using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Area Pulses", fileName = "AreaPulses")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AreaPulseDefinition : ScriptableObject
    {
        public AttackDefinition Attack;
        [Min(.001f)] public float Duration = 5;
        [Min(.01f)] public float Interval = .5f;
        public bool ApplyImmediately = true;
        public AreaOverlapGroupDefinition OverlapGroup;
        public int Priority;
        public bool UsesSharedClock => OverlapGroup != null && OverlapGroup.Mode != AreaOverlapMode.Stack;
        public bool IsValid => Attack != null && Attack.Delivery == DamageDelivery.Area && Attack.IsValid
            && CombatMath.Positive(Duration) && (OverlapGroup == null || OverlapGroup.IsValid)
            && (UsesSharedClock || CombatMath.Finite(Interval) && Interval >= .01f);
    }

    /// <summary>Prepared at emission, so a delayed impact does not depend on the source still existing.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct AreaPulseSnapshot
    {
        public readonly DamageInfo Payload;
        public readonly AreaSettings Area;
        public readonly float Duration, Interval;
        public readonly bool ApplyImmediately, IsValid;
        public readonly AreaOverlapGroupDefinition OverlapGroup;
        public readonly AreaOverlapMode OverlapMode;
        public readonly int Priority;
        public AreaPulseSnapshot(AreaPulseDefinition recipe, GameObject source, DamageOrigin parentOrigin = default)
        {
            Payload = default; Area = default; Duration = Interval = 0; ApplyImmediately = IsValid = false;
            OverlapGroup = null; OverlapMode = AreaOverlapMode.Stack; Priority = 0;
            if (recipe == null || !recipe.IsValid) return;
            var statuses = source != null ? source.GetComponent<StatusController>() : null;
            var multiplier = statuses != null ? statuses.GetStatMultiplier(recipe.Attack.DamageModifierStat) : 1;
            if (!CombatMath.Positive(multiplier)) return;
            // The journal names the field's own attack, preserving actor, team and the triggering shot's CastId.
            var origin = parentOrigin.CastId != 0 ? new DamageOrigin(parentOrigin.Actor, recipe.Attack,
                parentOrigin.CastId, hasTeam: parentOrigin.HasTeam, team: parentOrigin.Team) : default;
            Payload = recipe.Attack.RollRandom(source, default, Vector3.forward, multiplier, origin);
            Area = new AreaSettings(recipe.Attack.Area);
            Duration = recipe.Duration; Interval = recipe.Interval; ApplyImmediately = recipe.ApplyImmediately;
            OverlapGroup = recipe.OverlapGroup; Priority = recipe.Priority;
            if (OverlapGroup != null)
            {
                OverlapMode = OverlapGroup.Mode;
                if (OverlapMode != AreaOverlapMode.Stack) { Interval = OverlapGroup.Interval; ApplyImmediately = OverlapGroup.ApplyImmediately; }
            }
            IsValid = Payload.IsValid && Area.Enabled;
        }
    }
}
