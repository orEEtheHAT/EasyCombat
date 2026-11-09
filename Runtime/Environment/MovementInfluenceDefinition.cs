using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Environment/Movement Influence")]
    public class MovementInfluenceDefinition : ScriptableObject
    {
        [Range(0,1),Tooltip("Active zones use the strongest slow. 0.5 halves every gait.")] public float SpeedMultiplier=.5f;
        public virtual bool IsValid => CombatMath.Finite(SpeedMultiplier) && SpeedMultiplier>=0 && SpeedMultiplier<=1;
    }
}
