using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Damage Type", fileName = "DamageType")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageTypeDefinition : ScriptableObject
    {
        public string DisplayName;
        public Color Color = Color.white;
        public Sprite Icon;
        public string Label => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName;
    }
}
