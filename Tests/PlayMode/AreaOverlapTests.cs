using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AreaOverlapTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 center = new Vector3(0, 4500, 0);
        private Health target;
        private GameObject source;
        private AreaOverlapGroupDefinition group;
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string label, float x = 0)
        { var go = new GameObject(label); go.transform.position = center + Vector3.right * x; cleanup.Add(go); return go; }
        private Health Target(float x = 0)
        { var go = Actor("Target", x); go.AddComponent<SphereCollider>().radius = .1f; return go.AddComponent<Health>(); }
        [SetUp] public void Setup()
        {
            target = Target(); source = Actor("Source", 50); source.AddComponent<Health>();
            group = Asset<AreaOverlapGroupDefinition>(); group.Mode = AreaOverlapMode.OnePerTarget; group.Interval = 1;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach (var scheduler in Object.FindObjectsByType<AreaOverlapScheduler>(FindObjectsSortMode.None)) Object.DestroyImmediate(scheduler.gameObject);
            for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear();
            yield return null;
        }
        private AreaPulseDefinition Recipe(float damage, int priority = 0, float duration = 10)
        {
            var shape = Asset<AreaDefinition>(); shape.Radius = 2;
            var attack = Asset<AttackDefinition>(); attack.Delivery = DamageDelivery.Area; attack.Area = shape; attack.Damage = damage;
            var recipe = Asset<AreaPulseDefinition>(); recipe.Attack = attack; recipe.OverlapGroup = group;
            recipe.Interval = 1; recipe.ApplyImmediately = false; recipe.Duration = duration; recipe.Priority = priority; return recipe;
        }
        private DamageArea Start(AreaPulseDefinition recipe, GameObject owner = null, float x = 0)
        {
            var field = Actor("Field", x).AddComponent<DamageArea>(); Physics.SyncTransforms();
            Assert.That(field.Begin(recipe, owner != null ? owner : source), Is.True); return field;
        }
        [Test] public void StackModeKeepsIndependentPulsesAndApplications()
        {
            group.Mode = AreaOverlapMode.Stack;
            var status = Asset<StatusDefinition>(); var controller = target.gameObject.AddComponent<StatusController>();
            var recipe = Recipe(10); recipe.Attack.OnHitStatuses = new[] { new StatusApplication(status) };
            var a = Start(recipe); var b = Start(recipe); a.Advance(1); b.Advance(1);
            Assert.That(target.Current, Is.EqualTo(80)); Assert.That(controller.GetStacks(status), Is.EqualTo(2));
        }
        [Test] public void PrioritySuppressesBothDamageAndStatusBeforeChanceRoll()
        {
            var lowStatus = Asset<StatusDefinition>(); var highStatus = Asset<StatusDefinition>();
            var controller = target.gameObject.AddComponent<StatusController>();
            var low = Recipe(20); low.Attack.OnHitStatuses = new[] { new StatusApplication(lowStatus) };
            var high = Recipe(5, 10); high.Attack.OnHitStatuses = new[] { new StatusApplication(highStatus) };
            Start(low); Start(high); AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current, Is.EqualTo(95)); Assert.That(controller.GetStacks(lowStatus), Is.Zero); Assert.That(controller.GetStacks(highStatus), Is.EqualTo(1));
        }
        [Test] public void EqualPriorityUsesOldestAreaAndSwitchKeepsGroupCadence()
        {
            var first = Start(Recipe(10)); AreaOverlapScheduler.AdvanceSimulation(.4f);
            Start(Recipe(20)); AreaOverlapScheduler.AdvanceSimulation(.6f); Assert.That(target.Current, Is.EqualTo(90));
            first.Stop(); AreaOverlapScheduler.AdvanceSimulation(.4f); Assert.That(target.Current, Is.EqualTo(90));
            AreaOverlapScheduler.AdvanceSimulation(.6f); Assert.That(target.Current, Is.EqualTo(70));
        }
        [Test] public void WinnerIsPerTargetRatherThanOneFieldForWholeMap()
        {
            var other = Target(3); Start(Recipe(10), x: 0); Start(Recipe(20, 1), x: 3);
            AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current, Is.EqualTo(90)); Assert.That(other.Current, Is.EqualTo(80));
        }
        [Test] public void DifferentGroupAssetsAreIndependent()
        {
            Start(Recipe(10)); group = Asset<AreaOverlapGroupDefinition>(); group.Mode = AreaOverlapMode.OnePerTarget;
            Start(Recipe(20)); AreaOverlapScheduler.AdvanceSimulation(1); Assert.That(target.Current, Is.EqualTo(70));
        }
        [Test] public void OnePerSourceSuppressesSameSourceButSumsDifferentDestroyedSources()
        {
            group.Mode = AreaOverlapMode.OnePerSource; var otherSource = Actor("Second source", 70); otherSource.AddComponent<Health>();
            Start(Recipe(10)); Start(Recipe(30)); Start(Recipe(20), otherSource);
            Object.DestroyImmediate(source); Object.DestroyImmediate(otherSource);
            AreaOverlapScheduler.AdvanceSimulation(1); Assert.That(target.Current, Is.EqualTo(70));
        }
        [Test] public void RespawnedSourceCountsAsNewLife()
        {
            group.Mode = AreaOverlapMode.OnePerSource; Start(Recipe(10)); source.GetComponent<Health>().ResetToFull();
            Start(Recipe(20)); AreaOverlapScheduler.AdvanceSimulation(1); Assert.That(target.Current, Is.EqualTo(70));
        }
        [Test] public void ImmediateTickIsArbitratedAndReentryCannotResetIt()
        {
            group.ApplyImmediately = true;
            var a = Start(Recipe(10)); var b = Start(Recipe(20, 1));
            Assert.That(target.Current, Is.EqualTo(100)); AreaOverlapScheduler.AdvanceSimulation(0);
            Assert.That(target.Current, Is.EqualTo(80)); a.Stop(); b.Stop();
            Start(Recipe(5)); AreaOverlapScheduler.AdvanceSimulation(0); Assert.That(target.Current, Is.EqualTo(80));
            AreaOverlapScheduler.AdvanceSimulation(1); Assert.That(target.Current, Is.EqualTo(75));
        }
        [Test] public void EmptyGroupCanStartFreshAfterItsCooldownExpires()
        {
            group.ApplyImmediately = true; var first = Start(Recipe(10)); AreaOverlapScheduler.AdvanceSimulation(0); first.Stop();
            AreaOverlapScheduler.AdvanceSimulation(1); Start(Recipe(20)); AreaOverlapScheduler.AdvanceSimulation(0);
            Assert.That(target.Current, Is.EqualTo(70));
        }
        [Test] public void GroupOverridesLocalTimingAndSnapshotsIt()
        {
            var recipe = Recipe(10); recipe.Interval = float.NaN; recipe.ApplyImmediately = true;
            Start(recipe); group.Interval = .1f; group.Mode = AreaOverlapMode.Stack;
            AreaOverlapScheduler.AdvanceSimulation(.9f); Assert.That(target.Current, Is.EqualTo(100));
            AreaOverlapScheduler.AdvanceSimulation(.1f); Assert.That(target.Current, Is.EqualTo(90));
        }
        [Test] public void MissedTicksAreBoundedAndDrainedBeforeExpiration()
        {
            group.Interval = .1f; var area = Start(Recipe(1, duration: 1));
            AreaOverlapScheduler.AdvanceSimulation(2); Assert.That(target.Current, Is.EqualTo(92)); Assert.That(area.IsRunning, Is.True);
            AreaOverlapScheduler.AdvanceSimulation(0); Assert.That(target.Current, Is.EqualTo(90)); Assert.That(area.IsRunning, Is.False);
        }
        [Test] public void ShorterAreaStopsWithoutWaitingForNextGroupTick()
        {
            var area = Start(Recipe(10, duration: .2f)); AreaOverlapScheduler.AdvanceSimulation(.2f);
            Assert.That(area.IsRunning, Is.False); Assert.That(target.Current, Is.EqualTo(100));
        }
        [Test] public void NewFieldCannotDamageInPastBacklogTicks()
        {
            group.Interval = .1f; Start(Recipe(1, duration: 1)); AreaOverlapScheduler.AdvanceSimulation(2);
            Start(Recipe(10, 10)); AreaOverlapScheduler.AdvanceSimulation(0);
            Assert.That(target.Current, Is.EqualTo(90)); // remaining old ticks .9 and 1; new field starts at 2.
        }
        [Test] public void IneligibleHighPriorityFieldDoesNotSuppressEligibleLowField()
        {
            var high = Recipe(20, 10); high.Attack.Area.TargetMask = 1 << 9;
            Start(Recipe(10)); Start(high); AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current, Is.EqualTo(90));
        }
        [Test] public void WallsAndTeamsParticipateBeforeArbitration()
        {
            source.AddComponent<CombatTeam>().Team = 1; target.gameObject.AddComponent<CombatTeam>().Team = 1;
            var high = Recipe(20, 10); high.Attack.Area.Targets = AreaTargetFilter.Enemies;
            Start(Recipe(10)); Start(high); AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current, Is.EqualTo(90));
            high.Attack.Area.Targets = AreaTargetFilter.All; high.Attack.Area.RequireLineOfSight = true;
            var wall = Actor("Wall", .5f); wall.AddComponent<BoxCollider>().size = new Vector3(.1f, 3, 3);
            Start(high, x: 1); Physics.SyncTransforms(); AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current, Is.EqualTo(80));
        }
        [Test] public void StoppedOrReusedFieldCannotFinishStaleTargetSelection()
        {
            var second = Target(1); var recipe = Recipe(10); var field = Start(recipe);
            var impacts = 0;
            field.Impacted += _ => { impacts++; field.Stop(); };
            AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(impacts, Is.EqualTo(1)); Assert.That(target.Current + second.Current, Is.EqualTo(190));
            Assert.That(field.Begin(recipe, source), Is.True); AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(impacts, Is.EqualTo(2)); Assert.That(target.Current + second.Current, Is.EqualTo(180));
        }
        [Test] public void RespawnedTargetCannotReceiveStaleSelectedHit()
        {
            var second = Target(1); var field = Start(Recipe(10)); var once = false;
            field.Impacted += impact => { if (once) return; once = true; target.ResetToFull(); second.ResetToFull(); };
            AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current + second.Current, Is.EqualTo(200));
        }
        [Test] public void WarmSharedPulsesAllocateNoManagedMemory()
        {
            target.SetMaximum(100000); Start(Recipe(1, duration: 2000)); Start(Recipe(2, 1, 2000));
            for (var i = 0; i < 32; i++) AreaOverlapScheduler.AdvanceSimulation(1);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
        [Test] public void SuppressedFieldDoesNotConsumeRandomStatusChance()
        {
            target.gameObject.AddComponent<StatusController>(); var status = Asset<StatusDefinition>();
            var low = Recipe(10); low.Attack.OnHitStatuses = new[] { new StatusApplication(status, .5f) };
            Start(low); Start(Recipe(5, 1));
            var before = UnityEngine.Random.state;
            AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(before));
        }
        [Test] public void ReusingAnotherSelectedFieldInsideCallbackInvalidatesItsOldLease()
        {
            var second = Target(5); var first = Start(Recipe(10));
            var recipe = Recipe(20); var reused = Start(recipe, x: 5);
            first.Impacted += _ => { reused.Stop(); Assert.That(reused.Begin(recipe, source), Is.True); };
            AreaOverlapScheduler.AdvanceSimulation(1);
            Assert.That(target.Current, Is.EqualTo(90)); Assert.That(second.Current, Is.EqualTo(100));
        }
        [UnityTest] public IEnumerator AutomaticSchedulerUsesScaledTimeAndEndsAreas()
        {
            group.Interval = .05f; var area = Start(Recipe(1, duration: .1f));
            yield return new WaitForSeconds(.2f);
            Assert.That(area.IsRunning, Is.False); Assert.That(target.Current, Is.EqualTo(98));
        }
    }
}
