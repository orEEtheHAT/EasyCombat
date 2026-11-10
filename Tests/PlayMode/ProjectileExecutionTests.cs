using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ProjectileExecutionTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 origin = new Vector3(1600, 1700, 1800);
        private AttackDefinition attack;
        private Health victim;

        [SetUp]
        public void SetUp()
        {
            attack = ScriptableObject.CreateInstance<AttackDefinition>();
            cleanup.Add(attack);
            attack.Delivery = DamageDelivery.Projectile;
            attack.ProjectileSpeed = 10;
            attack.ProjectileLifetime = 5;
            attack.Range = 50;
            attack.HitMask = 1 << 10;
            var target = ObjectAt(origin + Vector3.forward * 2);
            target.layer = 10;
            target.AddComponent<BoxCollider>();
            victim = target.AddComponent<Health>();
            Physics.SyncTransforms();
        }

        private GameObject ObjectAt(Vector3 position)
        {
            var go = new GameObject("Projectile execution test");
            cleanup.Add(go);
            go.transform.position = position;
            return go;
        }

        private DamageProjectile Launch(Vector3 at, Action<DamageImpact> impact = null)
        {
            var shot = ObjectAt(at).AddComponent<DamageProjectile>();
            shot.Launch(at, Vector3.forward, new DamageInfo(3), null, attack, impact, p => p.gameObject.SetActive(false));
            return shot;
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i]) Object.Destroy(cleanup[i]);
            cleanup.Clear();
            yield return null;
        }

        [TestCase(ProjectileExecutionMode.SingleThread)]
        [TestCase(ProjectileExecutionMode.Parallel)]
        public void VolleyPreservesOrderExpiryAndLaunchDuringImpact(ProjectileExecutionMode mode)
        {
            var hits = new List<int>();
            DamageProjectile spawned = null;
            var moving = Launch(origin + Vector3.right * 8);
            for (int i = 0; i < 8; i++)
            {
                int id = i;
                Launch(origin, _ =>
                {
                    hits.Add(id);
                    if (id == 0) spawned = Launch(origin);
                });
            }
            var scheduler = Object.FindFirstObjectByType<ProjectileBatchScheduler>();
            scheduler.Advance(.25f, mode);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, hits);
            Assert.AreEqual(76, victim.Current);
            Assert.AreEqual(origin, spawned.transform.position);
            Assert.AreEqual(origin + Vector3.right * 8 + Vector3.forward * 2.5f, moving.transform.position);
            scheduler.Advance(10, mode);
            Assert.AreEqual(73, victim.Current);
            Assert.IsFalse(moving.IsFlying);
            Assert.AreEqual(origin + Vector3.right * 8 + Vector3.forward * 50, moving.transform.position);
        }

        [TestCase(ProjectileExecutionMode.SingleThread)]
        [TestCase(ProjectileExecutionMode.Parallel)]
        public void EarlierCallbackCanMoveATargetIntoAPreviouslyEmptySweep(ProjectileExecutionMode mode)
        {
            var lane = origin + Vector3.right * 8;
            Launch(origin, _ =>
            {
                victim.transform.position = lane + Vector3.forward;
                Physics.SyncTransforms();
            });
            var later = Launch(lane);
            var scheduler = Object.FindFirstObjectByType<ProjectileBatchScheduler>();
            scheduler.Advance(.25f, mode);
            Assert.AreEqual(94, victim.Current);
            Assert.IsFalse(later.IsFlying);
        }

        [TestCase(ProjectileExecutionMode.SingleThread)]
        [TestCase(ProjectileExecutionMode.Parallel)]
        public void SplashAppliesIndependentStacksAndTicksOncePerReceiver(ProjectileExecutionMode mode)
        {
            var burn = ScriptableObject.CreateInstance<StatusDefinition>(); cleanup.Add(burn);
            var fire = ScriptableObject.CreateInstance<PeriodicDamageEffect>(); cleanup.Add(fire);
            var area = ScriptableObject.CreateInstance<AreaDefinition>(); cleanup.Add(area);
            area.Radius = 4; area.TargetMask = attack.HitMask;
            burn.Duration = 2; burn.TickInterval = .25f; fire.DamagePerStack = 1;
            burn.TickEffects = new StatusTickEffect[] { fire };
            attack.Damage = 3; attack.CriticalChance = 0; attack.Area = area;
            attack.OnHitStatuses = new[] { new StatusApplication(burn) };
            var other = ObjectAt(victim.transform.position + Vector3.right * 2);
            other.layer = 10; other.AddComponent<BoxCollider>(); other.AddComponent<SphereCollider>();
            var otherHealth = other.AddComponent<Health>();
            var statuses = victim.gameObject.AddComponent<StatusController>();
            var otherStatuses = other.AddComponent<StatusController>();
            Physics.SyncTransforms();
            for (int i = 0; i < 2; i++)
            {
                var shot = ObjectAt(origin).AddComponent<DamageProjectile>();
                var payload = attack.RollDamage(null, origin, Vector3.forward, 0, 1);
                shot.Launch(origin, Vector3.forward, payload, null, attack, returnToPool: p => p.gameObject.SetActive(false));
            }
            Object.FindFirstObjectByType<ProjectileBatchScheduler>().Advance(.3f, mode);
            Assert.AreEqual(94, victim.Current);
            Assert.AreEqual(94, otherHealth.Current);
            Assert.AreEqual(2, statuses.GetStacks(burn));
            Assert.AreEqual(2, otherStatuses.GetStacks(burn));
            statuses.Advance(.25f); otherStatuses.Advance(.25f);
            Assert.AreEqual(92, victim.Current);
            Assert.AreEqual(92, otherHealth.Current);
        }

        [TestCase(ProjectileExecutionMode.SingleThread)]
        [TestCase(ProjectileExecutionMode.Parallel)]
        public void TwoConsecutiveBatchesHandleInitialOverlapAndThenEmptySpace(ProjectileExecutionMode mode)
        {
            attack.ProjectileRadius = .3f;
            var schedulerShot = Launch(victim.transform.position);
            var scheduler = Object.FindFirstObjectByType<ProjectileBatchScheduler>();
            scheduler.Advance(.02f, mode);
            Assert.IsFalse(schedulerShot.IsFlying);
            Assert.AreEqual(97, victim.Current);
            var moving = Launch(origin + Vector3.right * 3);
            scheduler.Advance(.02f, mode);
            Assert.IsTrue(moving.IsFlying);
            Assert.That(moving.transform.position.z, Is.EqualTo(origin.z + .2f).Within(.001f));
        }

        [TestCase(ProjectileExecutionMode.SingleThread)]
        [TestCase(ProjectileExecutionMode.Parallel)]
        public void LeadingImpactCanRemoveAShotAndMoveATargetBeforeTailCapture(ProjectileExecutionMode mode)
        {
            var lane = origin + Vector3.right * 8;
            DamageProjectile removed = null, replacement = null;
            Launch(origin, _ =>
            {
                removed.Despawn();
                replacement = Launch(lane);
                victim.transform.position = lane + Vector3.forward;
                Physics.SyncTransforms();
            });
            var survivor = Launch(origin + Vector3.left * 10);
            removed = Launch(lane);
            var tail = Launch(lane);
            var scheduler = Object.FindFirstObjectByType<ProjectileBatchScheduler>();
            scheduler.Advance(.25f, mode);
            Assert.AreEqual(94, victim.Current);
            Assert.IsFalse(tail.IsFlying);
            Assert.AreEqual(lane, replacement.transform.position);
            Assert.AreEqual(origin + Vector3.left * 10 + Vector3.forward * 2.5f, survivor.transform.position);
            scheduler.Advance(.25f, mode);
            Assert.AreEqual(91, victim.Current);
        }

        [TestCase(ProjectileExecutionMode.SingleThread)]
        [TestCase(ProjectileExecutionMode.Parallel)]
        public void EarlierHitCanAdvanceAndRecycleLaterShots(ProjectileExecutionMode mode)
        {
            DamageProjectile next = null, recycled = null;
            Vector3 lane = origin + Vector3.right * 10;
            Launch(origin, _ =>
            {
                next.Simulate(.1f);
                recycled.Despawn();
                recycled.Launch(lane, Vector3.right, new DamageInfo(1), null, attack, null, p => p.gameObject.SetActive(false));
            });
            next = Launch(lane);
            recycled = Launch(lane);
            var scheduler = Object.FindFirstObjectByType<ProjectileBatchScheduler>();
            scheduler.Advance(.2f, mode);
            Assert.AreEqual(lane, recycled.transform.position);
            Assert.AreEqual(lane + Vector3.forward * 3, next.transform.position);
            scheduler.Advance(6, mode);
            Assert.AreEqual(lane + Vector3.forward * 50, next.transform.position);
            Assert.AreEqual(lane + Vector3.right * 50, recycled.transform.position);
        }
    }
}
