using NUnit.Framework;
using UnityEngine;

namespace EasyCombat.Tests
{
    public sealed class HealthTests
    {
        private GameObject root;
        private Health health;
        [SetUp] public void SetUp() { root = new GameObject("Health test"); health = root.AddComponent<Health>(); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);

        [Test] public void OverkillClampsAndDeathIsEmittedOnce()
        {
            var deaths = 0;
            health.Died += _ => deaths++;
            var result = health.ApplyDamage(new DamageInfo(250));
            Assert.That(result.AppliedAmount, Is.EqualTo(100));
            Assert.That(result.Killed, Is.True);
            Assert.That(health.Current, Is.Zero);
            Assert.That(health.ApplyDamage(new DamageInfo(1)).Rejection, Is.EqualTo(DamageRejection.Dead));
            Assert.That(deaths, Is.EqualTo(1));
        }
        [Test] public void HealingClampsAndDoesNotRevive()
        {
            health.ApplyDamage(new DamageInfo(30));
            Assert.That(health.Heal(1000), Is.EqualTo(30));
            Assert.That(health.Current, Is.EqualTo(100));
            health.ApplyDamage(new DamageInfo(100));
            Assert.That(health.Heal(100), Is.Zero);
            Assert.That(health.IsAlive, Is.False);
        }
        [Test] public void InvalidDamageAndHealingLeaveStateUntouched()
        {
            foreach (var amount in new[] { 0f, -10f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.That(health.ApplyDamage(new DamageInfo(amount)).Rejection, Is.EqualTo(DamageRejection.InvalidAmount));
                Assert.That(health.Heal(amount), Is.Zero);
            }
            Assert.That(health.Current, Is.EqualTo(100));
        }
        [Test] public void ExplicitResetStartsANewLifeWithoutResettingOnEnable()
        {
            var deaths = 0;
            var revivals = 0;
            health.Died += _ => deaths++;
            health.Revived += _ => revivals++;
            health.ApplyDamage(new DamageInfo(100));
            root.SetActive(false);
            root.SetActive(true);
            Assert.That(health.Current, Is.Zero);
            health.ResetToFull();
            health.ApplyDamage(new DamageInfo(100));
            Assert.That(deaths, Is.EqualTo(2));
            Assert.That(revivals, Is.EqualTo(1));
        }
        [Test] public void DisabledHealthRejectsDamage()
        {
            health.enabled = false;
            Assert.That(health.ApplyDamage(new DamageInfo(25)).Rejection, Is.EqualTo(DamageRejection.Disabled));
            Assert.That(health.Current, Is.EqualTo(100));
        }
        [Test] public void SnapshotRoundTripRestoresHealthWithoutDeathReward()
        {
            health.ApplyDamage(new DamageInfo(35));
            var snapshot = health.CaptureState();
            health.ResetToFull();
            Assert.That(health.RestoreState(snapshot), Is.True);
            Assert.That(health.Current, Is.EqualTo(65));
            var deaths = 0;
            health.Died += _ => deaths++;
            snapshot.Current = 0;
            Assert.That(health.RestoreState(snapshot), Is.True);
            Assert.That(health.IsAlive, Is.False);
            Assert.That(deaths, Is.Zero);
        }
        [Test] public void InvalidSnapshotsAreRejectedAtomically()
        {
            health.ApplyDamage(new DamageInfo(10));
            Assert.That(health.RestoreState(new HealthSnapshot(200, float.NaN)), Is.False);
            Assert.That(health.RestoreState(new HealthSnapshot(-20, 5)), Is.False);
            var future = new HealthSnapshot(200, 30) { Version = 999 };
            Assert.That(health.RestoreState(future), Is.False);
            Assert.That(health.Maximum, Is.EqualTo(100));
            Assert.That(health.Current, Is.EqualTo(90));
        }
        [Test] public void SnapshotClampsCurrentToItsMaximum()
        {
            health.RestoreState(new HealthSnapshot(50, 300));
            Assert.That(health.Current, Is.EqualTo(50));
            health.RestoreState(new HealthSnapshot(50, -5));
            Assert.That(health.Current, Is.Zero);
        }
        [Test] public void MaximumChangeCanPreserveRatioOrAbsoluteValue()
        {
            health.ApplyDamage(new DamageInfo(25));
            health.SetMaximum(200);
            Assert.That(health.Current, Is.EqualTo(150));
            health.SetMaximum(160, false);
            Assert.That(health.Current, Is.EqualTo(150));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => health.SetMaximum(float.NaN));
        }
        [Test] public void HurtboxMultipliesDamageAndPreservesSource()
        {
            var part = new GameObject("Weak point");
            part.transform.SetParent(root.transform);
            var hurtbox = part.AddComponent<Hurtbox>();
            hurtbox.Configure(health, 2f);
            var result = hurtbox.ApplyDamage(new DamageInfo(20, root, Vector3.one, Vector3.forward));
            Assert.That(health.Current, Is.EqualTo(60));
            Assert.That(result.Damage.Source, Is.SameAs(root));
            Assert.That(result.Damage.Point, Is.EqualTo(Vector3.one));
        }
        [Test] public void ZeroMultiplierDoesNotHealOrDamage()
        {
            var hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.Configure(health, 0f);
            Assert.That(hurtbox.ApplyDamage(new DamageInfo(25)).Applied, Is.False);
            Assert.That(health.Current, Is.EqualTo(100));
        }
        [Test] public void SameObjectHurtboxTakesPrecedenceOverHealth()
        {
            var collider = root.AddComponent<BoxCollider>();
            var hurtbox = root.AddComponent<Hurtbox>();
            hurtbox.Configure(health, 2f);
            DamageQuery.Apply(collider, Vector3.zero, Vector3.back, new DamageInfo(10));
            Assert.That(health.Current, Is.EqualTo(80));
        }
    }
}
