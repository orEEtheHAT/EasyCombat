using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ResourceTests
    {
        private GameObject actor;
        private CombatResources resources;
        private ResourceDefinition mana, stamina;
        private ResourceProfile profile;
        [SetUp] public void Setup()
        {
            actor = new GameObject("Resources"); resources = actor.AddComponent<CombatResources>();
            mana = ScriptableObject.CreateInstance<ResourceDefinition>(); stamina = ScriptableObject.CreateInstance<ResourceDefinition>();
            mana.RegenerationPerSecond = 10; mana.DelayAfterSpend = 2;
            profile = ScriptableObject.CreateInstance<ResourceProfile>(); profile.Resources = new[] { mana,stamina };
            Assert.That(resources.Configure(profile),Is.True);
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(actor); Object.DestroyImmediate(profile); Object.DestroyImmediate(mana); Object.DestroyImmediate(stamina); }
        private float Current(ResourceDefinition key) { resources.TryGet(key,out var r); return r.Current; }
        [Test] public void MultiResourceSpendIsAllOrNothingAndDuplicatesSum()
        {
            Assert.That(resources.TrySpend(new[] { new ResourceCost(mana,20),new ResourceCost(stamina,101) }),Is.False);
            Assert.That(Current(mana),Is.EqualTo(100));
            Assert.That(resources.TrySpend(new[] { new ResourceCost(mana,60),new ResourceCost(mana,50) }),Is.False);
            Assert.That(resources.TrySpend(new[] { new ResourceCost(mana,20),new ResourceCost(mana,30),new ResourceCost(stamina,10) }),Is.True);
            Assert.That(Current(mana),Is.EqualTo(50)); Assert.That(Current(stamina),Is.EqualTo(90));
        }
        [Test] public void ObserverSeesCommittedTransactionAndCannotReenterIt()
        {
            var calls = 0;
            resources.Changed += _ =>
            {
                Assert.That(Current(stamina),Is.EqualTo(70));
                Assert.That(resources.TrySpend(new[] { new ResourceCost(mana,1) }),Is.False); calls++;
            };
            Assert.That(resources.TrySpend(new[] { new ResourceCost(mana,10),new ResourceCost(stamina,30) }),Is.True);
            Assert.That(calls,Is.EqualTo(2));
        }
        [Test] public void BrokenObserverDoesNotCancelLaterObservers()
        {
            var called = false;
            resources.Changed += _ => throw new InvalidOperationException("Resource observer failure");
            resources.Changed += _ => called = true;
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Resource observer failure");
            Assert.That(resources.TrySpend(new[] { new ResourceCost(mana,10) }),Is.True); Assert.That(called,Is.True);
        }
        [Test] public void RegenerationAccountsForDelayAndRejectsInvalidDelta()
        {
            resources.TrySpend(new[] { new ResourceCost(mana,50) });
            resources.Advance(float.NaN); resources.Advance(-1); Assert.That(Current(mana),Is.EqualTo(50));
            resources.Advance(2.5f); Assert.That(Current(mana),Is.EqualTo(55));
            resources.Advance(100); Assert.That(Current(mana),Is.EqualTo(100));
        }
        [Test] public void ConfigurationCopiesNumbersAndRejectsInvalidProfilesAtomically()
        {
            mana.Maximum = 999; mana.RegenerationPerSecond = 999;
            resources.TrySpend(new[] { new ResourceCost(mana,50) }); resources.Advance(3);
            Assert.That(Current(mana),Is.EqualTo(60));
            profile.Resources = new[] { mana,mana }; Assert.That(resources.Configure(profile),Is.False);
            Assert.That(Current(mana),Is.EqualTo(60)); Assert.That(resources.Count,Is.EqualTo(2));
        }
        [Test] public void CapacitiesCanFollowStatsWithoutHealingByDefault()
        {
            var key = ScriptableObject.CreateInstance<CombatStatDefinition>();
            try
            {
                var stats = actor.AddComponent<CombatStats>(); stats.SetValue(key,100); mana.MaximumStat = key;
                Assert.That(resources.Configure(profile),Is.True); resources.TrySpend(new[] { new ResourceCost(mana,50) });
                stats.SetValue(key,200); Assert.That(resources.RefreshMaximums(),Is.True); Assert.That(Current(mana),Is.EqualTo(50));
                stats.SetValue(key,400); resources.RefreshMaximums(true); Assert.That(Current(mana),Is.EqualTo(100));
                stats.SetValue(key,0); Assert.That(resources.RefreshMaximums(),Is.False); Assert.That(Current(mana),Is.EqualTo(100));
            }
            finally { Object.DestroyImmediate(key); }
        }
        [Test] public void RestoreClampsAndDisablePausesButDoesNotReset()
        {
            resources.TrySpend(new[] { new ResourceCost(mana,80) });
            resources.enabled = false; resources.Advance(10); Assert.That(resources.Restore(mana,10),Is.Zero);
            resources.enabled = true; Assert.That(Current(mana),Is.EqualTo(20));
            Assert.That(resources.Restore(mana,float.MaxValue),Is.EqualTo(80)); Assert.That(Current(mana),Is.EqualTo(100));
        }
        [Test] public void TransactionsAndRegenerationAllocateNothingAfterWarmup()
        {
            var costs = new[] { new ResourceCost(mana,1) };
            for (var i=0;i<32;i++) { resources.TrySpend(costs); resources.Restore(mana,1); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i=0;i<1000;i++) { resources.TrySpend(costs); resources.Restore(mana,1); resources.Advance(.01f); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
        }
    }
}
