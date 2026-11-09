using EasyMovement.FirstPerson;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(MovementInfluences))]
    [AddComponentMenu("EasyCombat/Integrations/EasyMovement Combat FPS")]
    public sealed class CombatFirstPersonController : FirstPersonCharacterController
    {
        private MovementInfluences influences;
        private float SpeedFactor()
        {
            // EasyMovement selects walk, sprint, crouch, custom gait or flight before we scale it.
            if(!influences) influences=GetComponent<MovementInfluences>();
            return influences ? influences.Multiplier : 1;
        }
        public override float GetMaxSpeed()=>base.GetMaxSpeed()*SpeedFactor();
        public override float GetMinAnalogSpeed()=>base.GetMinAnalogSpeed()*SpeedFactor();
    }
}
