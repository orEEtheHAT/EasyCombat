using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Environment/Movement Influence")]
    public sealed class MovementInfluenceDefinition : ScriptableObject
    {
        [Range(0,1),Tooltip("Active zones use the strongest slow. 0.5 halves every gait.")] public float SpeedMultiplier=.5f;
        [Min(0),Tooltip("Extra ground resistance, in m/s² at full wheel support.")] public float RollingDeceleration=4;
        public bool IsValid => CombatMath.Finite(SpeedMultiplier) && SpeedMultiplier>=0 && SpeedMultiplier<=1 && CombatMath.Finite(RollingDeceleration) && RollingDeceleration>=0;
    }
}
