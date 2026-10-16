using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class SharedDamageFormulaTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health target;
        private AttackDefinition attack;
        private DamageFormulaDefinition formula;
        private DamageTypeDefinition fire, poison;
        private GameObject source;
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        [SetUp] public void Setup()
        {
            target = Actor("Target").AddComponent<Health>(); target.SetMaximum(1000);
            source = Actor("Source"); source.AddComponent<Health>();
            fire = Asset<DamageTypeDefinition>(); poison = Asset<DamageTypeDefinition>();
            formula = Asset<DamageFormulaDefinition>();
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.Fixed, 10), new DamageFormulaTerm(poison, StatusDamageBasis.TargetMaximumHealth, .05f) };
            attack = Asset<AttackDefinition>(); attack.Formula = formula;
        }
        [TearDown] public void Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private DamageInfo Shot(float outgoing = 1) => attack.RollDamage(source, default, Vector3.forward, 0, 0, outgoing);
        private void Status(out StatusController controller, out StatusDefinition status, out PeriodicDamageEffect effect)
        {
            controller = target.gameObject.AddComponent<StatusController>();
            effect = Asset<PeriodicDamageEffect>(); effect.Formula = formula;
            status = Asset<StatusDefinition>(); status.Duration = 4; status.TickInterval = 1;
            status.TickEffects = new StatusTickEffect[] { effect };
        }
        [TestCase(100, 15)] [TestCase(1000, 60)]
        public void FixedPlusPercentResolvesAtReceiver(float maximum, float expected)
        {
            var shot = Shot(); target.SetMaximum(maximum);
            var result = target.ApplyDamage(shot);
            Assert.That(result.AppliedAmount, Is.EqualTo(expected).Within(.0001));
            Assert.That(result.Damage.HasFormula, Is.False);
            Assert.That(result.Damage.Parts[0].Type, Is.EqualTo(fire)); Assert.That(result.Damage.Parts[1].Type, Is.EqualTo(poison));
        }
        [Test] public void AllCurrentAndMissingPartsUseSamePreHitHealth()
        {
            target.ApplyDamage(new DamageInfo(400));
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetCurrentHealth, .1f),
                new DamageFormulaTerm(poison, StatusDamageBasis.TargetMissingHealth, .5f) };
            Assert.That(target.ApplyDamage(Shot()).AppliedAmount, Is.EqualTo(260));
        }
        [TestCase(false, 240)] [TestCase(true, 90)]
        public void PercentCanOptOutOfCritical(bool ignore, float expected)
        {
            attack.CriticalChance = 1; attack.CriticalMultiplier = 4; formula.Terms[1].IgnoreCritical = ignore;
            // Fixed 10*4 + percent (50*4 or 50).
            Assert.That(target.ApplyDamage(Shot()).AppliedAmount, Is.EqualTo(expected));
        }
        [Test] public void LimitIsAfterOutgoingCriticalAndZoneButBeforeResistance()
        {
            formula.Terms[1].MaximumDamage = 30; attack.CriticalChance = 1;
            var resist = Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(fire, .5f), new DamageResistance(poison, -.5f));
            var profile = Asset<HealthDefinition>(); profile.MaximumHealth = 1000; profile.Resistances = resist; target.ApplyDefinition(profile);
            var zone = target.gameObject.AddComponent<Hurtbox>(); zone.Configure(target, 3);
            var result = zone.ApplyDamage(Shot(2));
            Assert.That(result.Damage.Amount, Is.EqualTo(150)); // 10*2*2*3 + min(50*2*2*3,30)
            Assert.That(result.AppliedAmount, Is.EqualTo(105)); // 120*.5 + 30*1.5
        }
        [Test] public void FalloffIsAppliedBeforeHardCap()
        {
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetMaximumHealth, .1f) { MaximumDamage = 30 } };
            Assert.That(target.ApplyDamage(Shot().Scale(.5f)).AppliedAmount, Is.EqualTo(30));
        }
        [Test] public void PreparedShotCopiesCoefficientsCapsTypesAndCriticalFlags()
        {
            formula.Terms[1].MaximumDamage = 20; var shot = Shot();
            formula.Terms[0] = new DamageFormulaTerm(poison, StatusDamageBasis.Fixed, 999);
            formula.Terms[1].MaximumDamage = 999; formula.Terms[1].Minimum = formula.Terms[1].Maximum = .9f;
            Assert.That(target.ApplyDamage(shot).AppliedAmount, Is.EqualTo(30));
        }
        [Test] public void IndependentRangesAreRolledOncePerPreparedShot()
        {
            formula.Terms[0].Maximum = 20; formula.Terms[1].Maximum = .1f;
            var samples = new[] { 1f, 0f }; var shot = attack.RollDamage(source, default, Vector3.forward, samples, 1);
            Assert.That(target.ApplyDamage(shot).AppliedAmount, Is.EqualTo(70));
            target.SetMaximum(100); target.ResetToFull();
            Assert.That(target.ApplyDamage(shot).AppliedAmount, Is.EqualTo(25));
        }
        [TestCase(StatusValueSampling.OnApplication, 10)] [TestCase(StatusValueSampling.EveryTick, 20)]
        public void SourceStatCanBeSavedOrReadAtHit(StatusValueSampling sampling, float expected)
        {
            var power = Asset<CombatStatDefinition>(); var stats = source.AddComponent<CombatStats>(); stats.SetValue(power, 100);
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.SourceStat, .1f) { Stat = power, Sampling = sampling } };
            var shot = Shot(); stats.SetValue(power, 200);
            Assert.That(target.ApplyDamage(shot).AppliedAmount, Is.EqualTo(expected));
            Object.DestroyImmediate(source);
            Assert.That(target.ApplyDamage(shot).AppliedAmount, Is.EqualTo(sampling == StatusValueSampling.OnApplication ? 10 : 0));
        }
        [Test] public void ZeroPercentResultStillSupportsContactStatusPolicy()
        {
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetMissingHealth, .1f) };
            var status = Asset<StatusDefinition>(); var controller = target.gameObject.AddComponent<StatusController>();
            attack.OnHitStatuses = new[] { new StatusApplication(status) };
            Assert.That(target.ApplyDamage(Shot()).StatusesApplied, Is.Zero);
            attack.StatusPolicy = StatusApplyPolicy.OnContact;
            Assert.That(target.ApplyDamage(Shot()).StatusesApplied, Is.EqualTo(1)); Assert.That(controller.Count, Is.EqualTo(1));
        }
        [Test] public void InvalidRecipeAndSelfReferencingHitAreRejected()
        {
            formula.Terms[1].MaximumDamage = float.NaN; Assert.That(attack.IsValid, Is.False);
            formula.Terms[1].MaximumDamage = -1; Assert.That(attack.IsValid, Is.False);
            formula.Terms[1].MaximumDamage = 0; formula.Terms[1].Maximum = 0; Assert.That(attack.IsValid, Is.False);
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TriggeringHit, .2f) };
            Assert.That(formula.IsValid, Is.True); Assert.That(attack.IsValid, Is.False); Assert.That(Shot().IsValid, Is.False);
            formula.Terms = new DamageFormulaTerm[9]; Assert.That(formula.IsValid, Is.False);
        }
        [Test] public void StatusStacksKeepTheirOwnTypedPowerAndCriticalAfterFormulaEdit()
        {
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.Fixed, 10), new DamageFormulaTerm(poison, StatusDamageBasis.Fixed, 5) { IgnoreCritical = true } };
            Status(out var controller, out var status, out var effect); effect.CriticalChance = 1;
            controller.Apply(status, source); // 20 + 5
            formula.Terms[0].Minimum = formula.Terms[0].Maximum = 30; effect.CriticalChance = 0;
            controller.Apply(status, source); // 30 + 5
            Object.DestroyImmediate(source);
            Assert.That(controller.TryGetStackDamage(status, 0, 0, out var first), Is.True); Assert.That(first, Is.EqualTo(25));
            Assert.That(controller.TryGetStackDamage(status, 1, 0, out var second), Is.True); Assert.That(second, Is.EqualTo(35));
            controller.Advance(1); Assert.That(target.Current, Is.EqualTo(940));
        }
        [Test] public void StatusCapturesResolvedHitAndCannotApplyWithoutIt()
        {
            Status(out var controller, out var status, out var effect);
            var burnFormula = Asset<DamageFormulaDefinition>(); burnFormula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TriggeringHit, .2f) };
            effect.Formula = burnFormula; Assert.That(controller.Apply(status, source), Is.False);
            attack.OnHitStatuses = new[] { new StatusApplication(status) };
            Assert.That(target.ApplyDamage(Shot()).AppliedAmount, Is.EqualTo(60));
            controller.Advance(1); Assert.That(target.Current, Is.EqualTo(928));
        }
        [TestCase(StatusDamageUnit.PerTick, 30)] [TestCase(StatusDamageUnit.PerSecond, 60)] [TestCase(StatusDamageUnit.OverDuration, 15)]
        public void StatusTermCapUsesSelectedUnitsBeforeConversion(StatusDamageUnit unit, float expected)
        {
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetMaximumHealth, .1f) { MaximumDamage = 30 } };
            Status(out var controller, out var status, out var effect); status.TickInterval = 2; effect.Unit = unit;
            controller.Apply(status, source); controller.Advance(2);
            Assert.That(target.Current, Is.EqualTo(1000 - expected));
        }
        [TestCase(StatusValueSampling.OnApplication, 50)] [TestCase(StatusValueSampling.EveryTick, 100)]
        public void StatusMaximumHealthSamplingIsRespected(StatusValueSampling sampling, float expected)
        {
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetMaximumHealth, .05f) { Sampling = sampling } };
            Status(out var controller, out var status, out _); controller.Apply(status, source); target.SetMaximum(2000);
            controller.Advance(1); Assert.That(target.Current, Is.EqualTo(2000 - expected));
        }
        [Test] public void StatusTotalBudgetSnapshotsLiveHealthAndSource()
        {
            formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetCurrentHealth, .1f) };
            Status(out var controller, out var status, out var effect); effect.Unit = StatusDamageUnit.OverDuration;
            controller.Apply(status, source); target.ApplyDamage(new DamageInfo(100)); controller.Advance(4);
            Assert.That(target.Current, Is.EqualTo(800));
        }
        [Test] public void WarmFormulaDamageAllocatesNoManagedMemory()
        {
            target.SetMaximum(1000000); formula.Terms[1].MaximumDamage = 1; var shot = Shot();
            for (var i = 0; i < 32; i++) target.ApplyDamage(shot);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) target.ApplyDamage(shot);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
        [Test] public void WarmIndependentFormulaCaptureTickAndExpiryAllocateNoManagedMemory()
        {
            target.SetMaximum(1000000); formula.Terms[1].MaximumDamage = 1;
            Status(out var controller, out var status, out _); status.Duration = 1;
            for (var i = 0; i < 32; i++) { controller.Apply(status, source); controller.Advance(1); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) { controller.Apply(status, source); controller.Advance(1); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
    }
}
