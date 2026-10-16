using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class StatusLifecycleTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health health;
        private StatusController controller;
        private StatusDefinition burn;
        private PeriodicDamageEffect tick;
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        [SetUp] public void Setup()
        {
            var actor=new GameObject("Statuses"); cleanup.Add(actor); health=actor.AddComponent<Health>(); controller=actor.AddComponent<StatusController>();
            burn=Asset<StatusDefinition>(); burn.Duration=2; burn.TickInterval=1;
            tick=Asset<PeriodicDamageEffect>(); tick.DamagePerStack=5; burn.TickEffects=new StatusTickEffect[] { tick };
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void IndependentStacksPublishApplicationTicksAndExpiration()
        {
            var events=new List<StatusChange>(); controller.Lifecycle+=events.Add;
            controller.Apply(burn); controller.Apply(burn); controller.Advance(2);
            Assert.That(events.FindAll(e=>e.Kind==StatusChangeKind.Applied).Count,Is.EqualTo(1));
            Assert.That(events.FindAll(e=>e.Kind==StatusChangeKind.StackAdded).Count,Is.EqualTo(1));
            Assert.That(events.FindAll(e=>e.Kind==StatusChangeKind.Tick).Count,Is.EqualTo(4));
            Assert.That(events.FindAll(e=>e.Kind==StatusChangeKind.Removed && e.RemovalReason==StatusRemovalReason.Expired).Count,Is.EqualTo(2));
        }
        [Test] public void SelectiveDispelLeavesOtherTagsUntouched()
        {
            var tag=Asset<CombatTagDefinition>(); burn.Tags=new[] { tag }; var other=Asset<StatusDefinition>();
            controller.Apply(burn); controller.Apply(other); StatusRemovalReason reason=default;
            controller.Lifecycle+=change=>reason=change.RemovalReason;
            Assert.That(controller.Dispel(tag),Is.EqualTo(1)); Assert.That(controller.GetStacks(other),Is.EqualTo(1)); Assert.That(reason,Is.EqualTo(StatusRemovalReason.Dispelled));
        }
        [Test] public void ConsumeIsExactAndPreservesRemainingIndependentStrength()
        {
            controller.Apply(burn); controller.Advance(.5f); tick.DamagePerStack=10; controller.Apply(burn);
            Assert.That(controller.TryConsume(burn,3),Is.False); Assert.That(controller.GetStacks(burn),Is.EqualTo(2));
            Assert.That(controller.TryConsume(burn,1),Is.True); Assert.That(controller.GetStacks(burn),Is.EqualTo(1));
            controller.TryGetStackDamage(burn,0,0,out var strength); Assert.That(strength,Is.EqualTo(10));
        }
        [Test] public void ResetAndDeathHaveDistinctRemovalReasons()
        {
            var reason=StatusRemovalReason.Cleared; controller.Lifecycle+=change=> { if(change.Kind==StatusChangeKind.Removed) reason=change.RemovalReason; };
            controller.Apply(burn); health.ResetToFull(); Assert.That(reason,Is.EqualTo(StatusRemovalReason.Reset));
            controller.Apply(burn); health.ApplyDamage(new DamageInfo(1000)); Assert.That(reason,Is.EqualTo(StatusRemovalReason.Death));
        }
        [Test] public void DurationAndPeriodicPowerAreCapturedForEachApplication()
        {
            var defense=Asset<StatusDefenseProfile>(); defense.Rules=new[] { new StatusDefenseRule(burn,duration:.5f,power:.5f) };
            var hp=Asset<HealthDefinition>(); hp.StatusDefense=defense; health.ApplyDefinition(hp);
            controller.Apply(burn); defense.Rules[0].DurationMultiplier=2; defense.Rules[0].PeriodicPowerMultiplier=2;
            controller.Apply(burn);
            Assert.That(controller.GetStack(burn,0).Remaining,Is.EqualTo(1)); Assert.That(controller.GetStack(burn,1).Remaining,Is.EqualTo(4));
            controller.Advance(1); Assert.That(health.Current,Is.EqualTo(87.5f)); Assert.That(controller.GetStacks(burn),Is.EqualTo(1));
        }
        [Test] public void ChanceZeroAndTooShortBudgetRejectWithoutPartialApplication()
        {
            var defense=Asset<StatusDefenseProfile>(); defense.Rules=new[] { new StatusDefenseRule(burn,chance:0) };
            var hp=Asset<HealthDefinition>(); hp.StatusDefense=defense; health.ApplyDefinition(hp);
            Assert.That(controller.Apply(burn),Is.False);
            defense.Rules[0]=new StatusDefenseRule(burn,duration:.1f); tick.Unit=StatusDamageUnit.OverDuration;
            Assert.That(controller.Apply(burn),Is.False); Assert.That(controller.GetStacks(burn),Is.Zero);
        }
        [Test] public void BrokenStatusObserverDoesNotCancelTickOrExpiration()
        {
            controller.Apply(burn); controller.Lifecycle+=_=>throw new InvalidOperationException("Status observer failure");
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Status observer failure");
            controller.Advance(1); Assert.That(health.Current,Is.EqualTo(95));
            // Unsubscribe before destroying the fixture, since destruction clears active statuses.
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Status observer failure");
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Status observer failure");
            controller.Advance(1); Assert.That(controller.GetStacks(burn),Is.Zero);
        }
    }
}
