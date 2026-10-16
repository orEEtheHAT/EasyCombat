using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class StatusDamageFormulaTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health health;
        private StatusController controller;
        private StatusDefinition status;
        private PeriodicDamageEffect effect;
        private GameObject source;
        private CombatStatDefinition power;
        private CombatStats stats;
        private T Asset<T>() where T : ScriptableObject
        { var item = ScriptableObject.CreateInstance<T>(); cleanup.Add(item); return item; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        [SetUp] public void Setup()
        {
            health = Actor("Target").AddComponent<Health>(); health.SetMaximum(1000);
            controller = health.gameObject.AddComponent<StatusController>();
            source = Actor("Source"); source.AddComponent<Health>(); stats = source.AddComponent<CombatStats>();
            power = Asset<CombatStatDefinition>(); stats.SetValue(power, 100);
            effect = Asset<PeriodicDamageEffect>(); effect.DamagePerStack = 5;
            status = Asset<StatusDefinition>(); status.Duration = 4; status.TickInterval = 1;
            status.TickEffects = new StatusTickEffect[] { effect };
        }
        [TearDown] public void Teardown()
        {
            for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }
        private void Formula(StatusDamageBasis basis, float value, StatusValueSampling sample = StatusValueSampling.OnApplication)
        { effect.Terms = new[] { new StatusDamageTerm(basis, value, sample) { Stat = power } }; }
        private DamageResult Hit(float amount) => health.ApplyDamage(new DamageInfo(amount, source,
            onHitStatuses: new[] { new StatusApplication(status) }));
        private float Preview(int stack = 0, int effectIndex = 0)
        { Assert.That(controller.TryGetStackDamage(status, stack, effectIndex, out var value), Is.True); return value; }

        [Test] public void LegacyFixedEffectRetainsPerStackDamageAndSnapshotsEachApplication()
        {
            controller.Apply(status); effect.DamagePerStack = 9; controller.Apply(status);
            Assert.That(Preview(0), Is.EqualTo(5)); Assert.That(Preview(1), Is.EqualTo(9));
            controller.Advance(1); Assert.That(health.Current, Is.EqualTo(986));
        }
        [Test] public void IndependentHitStacksKeepDifferentPowerAfterSourceDestruction()
        {
            Formula(StatusDamageBasis.TriggeringHit, .2f);
            Hit(10); Hit(30); Object.DestroyImmediate(source);
            Assert.That(Preview(0), Is.EqualTo(2)); Assert.That(Preview(1), Is.EqualTo(6));
            controller.Advance(1); Assert.That(health.Current, Is.EqualTo(952));
        }
        [Test] public void StrongerHitAtFullLimitCannotChangeExistingPower()
        {
            Formula(StatusDamageBasis.TriggeringHit, .2f); status.MaximumStacks = 1;
            Assert.That(Hit(10).StatusesApplied, Is.EqualTo(1));
            Assert.That(Hit(100).StatusesApplied, Is.Zero);
            Assert.That(Preview(), Is.EqualTo(2));
            status.StackOverflow = StatusStackOverflow.ReplaceSoonestExpiring;
            Assert.That(Hit(30).StatusesApplied, Is.EqualTo(1)); Assert.That(Preview(), Is.EqualTo(6));
        }
        [TestCase(HitDamageStage.Incoming, false, 60)]
        [TestCase(HitDamageStage.AfterResistance, false, 40)]
        [TestCase(HitDamageStage.HealthLost, false, 10)]
        [TestCase(HitDamageStage.Incoming, true, 20)]
        [TestCase(HitDamageStage.AfterResistance, true, 10)]
        [TestCase(HitDamageStage.HealthLost, true, 2.5f)]
        public void HitStagesAndTypeSelectionUseResolvedBreakdown(HitDamageStage stage, bool filtered, float expected)
        {
            var fire = Asset<DamageTypeDefinition>(); var poison = Asset<DamageTypeDefinition>();
            var resist = Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(fire, .5f), new DamageResistance(poison, .25f));
            var profile = Asset<HealthDefinition>(); profile.MaximumHealth = 10; profile.Resistances = resist;
            var victim = Actor("Original victim").AddComponent<Health>(); victim.ApplyDefinition(profile);
            var parts = new DamageAmounts(); parts.Add(fire, 20); parts.Add(poison, 40);
            var result = victim.ApplyDamage(new DamageInfo(parts, source));
            effect.Terms = new[] { new StatusDamageTerm(StatusDamageBasis.TriggeringHit, 1)
                { HitStage = stage, FilterHitType = filtered, HitType = fire } };
            Assert.That(controller.ApplyFromHit(status, in result), Is.True);
            Assert.That(Preview(), Is.EqualTo(expected).Within(.0001f));
        }
        [Test] public void AbsentHitRejectsAtomicallyButAbsentSelectedChannelIsZero()
        {
            Formula(StatusDamageBasis.TriggeringHit, .2f);
            Assert.That(controller.Apply(status, source), Is.False); Assert.That(controller.Count, Is.Zero);
            effect.Terms[0].FilterHitType = true; effect.Terms[0].HitType = Asset<DamageTypeDefinition>();
            Assert.That(Hit(10).StatusesApplied, Is.EqualTo(1)); Assert.That(Preview(), Is.Zero);
        }
        [Test] public void CurrentHealthIsReevaluatedBetweenIndependentTicks()
        {
            Formula(StatusDamageBasis.TargetCurrentHealth, .1f);
            controller.Apply(status); controller.Apply(status); controller.Advance(1);
            Assert.That(health.Current, Is.EqualTo(810).Within(.001f));
        }
        [Test] public void MissingHealthRespondsToHealingBeforeTick()
        {
            Formula(StatusDamageBasis.TargetMissingHealth, .1f);
            health.ApplyDamage(new DamageInfo(400)); controller.Apply(status);
            health.Heal(200); controller.Advance(1);
            Assert.That(health.Current, Is.EqualTo(780));
        }
        [TestCase(StatusValueSampling.OnApplication, 10)]
        [TestCase(StatusValueSampling.EveryTick, 20)]
        public void MaximumHealthCanBeCapturedOrLive(StatusValueSampling sampling, float expected)
        {
            Formula(StatusDamageBasis.TargetMaximumHealth, .01f, sampling); controller.Apply(status);
            health.SetMaximum(2000); Assert.That(Preview(), Is.EqualTo(expected));
            controller.Advance(1); Assert.That(health.Current, Is.EqualTo(2000 - expected));
        }
        [TestCase(StatusValueSampling.OnApplication, 15)]
        [TestCase(StatusValueSampling.EveryTick, 30)]
        public void SourceAttributeIncludesStatusMultiplierAndSamplingPolicy(StatusValueSampling sampling, float expected)
        {
            var modifiers = source.AddComponent<StatusController>();
            var buff = Asset<StatusDefinition>(); buff.StatModifiers = new[] { new StatusStatModifier(power, 1.5f) };
            modifiers.Apply(buff); Formula(StatusDamageBasis.SourceStat, .1f, sampling); controller.Apply(status, source);
            stats.SetValue(power, 200); Assert.That(Preview(), Is.EqualTo(expected).Within(.001f));
        }
        [Test] public void MissingSourceAttributeRejectsApplicationAndLiveSourceLossUsesZero()
        {
            Formula(StatusDamageBasis.SourceStat, .1f, StatusValueSampling.EveryTick);
            Assert.That(controller.Apply(status), Is.False);
            Assert.That(controller.Apply(status, Actor("No stats")), Is.False);
            Assert.That(controller.Apply(status, source), Is.True);
            Object.DestroyImmediate(source); Assert.That(Preview(), Is.Zero);
        }
        [Test] public void SnapshotSourceAttributeSurvivesSourceDestruction()
        {
            Formula(StatusDamageBasis.SourceStat, .1f); controller.Apply(status, source);
            Object.DestroyImmediate(source); controller.Advance(1); Assert.That(health.Current, Is.EqualTo(990));
        }
        [Test] public void SharedStatProfileHasIndependentRuntimeOverrides()
        {
            var profile = Asset<CombatStatsDefinition>(); profile.Values = new[] { new CombatStatValue(power, 50) };
            var first = Actor("First caster").AddComponent<CombatStats>(); first.Profile = profile;
            var second = Actor("Second caster").AddComponent<CombatStats>(); second.Profile = profile;
            first.SetValue(power, 80);
            Assert.That(first.TryGetValue(power, out var a), Is.True); Assert.That(a, Is.EqualTo(80));
            Assert.That(second.TryGetValue(power, out var b), Is.True); Assert.That(b, Is.EqualTo(50));
            Assert.That(profile.Values[0].Value, Is.EqualTo(50));
        }
        [Test] public void ExpiringOneStackKeepsTheOtherStacksPower()
        {
            Formula(StatusDamageBasis.TriggeringHit, .2f); status.Duration = 2;
            Hit(10); controller.Advance(1); Hit(30); controller.Advance(1);
            Assert.That(controller.GetStacks(status), Is.EqualTo(1)); Assert.That(Preview(), Is.EqualTo(6));
            controller.Advance(1); Assert.That(health.Current, Is.EqualTo(944));
        }
        [Test] public void LongFramePreservesIndependentBudgetsAcrossCatchUpCalls()
        {
            effect.Unit = StatusDamageUnit.OverDuration; status.TickInterval = .1f; status.Duration = 2;
            effect.DamagePerStack = 20; controller.Apply(status);
            effect.DamagePerStack = 40; controller.Apply(status);
            controller.Advance(3);
            while (controller.Count > 0) controller.Advance(.01f);
            Assert.That(health.Current, Is.EqualTo(940).Within(.001f));
        }
        [Test] public void CompositeFormulaCapsInSelectedUnitsBeforeOutputResistance()
        {
            effect.Terms = new[] { new StatusDamageTerm(StatusDamageBasis.Fixed, 5), new StatusDamageTerm(StatusDamageBasis.TargetMaximumHealth, .01f) };
            effect.MinimumAmount = 8; effect.MaximumAmount = 10;
            var fire = Asset<DamageTypeDefinition>(); effect.DamageType = fire;
            var resist = Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(fire, .5f));
            var profile = Asset<HealthDefinition>(); profile.MaximumHealth = 1000; profile.Resistances = resist; health.ApplyDefinition(profile);
            controller.Apply(status); Assert.That(Preview(), Is.EqualTo(10)); controller.Advance(1); Assert.That(health.Current, Is.EqualTo(995));
        }
        [TestCase(1, 25)] [TestCase(.5f, 12.5f)] [TestCase(.25f, 6.25f)]
        public void TotalBudgetIsDistributedAcrossAllPlannedTicks(float interval, float perTick)
        {
            effect.Unit = StatusDamageUnit.OverDuration; effect.DamagePerStack = 100; status.TickInterval = interval;
            controller.Apply(status); Assert.That(Preview(), Is.EqualTo(perTick));
            for (var i = 0; i < (int)(4 / interval); i++) controller.Advance(interval);
            Assert.That(health.Current, Is.EqualTo(900).Within(.001f)); Assert.That(controller.Count, Is.Zero);
        }
        [Test] public void TotalBudgetCapturesNormallyLiveFormulaAndHandlesFractionalDuration()
        {
            Formula(StatusDamageBasis.TargetCurrentHealth, .1f); effect.Unit = StatusDamageUnit.OverDuration; status.Duration = 2.5f;
            controller.Apply(status); health.ApplyDamage(new DamageInfo(400));
            Assert.That(Preview(), Is.EqualTo(50)); controller.Advance(3);
            Assert.That(health.Current, Is.EqualTo(500));
        }
        [Test] public void TotalBudgetWithNoScheduledTickIsRejected()
        {
            effect.Unit = StatusDamageUnit.OverDuration; status.Duration = .5f;
            Assert.That(status.IsValid, Is.False); Assert.That(controller.Apply(status), Is.False);
        }
        [Test] public void PerSecondConvertsUsingCapturedTickInterval()
        {
            effect.Unit = StatusDamageUnit.PerSecond; effect.DamagePerStack = 10; status.TickInterval = .25f;
            controller.Apply(status); Assert.That(Preview(), Is.EqualTo(2.5f));
            controller.Advance(1); Assert.That(health.Current, Is.EqualTo(990));
        }
        [Test] public void EarlyRemovalDoesNotPayOutRemainingBudget()
        {
            effect.Unit = StatusDamageUnit.OverDuration; effect.DamagePerStack = 100;
            controller.Apply(status); controller.Advance(1); controller.Remove(status); controller.Advance(10);
            Assert.That(health.Current, Is.EqualTo(975));
        }
        [Test] public void SharedRefreshReplacesBudgetAndUsesActualRemainingCadence()
        {
            status.Stacking = StatusStacking.RefreshDuration; status.Duration = 1.5f;
            effect.Unit = StatusDamageUnit.OverDuration; effect.DamagePerStack = 30;
            controller.Apply(status); controller.Advance(.6f); controller.Apply(status);
            Assert.That(Preview(), Is.EqualTo(15), "There are ticks at 1 and 2 before the refreshed expiry at 2.1.");
            controller.Advance(2); Assert.That(health.Current, Is.EqualTo(970));
        }
        [Test] public void SharedStackingUsesLatestPowerForWholeGroup()
        {
            status.Stacking = StatusStacking.StackAndRefresh; Formula(StatusDamageBasis.TriggeringHit, .2f);
            Hit(10); Hit(30); controller.Advance(1); Assert.That(health.Current, Is.EqualTo(948));
        }
        [Test] public void DifferentEffectsCaptureTheirOwnValues()
        {
            Formula(StatusDamageBasis.TriggeringHit, .1f);
            var other = Asset<PeriodicDamageEffect>(); other.Terms = new[] { new StatusDamageTerm(StatusDamageBasis.TriggeringHit, .3f) };
            status.TickEffects = new StatusTickEffect[] { effect, other };
            Hit(20); Assert.That(Preview(0, 0), Is.EqualTo(2)); Assert.That(Preview(0, 1), Is.EqualTo(6));
            controller.Advance(1); Assert.That(health.Current, Is.EqualTo(972));
        }
        [Test] public void EmptyEffectSlotsPreserveInspectorAndApiIndices()
        {
            status.TickEffects = new StatusTickEffect[] { null, effect }; controller.Apply(status);
            Assert.That(controller.TryGetStackDamage(status, 0, 0, out _), Is.False);
            Assert.That(Preview(0, 1), Is.EqualTo(5)); controller.Advance(1);
            Assert.That(health.Current, Is.EqualTo(995));
        }
        [Test] public void QueuedApplicationCapturesAtRequestNotWhenQueueDrains()
        {
            Formula(StatusDamageBasis.SourceStat, .1f);
            var parent = Asset<StatusDefinition>(); parent.Duration = 1;
            var trigger = Asset<CaptureThenChangeStat>(); trigger.Child = status; trigger.Stat = power;
            parent.TickEffects = new StatusTickEffect[] { trigger };
            controller.Apply(parent, source); controller.Advance(1);
            Assert.That(Preview(), Is.EqualTo(10)); controller.Advance(1); Assert.That(health.Current, Is.EqualTo(990));
        }
        [Test] public void ClearedQueuedApplicationsCannotLeaveCapturedStateBehind()
        {
            Formula(StatusDamageBasis.SourceStat, .1f);
            var parent = Asset<StatusDefinition>(); parent.Duration = 1;
            var trigger = Asset<CaptureThenChangeStat>(); trigger.Child = status; trigger.Stat = power; trigger.Clear = true;
            parent.TickEffects = new StatusTickEffect[] { trigger };
            controller.Apply(parent, source); controller.Advance(1); Assert.That(controller.Count, Is.Zero);
            stats.SetValue(power, 50); controller.Apply(status, source); Assert.That(Preview(), Is.EqualTo(5));
        }
        [Test] public void InvalidFormulaDoesNotPartiallyApplyStatus()
        {
            Formula(StatusDamageBasis.Fixed, float.NaN); Assert.That(controller.Apply(status), Is.False);
            Formula(StatusDamageBasis.Fixed, 5); effect.MinimumAmount = 10; effect.MaximumAmount = 2;
            Assert.That(controller.Apply(status), Is.False); Assert.That(controller.Count, Is.Zero);
        }
        [Test] public void WarmedHitCaptureAndIndependentFormulaTicksAllocateNothing()
        {
            Formula(StatusDamageBasis.TriggeringHit, .2f); status.Duration = 1;
            var applications = new[] { new StatusApplication(status) };
            var hitA = new DamageInfo(10, source, onHitStatuses: applications);
            var hitB = new DamageInfo(30, source, onHitStatuses: applications);
            for (var i = 0; i < 30; i++) { health.ResetToFull(); health.ApplyDamage(hitA); health.ApplyDamage(hitB); controller.Advance(1); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) { health.ResetToFull(); health.ApplyDamage(hitA); health.ApplyDamage(hitB); controller.Advance(1); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero); Assert.That(health.Current, Is.EqualTo(952));
        }
    }

    public sealed class CaptureThenChangeStat : StatusTickEffect
    {
        public StatusDefinition Child;
        public CombatStatDefinition Stat;
        public bool Clear;
        public override void Execute(in StatusTickContext context)
        {
            context.Controller.Apply(Child, context.Source);
            context.Source.GetComponent<CombatStats>().SetValue(Stat, 900);
            if (Clear) context.Controller.Clear();
        }
    }
}
