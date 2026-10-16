using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class WideAttackTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 origin = new Vector3(0, 1500, 0);
        private AttackDefinition attack;
        private DamageEmitter emitter;
        private Transform owner;
        private DamageProjectile projectile;
        private readonly List<DamageImpact> impacts = new List<DamageImpact>();
        private ShotInfo lastShot;

        [SetUp] public void Setup()
        {
            owner = Box("Owner", origin, Vector3.one).transform;
            emitter = owner.gameObject.AddComponent<DamageEmitter>();
            attack = Asset<AttackDefinition>();
            attack.RoundsPerMinute = 0;
            attack.Range = 20;
            attack.ProjectileSpeed = 20;
            attack.ProjectileLifetime = 10;
            attack.HitscanRadius = attack.ProjectileRadius = .6f;
            emitter.Attack = attack;
            emitter.Impacted += impacts.Add;
            emitter.Fired += shot => lastShot = shot;
            var go = new GameObject("Projectile"); cleanup.Add(go);
            projectile = go.AddComponent<DamageProjectile>();
            go.SetActive(false);
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]);
            cleanup.Clear(); impacts.Clear();
            yield return null;
        }

        private T Asset<T>() where T : ScriptableObject
        { var asset = ScriptableObject.CreateInstance<T>(); cleanup.Add(asset); return asset; }
        private GameObject Box(string label, Vector3 position, Vector3 size)
        {
            var go = new GameObject(label); cleanup.Add(go);
            go.transform.position = position; go.AddComponent<BoxCollider>().size = size; return go;
        }
        private Health Target(float z, float x = 0)
            => Box("Target " + z, origin + new Vector3(x, 0, z), Vector3.one).AddComponent<Health>();
        private void Fire(DamageDelivery delivery, float deltaTime = 1)
        {
            attack.Delivery = delivery;
            Physics.SyncTransforms();
            if (delivery == DamageDelivery.Hitscan) Assert.That(emitter.TryFire(origin, Vector3.forward), Is.True);
            else
            {
                projectile.Launch(origin, Vector3.forward, attack.RollDamage(owner.gameObject, origin, Vector3.forward, 0, 1),
                    owner, attack, impacts.Add, p => p.gameObject.SetActive(false));
                projectile.Simulate(deltaTime);
            }
        }
        private void Pierce(int maximum = 0)
        { attack.ContactBehavior = ContactBehavior.PierceTargets; attack.MaximumTargets = maximum; }

        [Test] public void PrecollectedContactCannotHitNewPooledLife()
        {
            var near=Target(3); var far=Target(8); near.Damaged+=_=>far.ResetToFull();
            Pierce(); Fire(DamageDelivery.Hitscan);
            Assert.That(near.Current,Is.EqualTo(75)); Assert.That(far.Current,Is.EqualTo(100));
        }
        [Test] public void LongLivedProjectileMayHitARespawnedLifeOnANewSweep()
        {
            var target=Target(3); Pierce(); attack.Delivery=DamageDelivery.Projectile;
            Physics.SyncTransforms();
            projectile.Launch(origin,Vector3.forward,attack.RollDamage(owner.gameObject,origin,Vector3.forward,0,1),owner,attack,impacts.Add,p=>p.gameObject.SetActive(false));
            projectile.Simulate(.2f); Assert.That(target.Current,Is.EqualTo(75));
            target.ResetToFull(); target.transform.position=origin+Vector3.forward*7; Physics.SyncTransforms(); projectile.Simulate(.2f);
            Assert.That(target.Current,Is.EqualTo(75)); Assert.That(impacts.Count,Is.EqualTo(2));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void WideVolumeHitsOffAxisTarget(DamageDelivery delivery)
        {
            var side = Target(5, 1);
            Fire(delivery);
            Assert.That(side.Current, Is.EqualTo(75));
            Assert.That(impacts.Count, Is.EqualTo(1));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void ZeroRadiusMissesOffAxisTarget(DamageDelivery delivery)
        {
            var side = Target(5, 1);
            attack.HitscanRadius = attack.ProjectileRadius = 0;
            Fire(delivery);
            Assert.That(side.Current, Is.EqualTo(100));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void PiercingResolvesNearToFarAndHonorsTotalTargetLimit(DamageDelivery delivery)
        {
            var far = Target(9); var middle = Target(6); var near = Target(3);
            Pierce(2); Fire(delivery);
            Assert.That(near.Current, Is.EqualTo(75)); Assert.That(middle.Current, Is.EqualTo(75));
            Assert.That(far.Current, Is.EqualTo(100));
            Assert.That(impacts[0].Collider.gameObject, Is.EqualTo(near.gameObject));
            Assert.That(impacts[1].Collider.gameObject, Is.EqualTo(middle.gameObject));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void WallStopsPiercingEvenWhenClippedByVolumeEdge(DamageDelivery delivery)
        {
            var near = Target(3); var far = Target(9);
            Box("Wall edge", origin + new Vector3(1, 0, 6), new Vector3(1, 1, .1f));
            Pierce(); Fire(delivery);
            Assert.That(near.Current, Is.EqualTo(75)); Assert.That(far.Current, Is.EqualTo(100));
            Assert.That(impacts.Count, Is.EqualTo(2));
            Assert.That(impacts[1].Result.Rejection, Is.EqualTo(DamageRejection.NoReceiver));
            if (delivery == DamageDelivery.Hitscan)
            {
                Assert.That(lastShot.End.z, Is.LessThan(6));
                Assert.That(lastShot.End.x, Is.Zero.Within(.0001f));
                Assert.That(lastShot.Radius, Is.EqualTo(.6f));
            }
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void MultipleHurtboxesUseFirstContactAndApplyStatusOnce(DamageDelivery delivery)
        {
            var near = Target(4); var far = Target(8);
            var status = Asset<StatusDefinition>();
            var statuses = near.gameObject.AddComponent<StatusController>();
            attack.OnHitStatuses = new[] { new StatusApplication(status) };
            var front = Box("Front weakpoint", origin + Vector3.forward * 3, Vector3.one * .5f);
            front.transform.SetParent(near.transform, true);
            front.AddComponent<Hurtbox>().Configure(near, 2);
            var back = Box("Back weakpoint", origin + Vector3.forward * 4.5f, Vector3.one * .5f);
            back.transform.SetParent(near.transform, true);
            back.AddComponent<Hurtbox>().Configure(near, 3);
            Pierce(2); Fire(delivery);
            Assert.That(near.Current, Is.EqualTo(50)); Assert.That(far.Current, Is.EqualTo(75));
            Assert.That(statuses.GetStacks(status), Is.EqualTo(1));
            Assert.That(impacts.Count, Is.EqualTo(2));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void InitialOverlapIsHitOnceThenContinues(DamageDelivery delivery)
        {
            var initial = Target(0); var next = Target(5);
            Pierce(); Fire(delivery);
            Assert.That(initial.Current, Is.EqualTo(75)); Assert.That(next.Current, Is.EqualTo(75));
            Assert.That(impacts.Count, Is.EqualTo(2));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void WallWinsAtOverlappingMuzzle(DamageDelivery delivery)
        {
            var initial = Target(0); var next = Target(5);
            Box("Muzzle wall", origin, Vector3.one);
            Pierce(); Fire(delivery);
            Assert.That(initial.Current, Is.EqualTo(100)); Assert.That(next.Current, Is.EqualTo(100));
            Assert.That(impacts.Count, Is.EqualTo(1));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void ImmunityStillConsumesTargetBudget(DamageDelivery delivery)
        {
            var immune = Target(3); var far = Target(7);
            var type = Asset<DamageTypeDefinition>();
            var resistance = Asset<ResistanceProfile>(); resistance.Configure(new DamageResistance(type, 1));
            var profile = Asset<HealthDefinition>(); profile.Resistances = resistance;
            immune.ApplyDefinition(profile); attack.DamageType = type;
            Pierce(1); Fire(delivery);
            Assert.That(impacts.Count, Is.EqualTo(1));
            Assert.That(impacts[0].Result.Rejection, Is.EqualTo(DamageRejection.Immune));
            Assert.That(far.Current, Is.EqualTo(100));
        }

        [Test] public void SlowSphereNeverRepeatsDamageWhileInsideTargetAndCanHitNext()
        {
            var near = Target(2); var next = Target(6);
            Pierce(); attack.ProjectileSpeed = 1;
            Fire(DamageDelivery.Projectile, 1);
            for (var i = 0; i < 60; i++) projectile.Simulate(.1f);
            Assert.That(near.Current, Is.EqualTo(75)); Assert.That(next.Current, Is.EqualTo(75));
            Assert.That(impacts.Count, Is.EqualTo(2));
            Assert.That(projectile.IsFlying, Is.True);
        }

        [Test] public void InFlightProjectileSnapshotsRadiusContactPolicyAndLimit()
        {
            var near = Target(3, 1); var far = Target(7, 1);
            Pierce(2); attack.ProjectileSpeed = 1;
            Fire(DamageDelivery.Projectile, .1f);
            attack.ContactBehavior = ContactBehavior.StopOnFirstHit;
            attack.MaximumTargets = 1; attack.ProjectileRadius = 0;
            projectile.Simulate(9);
            Assert.That(near.Current, Is.EqualTo(75)); Assert.That(far.Current, Is.EqualTo(75));
            Assert.That(projectile.IsFlying, Is.False);
        }

        [Test] public void PoolReuseClearsVisitedTargets()
        {
            var target = Target(4); Pierce(1); attack.Delivery = DamageDelivery.Projectile;
            var poolObject = new GameObject("Pool"); cleanup.Add(poolObject);
            var pool = poolObject.AddComponent<ProjectilePool>(); pool.Prefab = projectile;
            Physics.SyncTransforms();
            var first = pool.Spawn(origin, Vector3.forward, new DamageInfo(10), owner, attack, null);
            first.Simulate(1);
            var second = pool.Spawn(origin, Vector3.forward, new DamageInfo(10), owner, attack, null);
            Assert.That(second, Is.SameAs(first)); second.Simulate(1);
            Assert.That(target.Current, Is.EqualTo(80)); Assert.That(pool.ActiveCount, Is.Zero);
        }

        [Test] public void LargeQueryDoesNotTruncateTargetsOrOwnerColliders()
        {
            var targets = new List<Health>();
            for (var i = 0; i < 40; i++)
            {
                var child = Box("Owner child", origin + Vector3.forward * (2 + i), Vector3.one * .2f);
                child.transform.SetParent(owner, true);
                targets.Add(Target(2 + i));
            }
            Pierce(); attack.Range = 50; Fire(DamageDelivery.Hitscan);
            Assert.That(impacts.Count, Is.EqualTo(40));
            foreach (var target in targets) Assert.That(target.Current, Is.EqualTo(75));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void LayerAndTriggerFiltersAlsoApplyToPiercing(DamageDelivery delivery)
        {
            var trigger = Target(2); trigger.GetComponent<Collider>().isTrigger = true;
            var excluded = Target(4); excluded.gameObject.layer = 2;
            var last = Target(7); attack.HitMask = ~(1 << 2); Pierce();
            Fire(delivery);
            Assert.That(trigger.Current, Is.EqualTo(100)); Assert.That(excluded.Current, Is.EqualTo(100));
            Assert.That(last.Current, Is.EqualTo(75));
        }

        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void PiercingExplodesOncePerContactWithoutExtraDirectDamageOrEndBlast(DamageDelivery delivery)
        {
            attack.HitscanRadius = attack.ProjectileRadius = 0;
            var first = Target(3); var second = Target(6);
            var extraCollider = Box("Duplicate hurtbox", origin + Vector3.forward * 3.2f, Vector3.one * .5f);
            extraCollider.transform.SetParent(first.transform, true);
            extraCollider.AddComponent<Hurtbox>().Configure(first);
            var area = Asset<AreaDefinition>(); area.Radius = 4; area.Targets = AreaTargetFilter.All;
            area.RequireLineOfSight = false; area.DistanceFalloff = false;
            attack.Area = area; Pierce(); Fire(delivery);
            Assert.That(first.Current, Is.EqualTo(50)); Assert.That(second.Current, Is.EqualTo(50));
            Assert.That(impacts.Count, Is.EqualTo(4), "Two separate explosions, each hits both targets once.");
        }

        [Test] public void CallbackCannotReenterHitscanTrace()
        {
            var near = Target(3); var far = Target(6); Pierce();
            emitter.Impacted += _ => Assert.That(emitter.TryFire(origin, Vector3.forward), Is.False);
            Fire(DamageDelivery.Hitscan);
            Assert.That(near.Current, Is.EqualTo(75)); Assert.That(far.Current, Is.EqualTo(75));
        }

        [Test] public void ProjectileCancellationFromCallbackStopsFurtherTargetsAndReleasesOnce()
        {
            var near = Target(3); var far = Target(6); Pierce(); attack.Delivery = DamageDelivery.Projectile;
            var releases = 0; Physics.SyncTransforms();
            projectile.Launch(origin, Vector3.forward, new DamageInfo(25), owner, attack, _ =>
            {
                projectile.Simulate(1); // Recursive simulation is ignored.
                projectile.Despawn();
                Assert.That(releases, Is.Zero, "Pool return must wait until callbacks unwind.");
            }, p => { releases++; p.gameObject.SetActive(false); });
            projectile.Simulate(1);
            Assert.That(near.Current, Is.EqualTo(75)); Assert.That(far.Current, Is.EqualTo(100));
            Assert.That(releases, Is.EqualTo(1)); Assert.That(projectile.IsFlying, Is.False);
        }

        [Test] public void InvalidRadiusAndTargetLimitRejectBeforeDamage()
        {
            var target = Target(4); attack.HitscanRadius = float.NaN;
            Assert.That(emitter.TryFire(origin, Vector3.forward), Is.False);
            attack.HitscanRadius = .5f; attack.MaximumTargets = -1;
            Assert.That(emitter.TryFire(origin, Vector3.forward), Is.False);
            Assert.That(target.Current, Is.EqualTo(100));
        }

        [Test] public void WarmedPiercingHitscanHasNoManagedAllocationInCore()
        {
            var near = Target(3); var far = Target(6); Pierce();
            emitter.Impacted -= impacts.Add;
            Physics.SyncTransforms();
            for (var i = 0; i < 30; i++) { near.ResetToFull(); far.ResetToFull(); emitter.TryFire(origin, Vector3.forward); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100; i++) { near.ResetToFull(); far.ResetToFull(); emitter.TryFire(origin, Vector3.forward); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Only warmed core, two receivers, no UI subscribers.");
        }
    }
}
