using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum AreaShape { Sphere, Cone }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum AreaTargetFilter { All, Enemies, Allies }

    [CreateAssetMenu(menuName = "EasyCombat/Area", fileName = "Area")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AreaDefinition : ScriptableObject
    {
        public AreaShape Shape;
        [Min(.001f)] public float Radius = 4;
        [Range(1, 360)] public float ConeAngle = 90;
        public bool DistanceFalloff;
        [Range(0, 1)] public float EdgeMultiplier = .25f;
        public LayerMask TargetMask = ~0;
        public QueryTriggerInteraction Triggers = QueryTriggerInteraction.Ignore;
        public AreaTargetFilter Targets;
        public bool AffectOwner;
        public bool RequireLineOfSight;
        public LayerMask ObstacleMask = ~0;
        [Tooltip("Off: one body hit per receiver. On: closest eligible hurtbox supplies its multiplier.")]
        public bool UseHurtboxMultiplier;
        public bool IsValid => CombatMath.Positive(Radius) && CombatMath.Finite(ConeAngle) && ConeAngle >= 1 && ConeAngle <= 360
            && CombatMath.Finite(EdgeMultiplier) && EdgeMultiplier >= 0 && EdgeMultiplier <= 1
            && (Shape == AreaShape.Sphere || Shape == AreaShape.Cone)
            && (Targets == AreaTargetFilter.All || Targets == AreaTargetFilter.Enemies || Targets == AreaTargetFilter.Allies);
    }

    /// <summary>Capture at launch; editing/switching a recipe cannot change an in-flight explosion.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct AreaSettings
    {
        public readonly bool Enabled, DistanceFalloff, AffectOwner, RequireLineOfSight, UseHurtboxMultiplier;
        public readonly float Radius, ConeCosine, EdgeMultiplier;
        public readonly AreaShape Shape;
        public readonly AreaTargetFilter Targets;
        public readonly LayerMask TargetMask, ObstacleMask;
        public readonly QueryTriggerInteraction Triggers;
        public AreaSettings(AreaDefinition definition)
        {
            Enabled = definition != null && definition.IsValid;
            Radius = Enabled ? definition.Radius : 0;
            Shape = Enabled ? definition.Shape : default;
            ConeCosine = Enabled ? Mathf.Cos(definition.ConeAngle * .5f * Mathf.Deg2Rad) : 0;
            EdgeMultiplier = Enabled ? definition.EdgeMultiplier : 0;
            DistanceFalloff = Enabled && definition.DistanceFalloff;
            AffectOwner = Enabled && definition.AffectOwner;
            RequireLineOfSight = Enabled && definition.RequireLineOfSight;
            UseHurtboxMultiplier = Enabled && definition.UseHurtboxMultiplier;
            Targets = Enabled ? definition.Targets : default;
            TargetMask = Enabled ? definition.TargetMask : 0;
            ObstacleMask = Enabled ? definition.ObstacleMask : 0;
            Triggers = Enabled ? definition.Triggers : QueryTriggerInteraction.Ignore;
        }
    }
}
