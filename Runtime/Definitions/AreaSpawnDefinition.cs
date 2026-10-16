using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum AreaSpawnPlacement { Contact, GroundBelow }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum AreaSpawnTrigger { FinalContact, EveryContact }

    [CreateAssetMenu(menuName = "EasyCombat/Area Spawn", fileName = "AreaSpawn")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AreaSpawnDefinition : ScriptableObject
    {
        public AreaPulseDefinition Pulses;
        public DamageArea Prefab;
        public AreaSpawnTrigger Trigger;
        public AreaSpawnPlacement Placement = AreaSpawnPlacement.GroundBelow;
        public LayerMask GroundMask = ~0;
        [Min(0)] public float ProbeHeight = 1;
        [Min(.001f)] public float GroundDistance = 5;
        [Range(0, 89)] public float MaximumSlope = 50;
        [Min(0)] public float SurfaceOffset = .03f;
        public bool IsValid => Pulses != null && Pulses.IsValid && Prefab != null
            && (Trigger == AreaSpawnTrigger.FinalContact || Trigger == AreaSpawnTrigger.EveryContact)
            && (Placement == AreaSpawnPlacement.Contact || Placement == AreaSpawnPlacement.GroundBelow)
            && CombatMath.Finite(ProbeHeight) && ProbeHeight >= 0 && CombatMath.Positive(GroundDistance)
            && CombatMath.Finite(MaximumSlope) && MaximumSlope >= 0 && MaximumSlope <= 89
            && CombatMath.Finite(SurfaceOffset) && SurfaceOffset >= 0;
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct AreaSpawnSnapshot
    {
        public readonly AreaPulseSnapshot Pulses;
        public readonly DamageArea Prefab;
        public readonly AreaSpawnTrigger Trigger;
        public readonly AreaSpawnPlacement Placement;
        public readonly LayerMask GroundMask;
        public readonly float ProbeHeight, GroundDistance, MinimumUpDot, SurfaceOffset;
        public readonly bool Enabled;
        public AreaSpawnSnapshot(AreaSpawnDefinition recipe, GameObject source, DamageOrigin origin = default)
        {
            Pulses = default; Prefab = null; Trigger = default; Placement = default; GroundMask = 0;
            ProbeHeight = GroundDistance = MinimumUpDot = SurfaceOffset = 0; Enabled = false;
            if (recipe == null || !recipe.IsValid) return;
            Pulses = new AreaPulseSnapshot(recipe.Pulses, source, origin);
            Prefab = recipe.Prefab; Trigger = recipe.Trigger; Placement = recipe.Placement; GroundMask = recipe.GroundMask;
            ProbeHeight = recipe.ProbeHeight; GroundDistance = recipe.GroundDistance;
            MinimumUpDot = Mathf.Cos(recipe.MaximumSlope * Mathf.Deg2Rad); SurfaceOffset = recipe.SurfaceOffset;
            Enabled = Pulses.IsValid;
        }
    }
}
