using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ControlTests
    {
        [Test] public void OverlappingLeasesDoNotReleaseEachOther()
        {
            var actor=new GameObject("Control");
            try
            {
                var control=actor.AddComponent<CombatControl>();
                control.TryRestrict(CombatRestriction.Move|CombatRestriction.Attack,out var stun);
                control.TryRestrict(CombatRestriction.Move,out var root);
                Assert.That(control.Allows(CombatRestriction.Move),Is.False);
                stun.Remove(); Assert.That(control.Allows(CombatRestriction.Attack),Is.True); Assert.That(control.Allows(CombatRestriction.Move),Is.False);
                root.Remove(); Assert.That(control.Allows(CombatRestriction.Move),Is.True); Assert.That(root.Remove(),Is.False);
            }
            finally { Object.DestroyImmediate(actor); }
        }
        [Test] public void StatusRestrictionsExpireIndependentlyAndEmitterHonorsDisarm()
        {
            var actor=new GameObject("Control"); var disarm=ScriptableObject.CreateInstance<StatusDefinition>(); var root=ScriptableObject.CreateInstance<StatusDefinition>();
            var attack=ScriptableObject.CreateInstance<AttackDefinition>(); attack.Cooldown=0;
            try
            {
                actor.AddComponent<Health>(); var statuses=actor.AddComponent<StatusController>(); var emitter=actor.AddComponent<DamageEmitter>(); emitter.Attack=attack;
                disarm.Restrictions=CombatRestriction.Attack; disarm.Duration=1;
                root.Restrictions=CombatRestriction.Move; root.Duration=2;
                statuses.Apply(disarm); statuses.Apply(root);
                Assert.That(emitter.TryFire(default,Vector3.up),Is.False);
                statuses.Advance(1); Assert.That(emitter.TryFire(default,Vector3.up),Is.True);
                Assert.That(CombatControl.IsAllowed(actor,CombatRestriction.Move),Is.False);
                statuses.Advance(1); Assert.That(CombatControl.IsAllowed(actor,CombatRestriction.Move),Is.True);
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(disarm); Object.DestroyImmediate(root); Object.DestroyImmediate(attack); }
        }
        [Test] public void ClearingManualRestrictionsDoesNotClearStatuses()
        {
            var actor=new GameObject("Control"); var silence=ScriptableObject.CreateInstance<StatusDefinition>();
            try
            {
                var statuses=actor.AddComponent<StatusController>(); var control=actor.AddComponent<CombatControl>(); silence.Restrictions=CombatRestriction.Cast;
                statuses.Apply(silence); control.TryRestrict(CombatRestriction.Move,out var handle); control.ResetRestrictions();
                Assert.That(handle.Remove(),Is.False); Assert.That(control.Allows(CombatRestriction.Move),Is.True); Assert.That(control.Allows(CombatRestriction.Cast),Is.False);
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(silence); }
        }
    }
}
