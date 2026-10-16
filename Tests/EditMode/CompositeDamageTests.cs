using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class CompositeDamageTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health health;
        private StatusController statuses;
        private CombatJournal journal;
        private DamageTypeDefinition fire, poison;
        private AttackDefinition weapon;
        private ResistanceProfile resistances;
        private HealthDefinition profile;
        private T Asset<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); cleanup.Add(item); return item; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        [SetUp] public void Setup()
        {
            var target = Actor("Target"); health = target.AddComponent<Health>(); statuses = target.AddComponent<StatusController>();
            journal = Actor("Journal").AddComponent<CombatJournal>();
            journal.StartRecording(); // EditMode does not run ordinary MonoBehaviour lifecycle callbacks.
            fire = Asset<DamageTypeDefinition>(); fire.DisplayName = "Fire";
            poison = Asset<DamageTypeDefinition>(); poison.DisplayName = "Poison";
            resistances = Asset<ResistanceProfile>();
            profile = Asset<HealthDefinition>(); profile.Resistances = resistances; health.ApplyDefinition(profile);
            weapon = Asset<AttackDefinition>(); weapon.DisplayName = "Mixed weapon";
            weapon.DamageChannels = new[] { new DamageChannel(fire, 2, 4), new DamageChannel(poison, 7, 9) };
        }
        [TearDown] public void Teardown()
        { journal.StopRecording(); for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private DamageInfo Roll(float sample = .5f) => weapon.RollDamage(null, default, Vector3.forward, sample, 1);
        private StatusDefinition Burn()
        {
            var tick = Asset<PeriodicDamageEffect>(); tick.DamageType = fire; tick.DamagePerStack = 3;
            var status = Asset<StatusDefinition>(); status.DisplayName = "Burn"; status.Duration = 2; status.TickInterval = 1;
            status.Stacking = StatusStacking.StackAndRefresh; status.TickEffects = new StatusTickEffect[] { tick };
            return status;
        }
        [Test] public void FireTwoToFourAndPoisonSevenToNineUseIndependentSamplesAndResists()
        {
            resistances.Configure(new DamageResistance(fire, .5f), new DamageResistance(poison, .25f));
            var shot = weapon.RollDamage(null, default, default, new float[] { 0, 1 }, 1);
            Assert.That(shot.Parts[0].Amount, Is.EqualTo(2)); Assert.That(shot.Parts[1].Amount, Is.EqualTo(9));
            var result = health.ApplyDamage(shot);
            Assert.That(result.Breakdown[0].Applied, Is.EqualTo(1));
            Assert.That(result.Breakdown[1].Applied, Is.EqualTo(6.75f));
            Assert.That(result.AppliedAmount, Is.EqualTo(7.75f)); Assert.That(journal.Count, Is.EqualTo(1));
        }
        [Test] public void MultipleResistsCombineWithStatusModifierForOnlyTheirOwnType()
        {
            resistances.Configure(new DamageResistance(fire, .5f), new DamageResistance(poison, .25f));
            var ward = Asset<StatusDefinition>(); ward.ResistanceModifiers = new[] { new DamageResistance(fire, .25f) };
            statuses.Apply(ward);
            var result = health.ApplyDamage(Roll());
            Assert.That(result.Breakdown[0].Applied, Is.EqualTo(.75f)); Assert.That(result.Breakdown[1].Applied, Is.EqualTo(6));
        }
        [Test] public void ImmunityToFireDoesNotBlockPoisonOrDuplicateOnHitStatuses()
        {
            resistances.Configure(new DamageResistance(fire, 1), new DamageResistance(poison, .25f));
            var burn = Burn(); weapon.OnHitStatuses = new[] { new StatusApplication(burn) };
            var result = health.ApplyDamage(Roll());
            Assert.That(result.AppliedAmount, Is.EqualTo(6)); Assert.That(result.Breakdown[0].Applied, Is.Zero);
            Assert.That(statuses.GetStacks(burn), Is.EqualTo(1)); Assert.That(result.StatusesApplied, Is.EqualTo(1));
        }
        [Test] public void CriticalAndHurtboxScaleAllChannelsBeforeTheirResists()
        {
            resistances.Configure(new DamageResistance(fire, .5f), new DamageResistance(poison, .25f));
            weapon.CriticalChance = 1;
            var zone = health.gameObject.AddComponent<Hurtbox>(); zone.Configure(health, 2);
            Assert.That(zone.ApplyDamage(Roll()).AppliedAmount, Is.EqualTo(30));
        }
        [Test] public void OverkillIsProportionalAndDeathOccursOnceForWholePacket()
        {
            health.SetMaximum(5); health.ResetToFull(); var deaths = 0; health.Died += _ => deaths++;
            var result = health.ApplyDamage(Roll());
            Assert.That(result.EffectiveAmount, Is.EqualTo(11)); Assert.That(result.AppliedAmount, Is.EqualTo(5));
            Assert.That(result.Overkill, Is.EqualTo(6)); Assert.That(deaths, Is.EqualTo(1));
            Assert.That(result.Breakdown[0].Applied + result.Breakdown[1].Applied, Is.EqualTo(5).Within(.00001f));
            Assert.That(result.Breakdown[0].Applied, Is.EqualTo(5 * 3f / 11).Within(.00001f));
        }
        [Test] public void InvalidChannelRejectsWholePacketWithoutPartialDamage()
        {
            var amounts = new DamageAmounts(); amounts.Add(fire, 5); amounts.Add(poison, float.NaN);
            Assert.That(health.ApplyDamage(new DamageInfo(amounts)).Rejection, Is.EqualTo(DamageRejection.InvalidAmount));
            Assert.That(health.Current, Is.EqualTo(100));
        }
        [Test] public void ChannelsAreValueSnapshotsAndOverCapacityIsRejected()
        {
            var shot = Roll(); weapon.DamageChannels[0] = new DamageChannel(poison, 99, 99);
            Assert.That(shot.Parts[0].Type, Is.EqualTo(fire)); Assert.That(shot.Parts[0].Amount, Is.EqualTo(3));
            weapon.DamageChannels = new DamageChannel[9]; Assert.That(weapon.IsValid, Is.False);
        }
        [Test] public void StatusOnlyWorksWithoutHpLossAndHonorsSeparateStatusImmunity()
        {
            var slow = Asset<StatusDefinition>(); weapon.StatusOnly = true;
            weapon.OnHitStatuses = new[] { new StatusApplication(slow) };
            var result = health.ApplyDamage(Roll());
            Assert.That(result.StatusesApplied, Is.EqualTo(1)); Assert.That(health.Current, Is.EqualTo(100));
            statuses.Clear(); profile.StatusImmunities = new[] { slow };
            var rejected = health.ApplyDamage(Roll());
            Assert.That(rejected.StatusesApplied, Is.Zero); Assert.That(rejected.Rejection, Is.EqualTo(DamageRejection.NoEffect));
            Assert.That(statuses.Count, Is.Zero);
        }
        [Test] public void ContactPolicyCanApplyStatusDespiteDamageImmunity()
        {
            resistances.Configure(new DamageResistance(fire, 1), new DamageResistance(poison, 1));
            weapon.StatusPolicy = StatusApplyPolicy.OnContact; weapon.OnHitStatuses = new[] { new StatusApplication(Burn()) };
            var result = health.ApplyDamage(Roll());
            Assert.That(result.Rejection, Is.EqualTo(DamageRejection.Immune)); Assert.That(result.StatusesApplied, Is.EqualTo(1));
            Assert.That(journal.GetNewest(0).Result.Rejection, Is.EqualTo(DamageRejection.Immune));
        }
        [Test] public void JournalKeepsAttributionAfterSourceDestroyedAndDuringPeriodicDamage()
        {
            var source = Actor("Shooter"); source.AddComponent<Health>();
            weapon.OnHitStatuses = new[] { new StatusApplication(Burn()) };
            var shot = weapon.RollDamage(source, default, default, .5f, 1); health.ApplyDamage(shot);
            Object.DestroyImmediate(source); statuses.Advance(1);
            var tick = journal.GetNewest(0);
            Assert.That(tick.Result.Damage.IsPeriodic, Is.True);
            Assert.That(tick.Result.Damage.Origin.Actor.Name, Is.EqualTo("Shooter"));
            Assert.That(tick.Result.Damage.Origin.AttackName, Is.EqualTo("Mixed weapon"));
            Assert.That(tick.Result.Damage.Origin.StatusName, Is.EqualTo("Burn"));
            Assert.That(tick.Result.Damage.Origin.CastId, Is.EqualTo(shot.Origin.CastId));
        }
        [Test] public void JournalRecordsRejectionsAndRingBufferRemainsBounded()
        {
            journal.Configure(3); var shot = Roll();
            for (var i = 0; i < 5; i++) health.ApplyDamage(shot);
            Assert.That(journal.Count, Is.EqualTo(3));
            Assert.That(journal.GetNewest(0).Sequence, Is.GreaterThan(journal.GetNewest(2).Sequence));
            health.enabled = false; health.ApplyDamage(shot);
            Assert.That(journal.GetNewest(0).Result.Rejection, Is.EqualTo(DamageRejection.Disabled));
            journal.Clear(); Assert.That(journal.Count, Is.Zero);
        }
        [Test] public void ReusedTargetGetsNewLifeIdentityWhileHistoryRetainsOldIdentity()
        {
            health.ApplyDamage(Roll()); var old = journal.GetNewest(0).Target;
            health.ResetToFull(); Assert.That(health.Identity.Id, Is.Not.EqualTo(old.Id));
            Assert.That(journal.GetNewest(0).Target.Id, Is.EqualTo(old.Id));
        }
        [Test] public void WarmMixedDamageWithJournalDoesNotAllocateManagedMemory()
        {
            health.SetMaximum(100000); health.ResetToFull();
            resistances.Configure(new DamageResistance(fire, .5f), new DamageResistance(poison, .25f));
            var shot = Roll(); for (var i = 0; i < 32; i++) health.ApplyDamage(shot);
            Assert.That(journal.Count, Is.GreaterThan(0));
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; i++) health.ApplyDamage(shot);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero, "Excludes UI formatting and cold allocation.");
        }
        [Test] public void BrokenJournalObserverCannotCancelDeathNotification()
        {
            var deaths = 0; health.Died += _ => deaths++;
            Action<CombatRecord> broken = _ => throw new InvalidOperationException("Observer failure");
            CombatEvents.DamageResolved += broken;
            try
            {
                LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: Observer failure"));
                health.ApplyDamage(new DamageInfo(200));
                Assert.That(deaths, Is.EqualTo(1)); Assert.That(journal.Count, Is.EqualTo(1));
            }
            finally { CombatEvents.DamageResolved -= broken; }
        }
    }
}
