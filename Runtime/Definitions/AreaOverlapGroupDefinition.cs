using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum AreaOverlapMode { Stack, OnePerTarget, OnePerSource }
    [CreateAssetMenu(menuName = "EasyCombat/Area Overlap Group", fileName = "AreaOverlapGroup")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AreaOverlapGroupDefinition : ScriptableObject
    {
        public AreaOverlapMode Mode;
        [Min(.01f)] public float Interval = 1;
        public bool ApplyImmediately;
        public bool IsValid => Mode == AreaOverlapMode.Stack
            || (Mode == AreaOverlapMode.OnePerTarget || Mode == AreaOverlapMode.OnePerSource) && CombatMath.Finite(Interval) && Interval >= .01f;
    }
}
