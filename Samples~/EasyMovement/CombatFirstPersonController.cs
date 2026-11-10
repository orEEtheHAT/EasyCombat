using EasyMovement.FirstPerson;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(MovementInfluences))]
    [AddComponentMenu("EasyCombat/Integrations/EasyMovement Combat FPS")]
    public sealed class CombatFirstPersonController : FirstPersonCharacterController, ICombatMotionReceiver
    {
        private MovementInfluences influences;
        private float SpeedFactor()
        {
            if (!CombatControl.IsAllowed(gameObject, CombatRestriction.Move)) return 0f;
            // EasyMovement selects walk, sprint, crouch, custom gait or flight before we scale it.
            if(!influences) influences=GetComponent<MovementInfluences>();
            return influences ? influences.Multiplier : 1;
        }
        public override float GetMaxSpeed()=>base.GetMaxSpeed()*SpeedFactor();
        public override float GetMinAnalogSpeed()=>base.GetMinAnalogSpeed()*SpeedFactor();

        public override bool IsJumpAllowed() =>
            base.IsJumpAllowed() && CombatControl.IsAllowed(gameObject, CombatRestriction.Jump);

        protected override void OnBeforeSimulationUpdate(float deltaTime)
        {
            if (!CombatControl.IsAllowed(gameObject, CombatRestriction.Move))
                SetMovementDirection(Vector3.zero);
            if (!CombatControl.IsAllowed(gameObject, CombatRestriction.Jump))
                StopJumping();
            base.OnBeforeSimulationUpdate(deltaTime);
        }

        public override void AddYawInput(float value)
        {
            if (value != 0f && CombatControl.IsAllowed(gameObject, CombatRestriction.Turn))
                base.AddYawInput(value);
        }

        protected override void UpdateRotation(float deltaTime)
        {
            if (CombatControl.IsAllowed(gameObject, CombatRestriction.Turn))
                base.UpdateRotation(deltaTime);
        }

        public bool ApplyMotion(in CombatMotionIntent intent)
        {
            if (!isActiveAndEnabled || !intent.IsValid) return false;
            AddForce(intent.VelocityChange, ForceMode.VelocityChange);
            return true;
        }
    }
}
