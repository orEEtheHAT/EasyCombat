using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Delivery/Area Enter Stay Exit",fileName="AreaTrigger")]
    public sealed class AreaTriggerDefinition:ScriptableObject
    {
        public AreaDefinition Area;
        [Tooltip("Payloads only; their own delivery geometry is ignored.")] public AttackDefinition EnterAttack,StayAttack,ExitAttack;
        [Min(0),Tooltip("Zero means until stopped.")] public float Duration=5;
        [Min(.01f)] public float StayInterval=.5f;
        public bool StayImmediately;
        public bool EmitExitOnStop=true;
        public bool StopWithSource;
        [Range(1,4096)] public int MaximumOccupants=256;
        public bool IsValid=>Area!=null && Area.IsValid && ValidAttack(EnterAttack) && ValidAttack(StayAttack) && ValidAttack(ExitAttack)
            && CombatMath.Finite(Duration) && Duration>=0 && CombatMath.Finite(StayInterval) && StayInterval>=.01f
            && MaximumOccupants>0 && MaximumOccupants<=4096;
        private static bool ValidAttack(AttackDefinition attack)=>attack==null || attack.IsValid;
    }
}
