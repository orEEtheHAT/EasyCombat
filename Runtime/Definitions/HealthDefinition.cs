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
        [Header("Healing")]
        public bool PreventHealing;
        [Tooltip("Optional absolute multiplier from this actor's CombatStats. Missing/invalid stats reject healing.")]
        public CombatStatDefinition ReceivedHealingMultiplierStat;
        public StatusDefenseProfile StatusDefense;
        [Tooltip("Independent of damage resistance. These statuses cannot be applied to this actor.")]
        public StatusDefinition[] StatusImmunities = System.Array.Empty<StatusDefinition>();
        public bool IsValid => CombatMath.Positive(MaximumHealth) && (HitRules == null || HitRules.IsValid) && (Defense == null || Defense.IsValid)
            && (StatusDefense == null || StatusDefense.IsValid);
    }
}
