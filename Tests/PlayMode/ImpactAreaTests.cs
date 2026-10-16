using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ImpactAreaTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 origin = new Vector3(0, 7200, 0);
        private GameObject source;
        private DamageAreaPool pool;
        private AreaSpawnDefinition spawn;
        private AreaPulseDefinition pulses;
        private AttackDefinition attack, pulseAttack;
        private AreaDefinition shape;
        private StatusDefinition burn;
        private DamageProjectile projectile;
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        private GameObject Box(string name, Vector3 position, Vector3 size)
        { var go = Actor(name); go.transform.position = position; go.AddComponent<BoxCollider>().size = size; return go; }
        private Health Target(float z = 4, float x = 0)
        {
            var go = Box("Target", origin + new Vector3(x, 0, z), Vector3.one);
            var hp = go.AddComponent<Health>(); go.AddComponent<StatusController>(); return hp;
        }
        [SetUp] public void Setup()
        {
            source = Box("Caster", origin, Vector3.one); source.AddComponent<Health>();
            pool = Actor("Area pool").AddComponent<DamageAreaPool>();
            var template = Actor("Field template").AddComponent<DamageArea>(); template.gameObject.SetActive(false);
            burn = Asset<StatusDefinition>(); burn.Duration = 5;
            shape = Asset<AreaDefinition>(); shape.Radius = 2;
            pulseAttack = Asset<AttackDefinition>(); pulseAttack.DisplayName = "Puddle"; pulseAttack.Delivery = DamageDelivery.Area;
            pulseAttack.Area = shape; pulseAttack.Damage = 2; pulseAttack.OnHitStatuses = new[] { new StatusApplication(burn) };
            pulses = Asset<AreaPulseDefinition>(); pulses.Attack = pulseAttack; pulses.Duration = 2; pulses.Interval = 1; pulses.ApplyImmediately = false;
            spawn = Asset<AreaSpawnDefinition>(); spawn.Prefab = template; spawn.Pulses = pulses; spawn.Placement = AreaSpawnPlacement.Contact;
            attack = Asset<AttackDefinition>(); attack.DisplayName = "Explosive shot"; attack.Delivery = DamageDelivery.Projectile;
            attack.Area = Asset<AreaDefinition>(); attack.Area.Radius = 2; attack.Damage = 10; attack.RoundsPerMinute = 0;
            attack.ImpactArea = spawn; attack.OnHitStatuses = new[] { new StatusApplication(burn) };
            projectile = Actor("Projectile").AddComponent<DamageProjectile>(); projectile.gameObject.SetActive(false);
        }
        [UnityTearDown] public IEnumerator Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }
        private DamageArea ActiveField() => pool.GetComponentInChildren<DamageArea>();
        private DamageInfo Launch()
        {
            Physics.SyncTransforms();
            var damage = attack.RollDamage(source, origin, Vector3.forward, 0, 1);
            projectile.Launch(origin, Vector3.forward, damage, source.transform, attack, null, p => p.gameObject.SetActive(false), pool);
            return damage;
        }
        private AreaSpawnSnapshot Snapshot() => new AreaSpawnSnapshot(spawn, source, DamageOrigin.Capture(source, attack));

        [Test] public void ExplosionSpawnsOneFieldForManyVictimsAndDuplicateHurtboxes()
        {
            var first = Target(); var second = Target(4, 1);
            var zone = Box("Duplicate hurtbox", origin + Vector3.forward * 3.4f, Vector3.one * .4f);
            zone.transform.SetParent(first.transform, true); zone.AddComponent<Hurtbox>().Configure(first);
            Launch(); projectile.Simulate(1);
            Assert.That(first.Current, Is.EqualTo(90)); Assert.That(second.Current, Is.EqualTo(90)); Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(projectile.IsFlying, Is.False);
            var field = ActiveField(); field.Advance(1);
            Assert.That(first.Current, Is.EqualTo(88)); Assert.That(second.Current, Is.EqualTo(88));
            Assert.That(first.Statuses.GetStacks(burn), Is.EqualTo(2));
            field.Advance(1); Assert.That(first.Current, Is.EqualTo(86)); Assert.That(pool.ActiveCount, Is.Zero);
            field.Advance(10); Assert.That(first.Current, Is.EqualTo(86));
        }
        [Test] public void ImmediatePulseMakesTwoSeparateBurnApplicationsExplicitly()
        {
            pulses.ApplyImmediately = true; var target = Target(); Launch(); projectile.Simulate(1);
            Assert.That(target.Current, Is.EqualTo(88)); Assert.That(target.Statuses.GetStacks(burn), Is.EqualTo(2));
        }
        [Test] public void SnapshotSurvivesSourceDestructionAndRecipeEditsBeforeImpact()
        {
            var target = Target(); var journal = Actor("Journal").AddComponent<CombatJournal>();
            var damage = Launch();
            Object.DestroyImmediate(source); pulses.Duration = 50; pulses.Interval = .01f; pulses.ApplyImmediately = true;
            pulseAttack.Damage = 99; pulseAttack.DisplayName = "Changed"; shape.Radius = .01f;
            spawn.Placement = AreaSpawnPlacement.GroundBelow;
            projectile.Simulate(1);
            Assert.That(target.Current, Is.EqualTo(90)); Assert.That(pool.ActiveCount, Is.EqualTo(1));
            var field = ActiveField(); Assert.That(field.Remaining, Is.EqualTo(2)); field.Advance(1);
            Assert.That(target.Current, Is.EqualTo(88));
            var record = journal.GetNewest(0);
            Assert.That(record.Result.Damage.Origin.Actor.Name, Is.EqualTo("Caster"));
            Assert.That(record.Result.Damage.Origin.AttackName, Is.EqualTo("Puddle"));
            Assert.That(record.Result.Damage.Origin.CastId, Is.EqualTo(damage.Origin.CastId));
        }
        [Test] public void LeavingAreaStopsNewPulsesButExistingBurnKeepsTicking()
        {
            var burnTick = Asset<PeriodicDamageEffect>(); burnTick.DamagePerStack = 1;
            burn.TickEffects = new StatusTickEffect[] { burnTick };
            var target = Target(); Launch(); projectile.Simulate(1);
            var field = ActiveField(); field.Advance(1); Assert.That(target.Current, Is.EqualTo(88));
            target.transform.position += Vector3.right * 10; Physics.SyncTransforms();
            field.Advance(1); Assert.That(target.Current, Is.EqualTo(88));
            target.Statuses.Advance(1); Assert.That(target.Current, Is.EqualTo(86));
        }
        [Test] public void GroundProjectionSkipsCharactersAndAlignsToFloor()
        {
            var target = Target(); Box("Floor", origin + new Vector3(0, -1, 4), new Vector3(20, .2f, 20));
            spawn.Placement = AreaSpawnPlacement.GroundBelow; Physics.SyncTransforms();
            var field = pool.Spawn(Snapshot(), target.transform.position, Vector3.up, Vector3.forward, source.transform);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.transform.position.y, Is.EqualTo(origin.y - .9f + spawn.SurfaceOffset).Within(.002f));
            Assert.That(Vector3.Dot(field.transform.up, Vector3.up), Is.GreaterThan(.999f));
        }
        [Test] public void MissingGroundSkipsFieldWithoutCancelingExplosion()
        {
            spawn.Placement = AreaSpawnPlacement.GroundBelow; spawn.GroundMask = 0;
            var target = Target(); Launch(); projectile.Simulate(1);
            Assert.That(target.Current, Is.EqualTo(90)); Assert.That(pool.ActiveCount, Is.Zero); Assert.That(pool.SkippedSpawns, Is.EqualTo(1));
        }
        [Test] public void WallImpactAlsoCreatesField()
        {
            Box("Wall", origin + Vector3.forward * 4, Vector3.one); Launch(); projectile.Simulate(1);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }
        [Test] public void RangeOrLifetimeExpiryDoesNotCreateField()
        {
            Target(10); attack.Range = 1; Launch(); projectile.Simulate(1); Assert.That(pool.ActiveCount, Is.Zero);
            attack.Range = 40; attack.ProjectileLifetime = .01f; Launch(); projectile.Simulate(1); Assert.That(pool.ActiveCount, Is.Zero);
        }
        [TestCase(AreaSpawnTrigger.FinalContact, 1)] [TestCase(AreaSpawnTrigger.EveryContact, 2)]
        public void PiercingUsesSelectedSpawnTrigger(AreaSpawnTrigger trigger, int expected)
        {
            Target(3); Target(8); attack.Area = null; attack.ContactBehavior = ContactBehavior.PierceTargets; attack.MaximumTargets = 2;
            spawn.Trigger = trigger; Launch(); projectile.Simulate(1);
            Assert.That(pool.ActiveCount, Is.EqualTo(expected));
        }
        [Test] public void HitscanExplosionCanSpawnAtItsContactOrMissEnd()
        {
            attack.Delivery = DamageDelivery.Hitscan; var emitter = source.AddComponent<DamageEmitter>(); emitter.Attack = attack; emitter.AreaPool = pool;
            var target = Target(); Physics.SyncTransforms(); Assert.That(emitter.TryFire(origin, Vector3.forward), Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1)); Assert.That(target.Current, Is.EqualTo(90));
            Assert.That(emitter.TryFire(origin, Vector3.right), Is.True); Assert.That(pool.ActiveCount, Is.EqualTo(2));
        }
        [Test] public void MissingPoolRejectsShotAndRecursiveFieldRecipesAreInvalid()
        {
            attack.Delivery = DamageDelivery.Hitscan; var emitter = source.AddComponent<DamageEmitter>(); emitter.Attack = attack;
            Assert.That(emitter.TryFire(origin, Vector3.forward), Is.False);
            pulseAttack.ImpactArea = spawn; Assert.That(attack.IsValid, Is.False);
            pulseAttack.ImpactArea = null; pulses.Attack = attack; Assert.That(attack.IsValid, Is.False);
        }
        [Test] public void PoolCapacityIsBoundedAndExpiredAreaIsReusedCleanly()
        {
            pool.MaximumActive = 1; var settings = Snapshot();
            var first = pool.Spawn(settings, origin + Vector3.forward * 4, Vector3.up, Vector3.forward);
            Assert.That(pool.Spawn(settings, origin, Vector3.up, Vector3.forward), Is.Null);
            first.Advance(2); Assert.That(pool.ActiveCount, Is.Zero);
            var next = pool.Spawn(settings, origin + Vector3.right * 5, Vector3.up, Vector3.forward);
            Assert.That(next, Is.SameAs(first)); Assert.That(next.Remaining, Is.EqualTo(2)); Assert.That(next.IsRunning, Is.True);
            pool.ClearActive(); Assert.That(pool.ActiveCount, Is.Zero); Assert.That(next.gameObject.activeSelf, Is.False);
        }
        [Test] public void DeactivatingPoolRootStopsFieldsWithoutReparentingDuringDisable()
        {
            var settings = Snapshot(); var field = pool.Spawn(settings, origin, Vector3.up, Vector3.forward);
            pool.gameObject.SetActive(false);
            Assert.That(field.IsRunning, Is.False); Assert.That(pool.ActiveCount, Is.Zero);
            pool.gameObject.SetActive(true);
            Assert.That(pool.Spawn(settings, origin, Vector3.up, Vector3.forward), Is.SameAs(field));
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void DestroyingPoolWithActiveAreaDoesNotReportLifecycleErrors()
        {
            pool.Spawn(Snapshot(), origin, Vector3.up, Vector3.forward);
            Object.DestroyImmediate(pool.gameObject);
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void StopInsidePulseDefersPoolReturnUntilCallbacksComplete()
        {
            Target(); pool.MaximumActive = 1; var settings = Snapshot(); Physics.SyncTransforms();
            var field = pool.Spawn(settings, origin + Vector3.forward * 4, Vector3.up, Vector3.forward);
            var ended = 0; field.Ended += _ => ended++;
            field.Impacted += _ =>
            {
                field.Stop(); field.Advance(1);
                Assert.That(field.Begin(settings.Pulses), Is.False);
                Assert.That(pool.ActiveCount, Is.EqualTo(1));
                Assert.That(pool.Spawn(settings, origin, Vector3.up, Vector3.forward), Is.Null);
            };
            field.Advance(1); Assert.That(ended, Is.EqualTo(1)); Assert.That(pool.ActiveCount, Is.Zero);
        }
        [Test] public void ThrowingImpactObserverDoesNotStopFieldOrLaterObservers()
        {
            var target=Target(); var settings = Snapshot(); Physics.SyncTransforms();
            var field = pool.Spawn(settings, origin + Vector3.forward * 4, Vector3.up, Vector3.forward);
            var observed=0;
            field.Impacted += _ => throw new InvalidOperationException("Test callback");
            field.Impacted += _ => observed++;
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Test callback");
            Assert.DoesNotThrow(() => field.Advance(1)); Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(target.Current,Is.EqualTo(98)); Assert.That(observed,Is.EqualTo(1)); field.Stop(); Assert.That(pool.ActiveCount,Is.Zero);
        }
        [Test] public void ThrowingEmitterObserversDoNotCancelPiercingOrChangeShotIdentity()
        {
            var first=Target(3); var second=Target(7); attack.Delivery=DamageDelivery.Hitscan; attack.Area=null; attack.ImpactArea=null;
            attack.ContactBehavior=ContactBehavior.PierceTargets; attack.MaximumTargets=2;
            var emitter=source.AddComponent<DamageEmitter>(); emitter.Attack=attack; ShotInfo shot=default; var impacts=0;
            emitter.Impacted+=_=>throw new InvalidOperationException("Impact observer");
            emitter.Impacted+=_=> { impacts++; emitter.Attack=pulseAttack; };
            emitter.Fired+=_=>throw new InvalidOperationException("Shot observer"); emitter.Fired+=value=>shot=value;
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Impact observer"); LogAssert.Expect(LogType.Exception,"InvalidOperationException: Impact observer");
            LogAssert.Expect(LogType.Exception,"InvalidOperationException: Shot observer"); Physics.SyncTransforms();
            Assert.That(emitter.TryFire(origin,Vector3.forward),Is.True);
            Assert.That(first.Current,Is.EqualTo(90)); Assert.That(second.Current,Is.EqualTo(90)); Assert.That(impacts,Is.EqualTo(2));
            Assert.That(shot.Attack,Is.SameAs(attack)); Assert.That(shot.Source,Is.SameAs(source)); Assert.That(shot.DamageOrigin.Actor.Id,Is.EqualTo(source.GetComponent<Health>().Identity.Id));
        }
        [Test] public void TwoOverlappingFieldsPulseIndependentlyWithoutDuplicateColliderHits()
        {
            var target = Target(); var settings = Snapshot(); Physics.SyncTransforms();
            var a = pool.Spawn(settings, target.transform.position, Vector3.up, Vector3.forward);
            var b = pool.Spawn(settings, target.transform.position, Vector3.up, Vector3.forward);
            a.Advance(1); b.Advance(1);
            Assert.That(target.Current, Is.EqualTo(96)); Assert.That(target.Statuses.GetStacks(burn), Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator LifetimeReturnsPooledAreaUsingNormalUnityUpdates()
        {
            pulses.Duration = .2f; pulses.Interval = .1f; var target = Target(); var settings = Snapshot(); Physics.SyncTransforms();
            pool.Spawn(settings, target.transform.position, Vector3.up, Vector3.forward);
            yield return new WaitForSeconds(.5f);
            Assert.That(pool.ActiveCount, Is.Zero); Assert.That(target.Current, Is.EqualTo(96));
        }
        [Test] public void WarmPoolSpawnPulseAndReturnDoNotAllocateManagedMemory()
        {
            var target = Target(); pulses.Duration = 1; var settings = Snapshot(); pool.Prewarm(spawn.Prefab, 2); Physics.SyncTransforms();
            for (var i = 0; i < 30; i++)
            { target.ResetToFull(); pool.Spawn(settings, target.transform.position, Vector3.up, Vector3.forward).Advance(1); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            { target.ResetToFull(); pool.Spawn(settings, target.transform.position, Vector3.up, Vector3.forward).Advance(1); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero, "Warmed pool/query/health/statuses, no visual prefab or UI.");
        }
    }
}
