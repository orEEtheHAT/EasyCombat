using ECM2;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(Character), typeof(StatusController))]
    [AddComponentMenu("EasyCombat/Integrations/ECM2 Status Modifiers")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Integrations", "GamesFactory.Combat.ECM2")]
    public sealed class CharacterStatusModifiers : MonoBehaviour
    {
        public CombatStatDefinition MovementSpeedStat;
        private Character character;
        private StatusController statuses;
        private CombatControl control;
        private float baseWalkSpeed,baseCrouchSpeed,baseFlySpeed,baseSwimSpeed,baseRotationRate;
        private bool baseCanJump;
        private void Awake()
        {
            character = GetComponent<Character>();
            statuses = GetComponent<StatusController>();
            baseWalkSpeed = character.maxWalkSpeed;
            baseCrouchSpeed=character.maxWalkSpeedCrouched; baseFlySpeed=character.maxFlySpeed; baseSwimSpeed=character.maxSwimSpeed;
            baseCanJump=character.canEverJump;
            baseRotationRate=character.rotationRate;
            control=GetComponent<CombatControl>();
        }
        private void OnEnable() { statuses.Changed += Refresh; if(control!=null) control.Changed+=ControlChanged; Refresh(); }
        private void OnDisable()
        {
            if (statuses != null) statuses.Changed -= Refresh;
            if (control!=null) control.Changed-=ControlChanged;
            if (character != null)
            { character.maxWalkSpeed=baseWalkSpeed; character.maxWalkSpeedCrouched=baseCrouchSpeed; character.maxFlySpeed=baseFlySpeed; character.maxSwimSpeed=baseSwimSpeed; character.canEverJump=baseCanJump; character.rotationRate=baseRotationRate; }
        }
        public void SetBaseWalkSpeed(float speed) { baseWalkSpeed = Mathf.Max(0f, speed); Refresh(); }
        private void ControlChanged(CombatRestriction _) => Refresh();
        private void Refresh()
        {
            var allowed=CombatControl.IsAllowed(gameObject,CombatRestriction.Move);
            var multiplier=allowed ? statuses.GetStatMultiplier(MovementSpeedStat) : 0;
            character.maxWalkSpeed=baseWalkSpeed*multiplier; character.maxWalkSpeedCrouched=baseCrouchSpeed*multiplier;
            character.maxFlySpeed=baseFlySpeed*multiplier; character.maxSwimSpeed=baseSwimSpeed*multiplier;
            character.canEverJump=baseCanJump && CombatControl.IsAllowed(gameObject,CombatRestriction.Jump);
            character.rotationRate=CombatControl.IsAllowed(gameObject,CombatRestriction.Turn) ? baseRotationRate : 0;
            if(!allowed) character.SetMovementDirection(Vector3.zero);
            if(!character.canEverJump) character.StopJumping();
        }
    }
}
