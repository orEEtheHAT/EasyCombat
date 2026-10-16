using UnityEngine;

namespace EasyCombat
{
    public readonly struct CombatMotionIntent
    {
        public readonly Vector3 VelocityChange;
        public readonly GameObject Source;
        public CombatMotionIntent(Vector3 velocityChange,GameObject source=null) { VelocityChange=velocityChange; Source=source; }
        public bool IsValid => CombatMath.Finite(VelocityChange);
    }
    public interface ICombatMotionReceiver { bool ApplyMotion(in CombatMotionIntent intent); }
}
