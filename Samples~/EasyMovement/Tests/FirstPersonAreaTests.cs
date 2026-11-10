using NUnit.Framework;
using ECM2;
using UnityEngine;

namespace EasyCombat.Integrations.Tests
{
    public sealed class FirstPersonAreaTests
    {
        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator CombatRestrictionsAndImpulsesUseTheSamePlayer()
        {
            var actor = new GameObject("Combat FPS");
            actor.SetActive(false);
            var speed = ScriptableObject.CreateInstance<CombatStatDefinition>();
            var slow = ScriptableObject.CreateInstance<StatusDefinition>();
            try
            {
                actor.transform.position = new Vector3(2500, 2500, 2500);
                var statuses = actor.AddComponent<StatusController>();
                var control = actor.AddComponent<CombatControl>();
                var player = actor.AddComponent<CombatFirstPersonController>();
                actor.GetComponent<MovementInfluences>().MovementSpeedStat = speed;
                slow.StatModifiers = new[] { new StatusStatModifier(speed, .5f) };
                player.maxWalkSpeed = 8f;
                player.canEverJump = true;
                actor.SetActive(true);
                yield return null;
                player.SetMovementMode(Character.MovementMode.Falling);
                Assert.IsTrue(player.IsJumpAllowed());
                Assert.IsTrue(statuses.Apply(slow));
                Assert.That(player.GetMaxSpeed(), Is.EqualTo(4f));
                Assert.IsTrue(control.TryRestrict(CombatRestriction.Move | CombatRestriction.Jump | CombatRestriction.Turn, out var lockHandle));
                Assert.That(player.GetMaxSpeed(), Is.Zero);
                Assert.IsFalse(player.IsJumpAllowed());
                var facing = player.GetRotation();
                player.AddControlYawInput(45f);
                var receiver = actor.GetComponent<ICombatMotionReceiver>();
                Assert.AreSame(player, receiver);
                Assert.IsTrue(receiver.ApplyMotion(new CombatMotionIntent(Vector3.right * 6f)));
                player.Simulate(.02f);
                Assert.That(Quaternion.Angle(facing, player.GetRotation()), Is.LessThan(.1f));
                Assert.That(player.GetVelocity().x, Is.GreaterThan(1f));
                Assert.IsTrue(lockHandle.Remove());
                Assert.That(player.GetMaxSpeed(), Is.EqualTo(4f));
                Assert.IsTrue(player.IsJumpAllowed());
                player.AddControlYawInput(45f);
                player.Simulate(.02f);
                Assert.That(Quaternion.Angle(facing, player.GetRotation()), Is.GreaterThan(40f));
            }
            finally
            {
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(slow);
                Object.DestroyImmediate(speed);
            }
        }

        [UnityEngine.TestTools.UnityTest] public System.Collections.IEnumerator AreaMultiplierFollowsWalkSprintCrouchCustomAndFlight()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); var actor=new GameObject("FPS"); actor.SetActive(false);
            var slow=ScriptableObject.CreateInstance<MovementInfluenceDefinition>(); slow.SpeedMultiplier=.5f;
            try
            {
                var position=new Vector3(1550,240,1600); floor.transform.position=position-Vector3.up*.5f; floor.transform.localScale=new Vector3(20,1,20);
                actor.transform.position=position+Vector3.up*.02f; var player=actor.AddComponent<CombatFirstPersonController>();
                JsonUtility.FromJsonOverwrite("{\"customStates\":[{\"stateId\":\"Injured\",\"maxSpeed\":3}]}",player);
                player.maxWalkSpeed=5; player.maxWalkSpeedCrouched=2.5f; player.MaxSprintSpeed=8; player.canEverCrouch=true;
                actor.SetActive(true); player.characterMovement.collisionLayers=1; Physics.SyncTransforms();
                yield return null; // ECM2.Start establishes the initial ground contact.
                player.characterMovement.SetPosition(position+Vector3.up*.02f,true);
                Assert.IsTrue(player.IsGrounded(),"Fixture must stand on the test road before requesting sprint.");
                player.SetMovementMode(Character.MovementMode.Walking);
                var field=actor.GetComponent<MovementInfluences>(); field.Set(floor.transform,slow); Assert.That(player.GetMaxSpeed(),Is.EqualTo(2.5f));
                player.SetMovementDirection(Vector3.forward); player.Sprint(); player.Simulate(.02f);
                Assert.That(player.IsSprinting(),Is.True); Assert.That(player.GetMaxSpeed(),Is.EqualTo(4f));
                player.Crouch(); player.Simulate(.02f); Assert.That(player.IsCrouched(),Is.True); Assert.That(player.GetMaxSpeed(),Is.EqualTo(1.25f));
                player.UnCrouch(); player.Simulate(.02f); Assert.That(player.TrySetCustomState("Injured"),Is.True); Assert.That(player.GetMaxSpeed(),Is.EqualTo(1.5f));
                field.Release(floor.transform); Assert.That(player.GetMaxSpeed(),Is.EqualTo(3f)); field.Set(floor.transform,slow);
                player.maxFlySpeed=12; player.SetMovementMode(Character.MovementMode.Flying); Assert.That(player.GetMaxSpeed(),Is.EqualTo(6f));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(floor); Object.DestroyImmediate(slow); }
        }
    }
}
