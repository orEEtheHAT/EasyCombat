using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Delivery/Projectile Motion",fileName="ProjectileMotion")]
    public sealed class ProjectileMotionDefinition:ScriptableObject
    {
        public bool Homing;
        public bool AutoAcquire=true;
        [Min(.001f)] public float TurnDegreesPerSecond=360;
        [Tooltip("Search shape, range, team, layers and LOS for homing acquisition and chain jumps.")] public AreaDefinition SearchArea;
        [Range(0,16)] public int Ricochets;
        [Range(0,1)] public float RicochetDamageMultiplier=1;
        [Range(0,32)] public int ChainJumps;
        [Range(0,1)] public float ChainDamageMultiplier=1;
        [Min(0),Tooltip("Zero disables return. Range and lifetime remain the total flight budget.")] public float ReturnAfterSeconds;
        [Min(.001f)] public float CatchRadius=.3f;
        public bool HitAgainOnReturn=true;
        [Range(.001f,.2f)] public float MaximumStep=.05f;
        public bool IsValid=>CombatMath.Positive(TurnDegreesPerSecond) && (SearchArea==null || SearchArea.IsValid)
            && (!(Homing && AutoAcquire || ChainJumps>0) || SearchArea!=null) && Ricochets>=0 && Ricochets<=16 && ChainJumps>=0 && ChainJumps<=32
            && Unit(RicochetDamageMultiplier) && Unit(ChainDamageMultiplier) && CombatMath.Finite(ReturnAfterSeconds) && ReturnAfterSeconds>=0
            && CombatMath.Positive(CatchRadius) && CombatMath.Finite(MaximumStep) && MaximumStep>=.001f && MaximumStep<=.2f;
        private static bool Unit(float value)=>CombatMath.Finite(value) && value>=0 && value<=1;
    }
}
