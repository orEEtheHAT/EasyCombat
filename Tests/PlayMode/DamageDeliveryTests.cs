using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EasyCombat.Tests
{
    public sealed class DamageDeliveryTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 origin = new Vector3(0, 1000, 0);
        private AttackDefinition attack;
        private DamageEmitter weapon;
        private GameObject owner;
        [SetUp] public void SetUp()
        {
            owner = Box("Owner", origin, Vector3.one);
            owner.AddComponent<Health>();
            weapon = owner.AddComponent<DamageEmitter>();
            weapon.OwnerRoot = owner.transform;
            attack = ScriptableObject.CreateInstance<AttackDefinition>();
            cleanup.Add(attack);
            attack.Cooldown = 0f;
            weapon.Attack = attack;
        }
        private GameObject Box(string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            cleanup.Add(go);
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = size;
            return go;
        }
        private Health Target(float distance, float thickness = 1f)
        {
            return Box("Target", origin + Vector3.forward * distance, new Vector3(1, 1, thickness)).AddComponent<Health>();
        }
        private DamageProjectile NewProjectile()
        {
            var go = new GameObject("Projectile");
            cleanup.Add(go);
            return go.AddComponent<DamageProjectile>();
        }
        private T Asset<T>() where T : ScriptableObject
        {
            var item = ScriptableObject.CreateInstance<T>();
            cleanup.Add(item);
            return item;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            for (var i = cleanup.Count - 1; i >= 0; i--)
                if (cleanup[i] != null) Object.Destroy(cleanup[i]);
            cleanup.Clear();
            yield return null;
        }
        [UnityTest] public IEnumerator HitscanSkipsOwnerAndDamagesOnlyNearestTarget()
        {
            var near = Target(5);
            var far = Target(8);
            Physics.SyncTransforms();
            Assert.That(weapon.TryFire(origin, Vector3.forward), Is.True);
            Assert.That(owner.GetComponent<Health>().Current, Is.EqualTo(100));
            Assert.That(near.Current, Is.EqualTo(75));
            Assert.That(far.Current, Is.EqualTo(100));
            yield return null;
        }
        [UnityTest] public IEnumerator WallStopsHitscanBeforeDamageableTarget()
        {
            var target = Target(5);
            Box("Wall", origin + Vector3.forward * 2f, Vector3.one);
            Physics.SyncTransforms();
            var impacts = 0;
            weapon.Impacted += impact => { impacts++; Assert.That(impact.Result.Rejection, Is.EqualTo(DamageRejection.NoReceiver)); };
            weapon.TryFire(origin, Vector3.forward);
            Assert.That(target.Current, Is.EqualTo(100));
            Assert.That(impacts, Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator MultipleHurtboxesOnOneTargetApplyOnlyOneHit()
        {
            var target = Target(5);
            var weak = Box("Weak", origin + Vector3.forward * 4, Vector3.one * 0.5f);
            weak.transform.SetParent(target.transform, true);
            weak.AddComponent<Hurtbox>().Configure(target, 2);
            Physics.SyncTransforms();
            weapon.TryFire(origin, Vector3.forward);
            Assert.That(target.Current, Is.EqualTo(50));
            yield return null;
        }
        [UnityTest] public IEnumerator HitscanHonorsLayerMaskAndTriggerPolicy()
        {
            var trigger = Target(2);
            trigger.GetComponent<Collider>().isTrigger = true;
            var excluded = Target(4);
            excluded.gameObject.layer = 2;
            var final = Target(6);
            attack.HitMask = ~(1 << 2);
            Physics.SyncTransforms();
            weapon.TryFire(origin, Vector3.forward);
            Assert.That(trigger.Current, Is.EqualTo(100));
            Assert.That(excluded.Current, Is.EqualTo(100));
            Assert.That(final.Current, Is.EqualTo(75));
            attack.Triggers = QueryTriggerInteraction.Collide;
            weapon.TryFire(origin, Vector3.forward);
            Assert.That(trigger.Current, Is.EqualTo(75));
            yield return null;
        }
        [UnityTest] public IEnumerator QueryGrowsPastSixteenIgnoredOwnerColliders()
        {
            for (var i = 0; i < 24; i++)
            {
                var child = Box("Owner part", origin + Vector3.forward * (1 + i * 0.2f), Vector3.one * 0.1f);
                child.transform.SetParent(owner.transform, true);
            }
            var target = Target(9);
            Physics.SyncTransforms();
            weapon.TryFire(origin, Vector3.forward);
            Assert.That(target.Current, Is.EqualTo(75));
            yield return null;
        }
        [UnityTest] public IEnumerator FastProjectileSweepsThinTargetEvenWithoutImpactSubscriber()
        {
            var target = Target(5, 0.02f);
            attack.Delivery = DamageDelivery.Projectile;
            attack.ProjectileSpeed = 1000;
            var projectile = NewProjectile();
            Physics.SyncTransforms();
            projectile.Launch(origin, Vector3.forward, new DamageInfo(25), owner.transform, attack);
            projectile.Simulate(0.02f);
            projectile.Simulate(0.02f);
            Assert.That(target.Current, Is.EqualTo(75));
            Assert.That(projectile.IsFlying, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator ProjectileStopsAtWall()
        {
            var target = Target(5);
            Box("Thin wall", origin + Vector3.forward * 2, new Vector3(2, 2, 0.02f));
            attack.Delivery = DamageDelivery.Projectile;
            attack.ProjectileSpeed = 1000;
            var projectile = NewProjectile();
            Physics.SyncTransforms();
            projectile.Launch(origin, Vector3.forward, new DamageInfo(25), owner.transform, attack);
            projectile.Simulate(0.02f);
            Assert.That(target.Current, Is.EqualTo(100));
            Assert.That(projectile.IsFlying, Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator ProjectileDetectsInitialOverlap()
        {
            var target = Target(0);
            attack.Delivery = DamageDelivery.Projectile;
            var projectile = NewProjectile();
            Physics.SyncTransforms();
            projectile.Launch(origin, Vector3.forward, new DamageInfo(25), owner.transform, attack);
            projectile.Simulate(0.02f);
            Assert.That(target.Current, Is.EqualTo(75));
            yield return null;
        }
        [UnityTest] public IEnumerator ProjectileCannotTravelPastRangeOrLifetime()
        {
            var target = Target(5);
            attack.Delivery = DamageDelivery.Projectile;
            attack.Range = 2;
            attack.ProjectileSpeed = 100;
            var projectile = NewProjectile();
            Physics.SyncTransforms();
            projectile.Launch(origin, Vector3.forward, new DamageInfo(25), owner.transform, attack);
            projectile.Simulate(1);
            Assert.That(projectile.transform.position.z, Is.EqualTo(2).Within(0.001f));
            Assert.That(projectile.IsFlying, Is.False);
            attack.Range = 40;
            attack.ProjectileLifetime = 0.01f;
            var shortLived = NewProjectile();
            shortLived.Launch(origin, Vector3.forward, new DamageInfo(25), owner.transform, attack);
            shortLived.Simulate(1);
            Assert.That(shortLived.transform.position.z, Is.EqualTo(1).Within(0.001f));
            Assert.That(target.Current, Is.EqualTo(100));
            yield return null;
        }
        [UnityTest] public IEnumerator PoolReusesConsumedProjectileWithFreshDamage()
        {
            var target = Target(5);
            attack.Delivery = DamageDelivery.Projectile;
            attack.ProjectileSpeed = 100;
            var prefab = NewProjectile();
            prefab.gameObject.SetActive(false);
            var poolObject = new GameObject("Pool");
            cleanup.Add(poolObject);
            var pool = poolObject.AddComponent<ProjectilePool>();
            pool.Prefab = prefab;
            Physics.SyncTransforms();
            var first = pool.Spawn(origin, Vector3.forward, new DamageInfo(25), owner.transform, attack, null);
            first.Simulate(0.1f);
            Assert.That(pool.ActiveCount, Is.Zero);
            var second = pool.Spawn(origin, Vector3.forward, new DamageInfo(10), owner.transform, attack, null);
            Assert.That(second, Is.SameAs(first));
            second.Simulate(0.1f);
            Assert.That(target.Current, Is.EqualTo(65));
            Assert.That(pool.ActiveCount, Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator EmitterCooldownAndMissingPoolRejectShots()
        {
            attack.Cooldown = 1;
            Assert.That(weapon.TryFire(origin, Vector3.forward), Is.True);
            Assert.That(weapon.TryFire(origin, Vector3.forward), Is.False);
            weapon.ResetCooldown();
            attack.Delivery = DamageDelivery.Projectile;
            Assert.That(weapon.TryFire(origin, Vector3.forward), Is.False);
            Assert.That(weapon.TryFire(origin, Vector3.zero), Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator CriticalTypedHitscanAppliesResistedDamageAndBurn()
        {
            var fire = Asset<DamageTypeDefinition>();
            var resistance = Asset<ResistanceProfile>();
            resistance.Configure(new DamageResistance(fire, 0.5f));
            var profile = Asset<HealthDefinition>();
            profile.Resistances = resistance;
            var target = Target(5);
            target.ApplyDefinition(profile);
            var statuses = target.gameObject.AddComponent<StatusController>();
            var tick = Asset<PeriodicDamageEffect>();
            tick.DamageType = fire; tick.DamagePerStack = 4;
            var burn = Asset<StatusDefinition>();
            burn.TickEffects = new StatusTickEffect[] { tick };
            attack.Damage = 10; attack.CriticalChance = 1; attack.CriticalMultiplier = 2; attack.DamageType = fire;
            attack.OnHitStatuses = new[] { new StatusApplication(burn) };
            Physics.SyncTransforms();
            weapon.TryFire(origin, Vector3.forward);
            Assert.That(target.Current, Is.EqualTo(90));
            Assert.That(statuses.Count, Is.EqualTo(1));
            statuses.Advance(1);
            Assert.That(target.Current, Is.EqualTo(88));
            yield return null;
        }
        [UnityTest] public IEnumerator ProjectileKeepsRolledDamageAndStatusesAfterWeaponChanges()
        {
            var target = Target(5);
            var statuses = target.gameObject.AddComponent<StatusController>();
            var fire = Asset<DamageTypeDefinition>();
            var burn = Asset<StatusDefinition>();
            attack.Damage = 10; attack.CriticalChance = 1; attack.CriticalMultiplier = 2;
            attack.DamageType = fire; attack.OnHitStatuses = new[] { new StatusApplication(burn) };
            attack.Delivery = DamageDelivery.Projectile;
            attack.ProjectileSpeed = 100;
            var template = NewProjectile();
            template.gameObject.SetActive(false);
            var go = new GameObject("Weapon pool"); cleanup.Add(go);
            var pool = go.AddComponent<ProjectilePool>(); pool.Prefab = template; weapon.Pool = pool;
            Physics.SyncTransforms();
            Assert.That(weapon.TryFire(origin, Vector3.forward), Is.True);
            var projectile = pool.GetComponentInChildren<DamageProjectile>();
            attack.Damage = 999; attack.CriticalChance = 0; attack.DamageType = null;
            attack.OnHitStatuses = System.Array.Empty<StatusApplication>();
            var received = default(DamageInfo);
            target.Damaged += result => received = result.Damage;
            projectile.Simulate(0.1f);
            Assert.That(target.Current, Is.EqualTo(80));
            Assert.That(received.IsCritical, Is.True);
            Assert.That(received.Type, Is.SameAs(fire));
            Assert.That(statuses.GetStacks(burn), Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator DifferentWeaponPrefabsKeepSeparateReusablePools()
        {
            var a = NewProjectile(); a.name = "A"; a.gameObject.SetActive(false);
            var b = NewProjectile(); b.name = "B"; b.gameObject.SetActive(false);
            var go = new GameObject("Mixed pool"); cleanup.Add(go);
            var pool = go.AddComponent<ProjectilePool>();
            attack.Delivery = DamageDelivery.Projectile;
            attack.ProjectilePrefab = a;
            var first = pool.Spawn(origin, Vector3.forward, new DamageInfo(10), owner.transform, attack, null);
            first.Despawn();
            attack.ProjectilePrefab = b;
            var second = pool.Spawn(origin, Vector3.forward, new DamageInfo(10), owner.transform, attack, null);
            Assert.That(second, Is.Not.SameAs(first));
            second.Despawn();
            attack.ProjectilePrefab = a;
            var reused = pool.Spawn(origin, Vector3.forward, new DamageInfo(10), owner.transform, attack, null);
            Assert.That(reused, Is.SameAs(first));
            pool.ClearActive();
            Assert.That(pool.ActiveCount, Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator SchedulerTicksAndExpiresActiveStatusInRealPlayerLoop()
        {
            var target = Target(5);
            var statuses = target.gameObject.AddComponent<StatusController>();
            var effect = Asset<PeriodicDamageEffect>(); effect.DamagePerStack = 1;
            var burn = Asset<StatusDefinition>();
            burn.Duration = 0.12f; burn.TickInterval = 0.04f;
            burn.TickEffects = new StatusTickEffect[] { effect };
            statuses.Apply(burn);
            yield return new WaitForSeconds(0.3f);
            Assert.That(target.Current, Is.EqualTo(97));
            Assert.That(statuses.Count, Is.Zero);
        }
        [UnityTest] public IEnumerator DeactivatingActorClearsStatusesButPreservesHealth()
        {
            var target = Target(5);
            var statuses = target.gameObject.AddComponent<StatusController>();
            var burn = Asset<StatusDefinition>();
            statuses.Apply(burn);
            target.ApplyDamage(new DamageInfo(10));
            target.gameObject.SetActive(false);
            target.gameObject.SetActive(true);
            Assert.That(statuses.Count, Is.Zero);
            Assert.That(target.Current, Is.EqualTo(90));
            yield return null;
        }
    }
}
