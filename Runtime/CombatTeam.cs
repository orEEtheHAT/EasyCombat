using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Combat Team")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class CombatTeam : MonoBehaviour
    {
        [Tooltip("Equal IDs are allies. Used by common hit rules and area target filters.")]
        public int Team;
    }
}
