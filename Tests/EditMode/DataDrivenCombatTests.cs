using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class DataDrivenCombatTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health health;
        private StatusController statuses;
        private DamageTypeDefinition fire, poison;
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            cleanup.Add(asset);
            return asset;
        }
        private Health Actor()
        {
            var go = new GameObject("Data driven actor");
            cleanup.Add(go);
            return go.AddComponent<Health>();
        }
        [SetUp] public void SetUp()
        {
            health = Actor();
            statuses = health.gameObject.AddComponent<StatusController>();
            fire = Asset<DamageTypeDefinition>();
            poison = Asset<DamageTypeDefinition>();
        }
        [TearDown] public void TearDown()
        {
            for (var i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear();
        }
        private void SetResistance(float amount)
        {
            var profile = Asset<ResistanceProfile>();
            profile.Configure(new DamageResistance(fire, amount));
            var definition = Asset<HealthDefinition>();
            definition.Resistances = profile;
            health.ApplyDefinition(definition);
        }
        private StatusDefinition Burn(float duration = 4, float interval = 1, float damage = 5)
        {
            var tick = Asset<PeriodicDamageEffect>();
            tick.DamageType = fire;
            tick.DamagePerStack = damage;
            var definition = Asset<StatusDefinition>();
            definition.Duration = duration;
            definition.TickInterval = interval;
            definition.TickEffects = new StatusTickEffect[] { tick };
            return definition;
        }
        [Test] public void DamageRangeAndCriticalBoundaryRolls()
        {
            var weapon = Asset<AttackDefinition>();
            weapon.MinimumDamage = 10; weapon.MaximumDamage = 20;
            weapon.CriticalChance = 0.25f; weapon.CriticalMultiplier = 3; weapon.DamageType = fire;
            Assert.That(weapon.RollDamage(null, default, default, 0, 0.5f).Amount, Is.EqualTo(10));
            var crit = weapon.RollDamage(null, default, default, 1, 0.1f);
            Assert.That(crit.Amount, Is.EqualTo(60));
            Assert.That(crit.IsCritical, Is.True);
            Assert.That(crit.Type, Is.SameAs(fire));
            Assert.That(weapon.RollDamage(null, default, default, 0.5f, 0.25f).IsCritical, Is.False);
            weapon.CriticalChance = 1;
            Assert.That(weapon.RollDamage(null, default, default, 0, 1).IsCritical, Is.True);
            weapon.CriticalChance = 0;
            Assert.That(weapon.RollDamage(null, default, default, 0, 0).IsCritical, Is.False);
            weapon.RoundsPerMinute = 600;
            Assert.That(weapon.Cooldown, Is.EqualTo(0.1f).Within(0.00001f));
        }
        [Test] public void LegacyWeaponUpgradePreservesFixedDamageAndCooldown()
        {
            var weapon = Asset<AttackDefinition>();
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("schemaVersion").intValue = 0;
            serialized.FindProperty("MinimumDamage").floatValue = 7f;
            serialized.FindProperty("MaximumDamage").floatValue = 25f;
            serialized.FindProperty("legacyCooldown").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            weapon.UpgradeLegacyData();
            Assert.That(weapon.MinimumDamage, Is.EqualTo(7));
            Assert.That(weapon.MaximumDamage, Is.EqualTo(7));
            Assert.That(weapon.RoundsPerMinute, Is.EqualTo(120));
        }
        [Test] public void TypedResistanceDoesNotAffectUnknownOrUntypedDamage()
        {
            SetResistance(0.5f);
            Assert.That(health.ApplyDamage(new DamageInfo(20, type: fire)).AppliedAmount, Is.EqualTo(10));
            Assert.That(health.ApplyDamage(new DamageInfo(20, type: poison)).AppliedAmount, Is.EqualTo(20));
            Assert.That(health.ApplyDamage(new DamageInfo(20)).AppliedAmount, Is.EqualTo(20));
        }
        [Test] public void ImmunityBlocksDamageAndOnHitStatus()
        {
            SetResistance(1);
            var burn = Burn();
            var result = health.ApplyDamage(new DamageInfo(20, type: fire, onHitStatuses: new[] { new StatusApplication(burn) }));
            Assert.That(result.Rejection, Is.EqualTo(DamageRejection.Immune));
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void VulnerabilityAmplifiesDamageWithoutHealing()
        {
            SetResistance(-0.5f);
            Assert.That(health.ApplyDamage(new DamageInfo(20, type: fire)).AppliedAmount, Is.EqualTo(30));
        }
        [Test] public void CriticalThenHurtboxThenResistanceAreAppliedOnce()
        {
            SetResistance(0.5f);
            var weapon = Asset<AttackDefinition>();
            weapon.Damage = 10; weapon.CriticalChance = 1; weapon.CriticalMultiplier = 3; weapon.DamageType = fire;
            var hurtbox = health.gameObject.AddComponent<Hurtbox>();
            hurtbox.Configure(health, 2);
            var result = hurtbox.ApplyDamage(weapon.RollDamage(null, default, default, 0, 0));
            Assert.That(result.AppliedAmount, Is.EqualTo(30));
            Assert.That(result.Damage.IsCritical, Is.True);
        }
        [Test] public void SharedDefinitionsNeverShareHealthOrStatusTimers()
        {
            var definition = Asset<HealthDefinition>();
            definition.MaximumHealth = 200;
            health.ApplyDefinition(definition);
            var other = Actor();
            other.ApplyDefinition(definition);
            var otherStatuses = other.gameObject.AddComponent<StatusController>();
            var burn = Burn();
            statuses.Apply(burn);
            otherStatuses.Apply(burn);
            statuses.Advance(1);
            Assert.That(health.Current, Is.EqualTo(195));
            Assert.That(other.Current, Is.EqualTo(200));
            Assert.That(otherStatuses.GetStatus(0).Remaining, Is.EqualTo(4));
            Assert.That(definition.MaximumHealth, Is.EqualTo(200));
            Assert.That(burn.Duration, Is.EqualTo(4));
        }
        [Test] public void PeriodicDamageUsesResistanceAndNeverAppliesProcs()
        {
            SetResistance(0.5f);
            var burn = Burn(3, 1, 10);
            health.ApplyDamage(new DamageInfo(20, type: poison, onHitStatuses: new[] { new StatusApplication(burn) }));
            var ticks = 0;
            health.Damaged += result => { if (result.Damage.IsPeriodic) { ticks++; Assert.That(result.Damage.IsCritical, Is.False); } };
            statuses.Advance(3);
            Assert.That(ticks, Is.EqualTo(3));
            Assert.That(health.Current, Is.EqualTo(65));
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void RefreshPreservesTickCadence()
        {
            var burn = Burn();
            burn.Stacking = StatusStacking.RefreshDuration;
            statuses.Apply(burn);
            statuses.Advance(0.75f);
            statuses.Apply(burn);
            statuses.Advance(0.25f);
            Assert.That(health.Current, Is.EqualTo(95));
            Assert.That(statuses.GetStacks(burn), Is.EqualTo(1));
            Assert.That(statuses.GetStatus(0).Remaining, Is.EqualTo(3.75f).Within(0.001f));
        }
        [Test] public void StackingIsCappedAndTicksScalePerStack()
        {
            var burn = Burn();
            burn.Stacking = StatusStacking.StackAndRefresh;
            burn.MaximumStacks = 3;
            for (var i = 0; i < 8; i++) statuses.Apply(burn);
            statuses.Advance(1);
            Assert.That(statuses.GetStacks(burn), Is.EqualTo(3));
            Assert.That(health.Current, Is.EqualTo(85));
        }
        [Test] public void IgnorePolicyDoesNotRefreshDuration()
        {
            var burn = Burn(2);
            burn.Stacking = StatusStacking.IgnoreWhileActive;
            statuses.Apply(burn);
            statuses.Advance(1.5f);
            Assert.That(statuses.Apply(burn), Is.False);
            statuses.Advance(0.5f);
            Assert.That(statuses.Count, Is.Zero);
            Assert.That(health.Current, Is.EqualTo(90));
        }
        [Test] public void LongFrameCatchUpIsBoundedWithoutLosingTicks()
        {
            var burn = Burn(1, 0.1f, 1);
            statuses.Apply(burn);
            statuses.Advance(2);
            Assert.That(health.Current, Is.EqualTo(92)); // 8-tick budget.
            Assert.That(statuses.Count, Is.EqualTo(1)); // Outstanding ticks are retained.
            statuses.Advance(0.001f);
            Assert.That(health.Current, Is.EqualTo(90));
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void TimePauseFreezesStatuses()
        {
            var burn = Burn();
            statuses.Apply(burn);
            statuses.Advance(0);
            statuses.Advance(float.NaN);
            Assert.That(statuses.GetStatus(0).Remaining, Is.EqualTo(4));
            Assert.That(health.Current, Is.EqualTo(100));
        }
        [Test] public void ExpiredModifiersDoNotLingerWhileCatchUpTicksDrain()
        {
            var stat = Asset<CombatStatDefinition>();
            var burn = Burn(1, 0.1f, 1);
            burn.StatModifiers = new[] { new StatusStatModifier(stat, 0.5f) };
            statuses.Apply(burn);
            Assert.That(statuses.GetStatMultiplier(stat), Is.EqualTo(0.5f));
            statuses.Advance(2);
            Assert.That(statuses.Count, Is.EqualTo(1));
            Assert.That(statuses.GetStatMultiplier(stat), Is.EqualTo(1));
            statuses.Advance(0.001f);
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void DeathStopsRemainingTicksAndEffects()
        {
            var burn = Burn(10, 0.1f, 200);
            statuses.Apply(burn);
            var deaths = 0;
            health.Died += _ => deaths++;
            statuses.Advance(5);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(statuses.Count, Is.Zero);
            Assert.That(health.IsAlive, Is.False);
        }
        [Test] public void ResetAndRestoreClearStatusesAndModifiers()
        {
            var stat = Asset<CombatStatDefinition>();
            var slow = Asset<StatusDefinition>();
            slow.StatModifiers = new[] { new StatusStatModifier(stat, 0.5f) };
            statuses.Apply(slow);
            Assert.That(statuses.GetStatMultiplier(stat), Is.EqualTo(0.5f));
            health.ResetToFull();
            Assert.That(statuses.GetStatMultiplier(stat), Is.EqualTo(1));
            statuses.Apply(slow);
            health.RestoreState(new HealthSnapshot(100, 60));
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void StatAndResistanceModifiersDisappearAfterExpiry()
        {
            SetResistance(0.5f);
            var stat = Asset<CombatStatDefinition>();
            var ward = Asset<StatusDefinition>();
            ward.Duration = 2;
            ward.ResistanceModifiers = new[] { new DamageResistance(fire, 0.5f) };
            ward.StatModifiers = new[] { new StatusStatModifier(stat, 0.5f) };
            statuses.Apply(ward);
            Assert.That(health.GetResistance(fire), Is.EqualTo(1));
            Assert.That(statuses.GetStatMultiplier(stat), Is.EqualTo(0.5f));
            statuses.Advance(2);
            Assert.That(health.GetResistance(fire), Is.EqualTo(0.5f));
            Assert.That(statuses.GetStatMultiplier(stat), Is.EqualTo(1));
        }
        [Test] public void DistinctResistanceProfilesAndCacheInvalidation()
        {
            var profile = Asset<ResistanceProfile>();
            profile.Configure(new DamageResistance(fire, 0.2f));
            Assert.That(profile.GetReduction(fire), Is.EqualTo(0.2f));
            profile.Configure(new DamageResistance(fire, 0.7f), new DamageResistance(poison, -0.3f));
            Assert.That(profile.GetReduction(fire), Is.EqualTo(0.7f));
            Assert.That(profile.GetReduction(poison), Is.EqualTo(-0.3f));
        }
        [Test] public void ZeroChanceAndKillingHitDoNotApplyStatuses()
        {
            var burn = Burn();
            health.ApplyDamage(new DamageInfo(10, onHitStatuses: new[] { new StatusApplication(burn, 0) }));
            Assert.That(statuses.Count, Is.Zero);
            health.ApplyDamage(new DamageInfo(200, onHitStatuses: new[] { new StatusApplication(burn) }));
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void RegenerationHealsAndDoesNotRevive()
        {
            var effect = Asset<PeriodicHealingEffect>();
            effect.HealingPerStack = 8;
            var regeneration = Asset<StatusDefinition>();
            regeneration.TickEffects = new StatusTickEffect[] { effect };
            health.ApplyDamage(new DamageInfo(20));
            statuses.Apply(regeneration);
            statuses.Advance(1);
            Assert.That(health.Current, Is.EqualTo(88));
            health.ApplyDamage(new DamageInfo(200));
            Assert.That(statuses.Apply(regeneration), Is.False);
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void NewCustomBehaviourCanQueueAnotherStatusSafely()
        {
            var next = Asset<StatusDefinition>();
            var effect = Asset<ApplyAnotherStatusEffect>();
            effect.Next = next;
            var first = Asset<StatusDefinition>();
            first.Duration = 1;
            first.TickEffects = new StatusTickEffect[] { effect };
            statuses.Apply(first);
            statuses.Advance(1);
            Assert.That(statuses.GetStacks(first), Is.Zero);
            Assert.That(statuses.GetStacks(next), Is.EqualTo(1));
        }
        [Test] public void WarmDamageAndStatusHotPathAllocatesNoManagedMemory()
        {
            health.SetMaximum(100000);
            health.ResetToFull();
            var burn = Burn(100000, 1, 1);
            statuses.Apply(burn);
            var damage = new DamageInfo(1, type: fire);
            for (var i = 0; i < 32; i++) { health.ApplyDamage(damage); statuses.Advance(0.01f); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++) { health.ApplyDamage(damage); statuses.Advance(0.01f); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Measured only synchronous warmed combat core, excluding rendering/editor/events from presentation.");
        }
    }
    public sealed class ApplyAnotherStatusEffect : StatusTickEffect
    {
        public StatusDefinition Next;
        public override void Execute(in StatusTickContext context) => context.Controller.Apply(Next, context.Source);
    }
}
