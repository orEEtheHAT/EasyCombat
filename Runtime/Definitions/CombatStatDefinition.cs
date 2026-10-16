using UnityEngine;

namespace EasyCombat
{
    /// <summary>A key for a multiplicative stat. Each gameplay adapter decides how to consume it.</summary>
    [CreateAssetMenu(menuName = "EasyCombat/Stat Key", fileName = "Stat")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class CombatStatDefinition : ScriptableObject
    {
        public string DisplayName;
        [TextArea] public string Description;
    }
}
