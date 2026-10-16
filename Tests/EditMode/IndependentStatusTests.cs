using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class IndependentStatusTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health health;
        private StatusController controller;
        private StatusDefinition status;
        private DamageTypeDefinition fire;
        private CombatJournal journal;
        private T Asset<T>() where T : ScriptableObject
        { var asset = ScriptableObject.CreateInstance<T>(); cleanup.Add(asset); return asset; }
        private GameObject Actor(string name)
        { var actor = new GameObject(name); cleanup.Add(actor); return actor; }
        [SetUp] public void Setup()
        {
            health = Actor("Target").AddComponent<Health>(); controller = health.gameObject.AddComponent<StatusController>();
            fire = Asset<DamageTypeDefinition>(); fire.DisplayName = "Fire";
            var effect = Asset<PeriodicDamageEffect>(); effect.DamageType = fire; effect.DamagePerStack = 2;
            status = Asset<StatusDefinition>(); status.DisplayName = "Independent burn"; status.Duration = 4; status.TickInterval = 1;
            status.TickEffects = new StatusTickEffect[] { effect };
            journal = Actor("Journal").AddComponent<CombatJournal>(); journal.StartRecording();
        }
        [TearDown] public void Teardown()
        {
            journal.StopRecording();
            for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }
        private void RecordBudget(int limit)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("maximumIndependentStackRecords").intValue = limit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [Test] public void NewDefinitionsDefaultToIndependentWithoutReinterpretingLegacyAssets()
        {
            Assert.That(status.Stacking, Is.EqualTo(StatusStacking.Independent));
            Assert.That(status.StackOverflow, Is.EqualTo(StatusStackOverflow.IgnoreNew));
            var old = Asset<StatusDefinition>();
            JsonUtility.FromJsonOverwrite("{\"Stacking\":0}", old); Assert.That(old.Stacking, Is.EqualTo(StatusStacking.RefreshDuration));
            JsonUtility.FromJsonOverwrite("{\"Stacking\":1}", old); Assert.That(old.Stacking, Is.EqualTo(StatusStacking.StackAndRefresh));
            JsonUtility.FromJsonOverwrite("{\"Stacking\":2}", old); Assert.That(old.Stacking, Is.EqualTo(StatusStacking.IgnoreWhileActive));
        }
        [Test] public void SecondApplicationDoesNotExtendFirstAndBoundaryTickIsIncluded()
        {
            controller.Apply(status); controller.Advance(3); var first = controller.GetStack(status, 0);
            controller.Apply(status);
            Assert.That(controller.Count, Is.EqualTo(1)); Assert.That(controller.GetStacks(status), Is.EqualTo(2));
            Assert.That(controller.GetStack(status, 0).Remaining, Is.EqualTo(1));
            Assert.That(controller.GetStack(status, 1).Remaining, Is.EqualTo(4));
            Assert.That(controller.GetStatus(0).NextStackExpiresIn, Is.EqualTo(1));
            Assert.That(controller.GetStatus(0).Remaining, Is.EqualTo(4));
            controller.Advance(1);
            Assert.That(health.Current, Is.EqualTo(90)); // Four ticks from A, one from B.
            Assert.That(controller.GetStacks(status), Is.EqualTo(1)); Assert.That(controller.GetStack(status, 0).Id, Is.Not.EqualTo(first.Id));
            controller.Advance(3);
            Assert.That(health.Current, Is.EqualTo(84)); Assert.That(controller.Count, Is.Zero); Assert.That(controller.IndependentRecordCount, Is.Zero);
        }
        [Test] public void EachStackHasItsOwnTickCadence()
        {
            controller.Apply(status); controller.Advance(.5f); controller.Apply(status);
            controller.Advance(.5f); Assert.That(health.Current, Is.EqualTo(98));
            Assert.That(controller.GetStack(status, 1).NextTickIn, Is.EqualTo(.5f));
            controller.Advance(.5f); Assert.That(health.Current, Is.EqualTo(96));
            controller.Advance(.5f); Assert.That(health.Current, Is.EqualTo(94));
        }
        [Test] public void DefaultOverflowDoesNotLetRapidHitsStarvePeriodicDamage()
        {
            status.MaximumStacks = 1; controller.Apply(status);
            for (var i = 0; i < 10; i++) { controller.Advance(.1f); controller.Apply(status); }
            Assert.That(health.Current, Is.EqualTo(98));
            Assert.That(controller.GetStack(status, 0).Remaining, Is.EqualTo(3).Within(.001f));
        }
        [Test] public void SourcesAndWeaponsStayWithTheirOwnStacksAfterSourceDestruction()
        {
            var first = Actor("First"); first.AddComponent<Health>(); var second = Actor("Second"); second.AddComponent<Health>();
            var weaponA = Asset<AttackDefinition>(); weaponA.DisplayName = "A";
            var weaponB = Asset<AttackDefinition>(); weaponB.DisplayName = "B";
            var originA = DamageOrigin.Capture(first, weaponA); var originB = DamageOrigin.Capture(second, weaponB);
            controller.Apply(status, first, originA); controller.Apply(status, second, originB); Object.DestroyImmediate(first);
            controller.Advance(1);
            Assert.That(journal.Count, Is.EqualTo(2));
            Assert.That(journal.GetNewest(1).Result.Damage.Origin.Actor.Name, Is.EqualTo("First"));
            Assert.That(journal.GetNewest(1).Result.Damage.Origin.CastId, Is.EqualTo(originA.CastId));
            Assert.That(journal.GetNewest(0).Result.Damage.Origin.Actor.Name, Is.EqualTo("Second"));
            Assert.That(journal.GetNewest(0).Result.Damage.Origin.AttackName, Is.EqualTo("B"));
            Assert.That(journal.GetNewest(0).Result.AppliedAmount, Is.EqualTo(2));
        }
        [Test] public void OverflowReplacesSoonestExpiryWithoutExtendingSurvivor()
        {
            status.StackOverflow = StatusStackOverflow.ReplaceSoonestExpiring;
            status.MaximumStacks = 2;
            controller.Apply(status); controller.Advance(.5f); controller.Apply(status);
            var survivor = controller.GetStack(status, 1).Id;
            controller.Advance(.5f); controller.Apply(status);
            Assert.That(controller.GetStacks(status), Is.EqualTo(2));
            var a = controller.GetStack(status, 0); var b = controller.GetStack(status, 1);
            var surviving = a.Id == survivor ? a : b;
            Assert.That(surviving.Id, Is.EqualTo(survivor)); Assert.That(surviving.Remaining, Is.EqualTo(3.5f));
            Assert.That(Mathf.Max(a.Remaining, b.Remaining), Is.EqualTo(4));
        }
        [Test] public void IgnoreOverflowLeavesAllTimersAndSourcesUnchanged()
        {
            status.MaximumStacks = 1; status.StackOverflow = StatusStackOverflow.IgnoreNew;
            controller.Apply(status); var id = controller.GetStack(status, 0).Id; controller.Advance(1);
            Assert.That(controller.Apply(status), Is.False); Assert.That(controller.GetStack(status, 0).Remaining, Is.EqualTo(3));
            Assert.That(controller.GetStack(status, 0).Id, Is.EqualTo(id));
        }
        [Test] public void StatAndResistanceModifiersFallOneStackAtATime()
        {
            status.TickEffects = Array.Empty<StatusTickEffect>(); var stat = Asset<CombatStatDefinition>();
            status.StatModifiers = new[] { new StatusStatModifier(stat, .5f) };
            status.ResistanceModifiers = new[] { new DamageResistance(fire, .25f) };
            controller.Apply(status); controller.Advance(2); controller.Apply(status);
            Assert.That(controller.GetStatMultiplier(stat), Is.EqualTo(.25f)); Assert.That(health.GetResistance(fire), Is.EqualTo(.5f));
            controller.Advance(2);
            Assert.That(controller.GetStatMultiplier(stat), Is.EqualTo(.5f)); Assert.That(health.GetResistance(fire), Is.EqualTo(.25f));
            controller.Advance(2);
            Assert.That(controller.GetStatMultiplier(stat), Is.EqualTo(1)); Assert.That(health.GetResistance(fire), Is.Zero);
        }
        [Test] public void LongFrameSharesCatchUpBudgetAndPreservesAllDueTicks()
        {
            status.Duration = 1; status.TickInterval = .1f; controller.Apply(status); controller.Apply(status);
            controller.Advance(2); Assert.That(journal.Count, Is.EqualTo(8)); Assert.That(controller.GetStacks(status), Is.Zero);
            Assert.That(controller.GetStatus(0).PendingStacks, Is.EqualTo(2));
            controller.Advance(.001f); Assert.That(journal.Count, Is.EqualTo(16));
            controller.Advance(.001f); Assert.That(journal.Count, Is.EqualTo(20));
            Assert.That(health.Current, Is.EqualTo(60)); Assert.That(controller.Count, Is.Zero);
        }
        [Test] public void CatchUpProcessesStacksByDueTimeWithoutStarvingSecondSource()
        {
            status.Duration = 1; status.TickInterval = .1f;
            var first = Actor("First"); first.AddComponent<Health>(); var second = Actor("Second"); second.AddComponent<Health>();
            controller.Apply(status, first); controller.Apply(status, second); controller.Advance(.8f);
            Assert.That(journal.Count, Is.EqualTo(8));
            for (var i = 0; i < 8; i++) Assert.That(journal.GetNewest(7 - i).Result.Damage.Origin.Actor.Name, Is.EqualTo(i % 2 == 0 ? "First" : "Second"));
        }
        [Test] public void ReplacingStackRetainsEarnedTicksButCancelsItsFutureTicks()
        {
            status.StackOverflow = StatusStackOverflow.ReplaceSoonestExpiring;
            status.Duration = 2; status.TickInterval = .1f; status.MaximumStacks = 1;
            controller.Apply(status); controller.Advance(1); Assert.That(journal.Count, Is.EqualTo(8));
            controller.Apply(status);
            Assert.That(controller.GetStacks(status), Is.EqualTo(1)); Assert.That(controller.IndependentRecordCount, Is.EqualTo(2));
            controller.Advance(.001f); Assert.That(journal.Count, Is.EqualTo(10)); Assert.That(controller.IndependentRecordCount, Is.EqualTo(1));
            controller.Advance(.099f); Assert.That(journal.Count, Is.EqualTo(11));
        }
        [Test] public void FullRecordBudgetRejectsReplacementWithoutLosingExistingDebt()
        {
            status.StackOverflow = StatusStackOverflow.ReplaceSoonestExpiring;
            RecordBudget(1); status.Duration = 2; status.TickInterval = .1f; status.MaximumStacks = 1;
            controller.Apply(status); var id = controller.GetStack(status, 0).Id;
            controller.Advance(1); Assert.That(controller.Apply(status), Is.False);
            Assert.That(controller.GetStack(status, 0).Id, Is.EqualTo(id)); Assert.That(controller.GetStack(status, 0).Remaining, Is.EqualTo(1));
            controller.Advance(.001f); Assert.That(journal.Count, Is.EqualTo(10));
            Assert.That(controller.Apply(status), Is.True); Assert.That(controller.IndependentRecordCount, Is.EqualTo(1));
        }
        [Test] public void DefinitionLimitCountsKindsNotIndividualStacks()
        {
            var serialized = new SerializedObject(controller); serialized.FindProperty("maximumActiveStatuses").intValue = 1; serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.Apply(status); controller.Apply(status); controller.Apply(status);
            Assert.That(controller.Count, Is.EqualTo(1)); Assert.That(controller.GetStacks(status), Is.EqualTo(3));
            Assert.That(controller.Apply(Asset<StatusDefinition>()), Is.False);
        }
        [Test] public void RemoveDeathResetAndRestoreReleaseEveryIndependentRecord()
        {
            controller.Apply(status); controller.Apply(status); controller.Remove(status);
            Assert.That(controller.IndependentRecordCount, Is.Zero);
            controller.Apply(status); controller.Apply(status); health.ResetToFull(); Assert.That(controller.IndependentRecordCount, Is.Zero);
            controller.Apply(status); health.RestoreState(new HealthSnapshot(100, 100)); Assert.That(controller.IndependentRecordCount, Is.Zero);
            controller.Apply(status); health.ApplyDamage(new DamageInfo(1000)); Assert.That(controller.IndependentRecordCount, Is.Zero);
        }
        [Test] public void DeathDuringCatchUpStopsOtherStacksImmediately()
        {
            health.SetMaximum(1); health.ResetToFull(); controller.Apply(status); controller.Apply(status); controller.Advance(2);
            Assert.That(journal.Count, Is.EqualTo(1)); Assert.That(controller.Count, Is.Zero); Assert.That(controller.IndependentRecordCount, Is.Zero);
        }
        [Test] public void TickCanQueueAnotherIndependentStackWithoutChangingIteration()
        {
            var child = Asset<StatusDefinition>(); child.DisplayName = "Child";
            var apply = Asset<ApplyAnotherStatusEffect>(); apply.Next = child; status.TickEffects = new StatusTickEffect[] { apply };
            status.Duration = 1; controller.Apply(status); controller.Apply(status); controller.Advance(1);
            Assert.That(controller.GetStacks(status), Is.Zero); Assert.That(controller.GetStacks(child), Is.EqualTo(2));
            Assert.That(controller.IndependentRecordCount, Is.EqualTo(2));
        }
        [Test] public void TickContextIdentifiesIndividualStackInsteadOfMultiplyingByGroupSize()
        {
            var effect = Asset<StackContextProbe>(); status.TickEffects = new StatusTickEffect[] { effect };
            controller.Apply(status); controller.Apply(status); controller.Advance(1);
            Assert.That(effect.Calls, Is.EqualTo(2)); Assert.That(effect.TotalStacks, Is.EqualTo(2));
            Assert.That(effect.FirstId, Is.GreaterThan(0)); Assert.That(effect.LastId, Is.Not.EqualTo(effect.FirstId));
        }
        [Test] public void WarmApplicationsExpiryAndJournalAllocateNoManagedMemory()
        {
            health.SetMaximum(100000); health.ResetToFull(); status.Duration = .02f; status.TickInterval = .01f;
            for (var i = 0; i < 16; i++) { controller.Apply(status); controller.Apply(status); controller.Advance(.025f); }
            Assert.That(journal.Count, Is.GreaterThan(0));
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) { controller.Apply(status); controller.Apply(status); controller.Advance(.025f); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero, "Warmed stack records, effects and journal only; excludes UI/VFX.");
        }
        [Test] public void SharedStackRefreshStillRefreshesWholeGroup()
        {
            status.Stacking = StatusStacking.StackAndRefresh; controller.Apply(status); controller.Advance(3); controller.Apply(status);
            controller.Advance(1); Assert.That(controller.GetStacks(status), Is.EqualTo(2)); Assert.That(controller.GetStatus(0).Remaining, Is.EqualTo(3));
            Assert.That(health.Current, Is.EqualTo(90));
        }
    }
    public sealed class StackContextProbe : StatusTickEffect
    {
        [NonSerialized] public int Calls, TotalStacks;
        [NonSerialized] public long FirstId, LastId;
        public override void Execute(in StatusTickContext context)
        { if (Calls == 0) FirstId = context.StackId; LastId = context.StackId; Calls++; TotalStacks += context.Stacks; }
    }
}
