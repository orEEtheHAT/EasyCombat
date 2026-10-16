using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Health Profile", fileName = "HealthProfile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class HealthDefinition : ScriptableObject
    {
        [Min(0.001f)] public float MaximumHealth = 100f;
        public ResistanceProfile Resistances;
        public HitRulesDefinition HitRules;
        public DefenseProfile Defense;
        [Tooltip("Independent of damage resistance. These statuses cannot be applied to this actor.")]
        public StatusDefinition[] StatusImmunities = System.Array.Empty<StatusDefinition>();
        public bool IsValid => CombatMath.Positive(MaximumHealth) && (HitRules == null || HitRules.IsValid) && (Defense == null || Defense.IsValid);
    }
}
