using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AreaDamageTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 center = new Vector3(0, 2000, 0);
        private AreaDefinition area;
        private AttackDefinition attack;
        private GameObject source;
        private T Asset<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); cleanup.Add(item); return item; }
        private GameObject Box(string name, Vector3 offset, Vector3 size)
        {
            var go = new GameObject(name); cleanup.Add(go); go.transform.position = center + offset;
            go.AddComponent<BoxCollider>().size = size; return go;
        }
        private Health Target(Vector3 offset) => Box("Target", offset, Vector3.one).AddComponent<Health>();
        [SetUp] public void Setup()
        {
            source = Box("Source", Vector3.zero, Vector3.one); source.AddComponent<Health>();
            area = Asset<AreaDefinition>(); area.Radius = 5;
            attack = Asset<AttackDefinition>(); attack.Area = area; attack.Delivery = DamageDelivery.Area; attack.Damage = 20; attack.Cooldown = 0;
        }
        [UnityTearDown] public IEnumerator Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }
        private DamageInfo Shot() => attack.RollDamage(source, center, Vector3.forward, .5f, 1);
        [UnityTest] public IEnumerator SphereDeduplicatesHurtboxesAndIgnoresWeakspotMultiplierByDefault()
        {
            var target = Target(Vector3.forward * 2);
            var zone = Box("Weak spot", Vector3.forward * 1.5f, Vector3.one * .5f);
            zone.transform.SetParent(target.transform); zone.AddComponent<Hurtbox>().Configure(target, 3);
            var other = Target(Vector3.right * 3); var far = Target(Vector3.forward * 8);
            Physics.SyncTransforms();
            var impacts = 0; var query = new AreaDamageQuery();
            Assert.That(query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform, _ => impacts++), Is.EqualTo(2));
            Assert.That(target.Current, Is.EqualTo(80)); Assert.That(other.Current, Is.EqualTo(80));
            Assert.That(far.Current, Is.EqualTo(100)); Assert.That(source.GetComponent<Health>().Current, Is.EqualTo(100));
            Assert.That(impacts, Is.EqualTo(2)); yield return null;
        }
        [UnityTest] public IEnumerator OptionalClosestHurtboxMultiplierIsAppliedOnce()
        {
            var target = Target(Vector3.forward * 2);
            var zone = Box("Weak", Vector3.forward, Vector3.one * .5f); zone.transform.SetParent(target.transform);
            zone.AddComponent<Hurtbox>().Configure(target, 2); area.UseHurtboxMultiplier = true; Physics.SyncTransforms();
            new AreaDamageQuery().Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform);
            Assert.That(target.Current, Is.EqualTo(60)); yield return null;
        }
        [UnityTest] public IEnumerator ConeAndDistanceFalloffUseClosestEligiblePoint()
        {
            var front = Target(Vector3.forward * 3); var behind = Target(Vector3.back * 3); var side = Target(Vector3.right * 3);
            area.Shape = AreaShape.Cone; area.ConeAngle = 60; area.DistanceFalloff = true; area.EdgeMultiplier = 0;
            Physics.SyncTransforms(); new AreaDamageQuery().Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform);
            Assert.That(front.Current, Is.EqualTo(90).Within(.001));
            Assert.That(behind.Current, Is.EqualTo(100)); Assert.That(side.Current, Is.EqualTo(100)); yield return null;
        }
        [UnityTest] public IEnumerator WallBlocksAreaOnlyWhenLineOfSightIsRequired()
        {
            var target = Target(Vector3.forward * 4); Box("Wall", Vector3.forward * 2, new Vector3(3, 3, .4f));
            area.RequireLineOfSight = true; Physics.SyncTransforms(); var query = new AreaDamageQuery();
            query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform);
            Assert.That(target.Current, Is.EqualTo(100));
            area.RequireLineOfSight = false; query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform);
            Assert.That(target.Current, Is.EqualTo(80)); yield return null;
        }
        [UnityTest] public IEnumerator TeamFiltersAndOwnerOptionAreIndependent()
        {
            source.AddComponent<CombatTeam>().Team = 1;
            var ally = Target(Vector3.forward * 2); ally.gameObject.AddComponent<CombatTeam>().Team = 1;
            var enemy = Target(Vector3.right * 2); enemy.gameObject.AddComponent<CombatTeam>().Team = 2;
            Physics.SyncTransforms(); var query = new AreaDamageQuery(); area.Targets = AreaTargetFilter.Enemies;
            query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform);
            Assert.That(ally.Current, Is.EqualTo(100)); Assert.That(enemy.Current, Is.EqualTo(80));
            area.Targets = AreaTargetFilter.Allies; area.AffectOwner = true;
            query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform);
            Assert.That(ally.Current, Is.EqualTo(80)); Assert.That(source.GetComponent<Health>().Current, Is.EqualTo(80)); yield return null;
        }
        [UnityTest] public IEnumerator SaturatedOverlapBufferDoesNotSilentlyLoseTargets()
        {
            var targets = new List<Health>();
            for (var i = 0; i < 48; i++)
            {
                var angle = i * Mathf.PI * 2 / 48;
                targets.Add(Target(new Vector3(Mathf.Cos(angle) * 3, 0, Mathf.Sin(angle) * 3)));
            }
            Physics.SyncTransforms();
            Assert.That(new AreaDamageQuery().Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform), Is.EqualTo(48));
            foreach (var target in targets) Assert.That(target.Current, Is.EqualTo(80)); yield return null;
        }
        [UnityTest] public IEnumerator ProjectileExplosionCapturesAreaAndMixedDamageWithoutExtraDirectHit()
        {
            var fire = Asset<DamageTypeDefinition>(); var poison = Asset<DamageTypeDefinition>();
            attack.DamageChannels = new[] { new DamageChannel(fire, 3, 3), new DamageChannel(poison, 8, 8) };
            attack.Delivery = DamageDelivery.Projectile; attack.ProjectileSpeed = 10; area.Radius = 3;
            var first = Target(Vector3.forward * 2); var second = Target(new Vector3(1.5f, 0, 2));
            var go = new GameObject("Projectile"); cleanup.Add(go); var projectile = go.AddComponent<DamageProjectile>();
            var impacts = 0; Physics.SyncTransforms();
            projectile.Launch(center, Vector3.forward, Shot(), source.transform, attack, _ => impacts++, _ => { });
            area.Radius = .1f; attack.DamageChannels[0] = new DamageChannel(fire, 99, 99);
            projectile.Simulate(.3f);
            Assert.That(first.Current, Is.EqualTo(89)); Assert.That(second.Current, Is.EqualTo(89));
            Assert.That(impacts, Is.EqualTo(2)); yield return null;
        }
        [UnityTest] public IEnumerator DirectAreaAndHitscanExplosionUseSameReceiverPath()
        {
            var target = Target(Vector3.forward * 2); var other = Target(new Vector3(2, 0, 2));
            var emitter = source.AddComponent<DamageEmitter>(); emitter.Attack = attack;
            Physics.SyncTransforms(); Assert.That(emitter.TryFire(center, Vector3.forward), Is.True);
            Assert.That(target.Current, Is.EqualTo(80)); Assert.That(other.Current, Is.EqualTo(80));
            attack.Delivery = DamageDelivery.Hitscan; Assert.That(emitter.TryFire(center, Vector3.forward), Is.True);
            Assert.That(target.Current, Is.EqualTo(60)); Assert.That(other.Current, Is.EqualTo(60)); yield return null;
        }
        [UnityTest] public IEnumerator StatusOnlyProjectileCanApplyWithoutDamage()
        {
            var target = Target(Vector3.forward * 2); var statuses = target.gameObject.AddComponent<StatusController>();
            var slow = Asset<StatusDefinition>(); attack.StatusOnly = true; attack.OnHitStatuses = new[] { new StatusApplication(slow) };
            attack.Delivery = DamageDelivery.Projectile; attack.Area = null; attack.ProjectileSpeed = 10;
            var go = new GameObject("Status projectile"); cleanup.Add(go); var projectile = go.AddComponent<DamageProjectile>();
            Physics.SyncTransforms(); projectile.Launch(center, Vector3.forward, Shot(), source.transform, attack, null, _ => { });
            projectile.Simulate(.3f); Assert.That(statuses.Count, Is.EqualTo(1)); Assert.That(target.Current, Is.EqualTo(100)); yield return null;
        }
        [UnityTest] public IEnumerator PersistentAreaHasBoundedCatchUpAndStopsAtDuration()
        {
            attack.Damage = 1; var target = Target(Vector3.forward * 2);
            var recipe = Asset<AreaPulseDefinition>(); recipe.Attack = attack; recipe.Duration = 1; recipe.Interval = .1f; recipe.ApplyImmediately = false;
            var go = new GameObject("Area"); cleanup.Add(go); go.transform.position = center;
            var field = go.AddComponent<DamageArea>(); Physics.SyncTransforms(); Assert.That(field.Begin(recipe, source), Is.True);
            field.Advance(2); Assert.That(target.Current, Is.EqualTo(92)); Assert.That(field.IsRunning, Is.True);
            field.Advance(.001f); Assert.That(target.Current, Is.EqualTo(90)); Assert.That(field.IsRunning, Is.False);
            field.Advance(10); Assert.That(target.Current, Is.EqualTo(90)); yield return null;
        }
        [UnityTest] public IEnumerator AreaHonorsTargetLayersAndTriggerSelection()
        {
            var target = Target(Vector3.forward * 2); target.gameObject.layer = 8; target.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms(); var query = new AreaDamageQuery();
            query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform); Assert.That(target.Current, Is.EqualTo(100));
            area.Triggers = QueryTriggerInteraction.Collide; area.TargetMask = 1 << 9;
            query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform); Assert.That(target.Current, Is.EqualTo(100));
            area.TargetMask = 1 << 8;
            query.Apply(center, Vector3.forward, Shot(), new AreaSettings(area), source.transform); Assert.That(target.Current, Is.EqualTo(80)); yield return null;
        }
        [UnityTest] public IEnumerator FieldCapturesOutgoingBuffAtBegin()
        {
            attack.Damage = 10;
            var key = Asset<CombatStatDefinition>(); attack.DamageModifierStat = key;
            var buff = Asset<StatusDefinition>(); buff.StatModifiers = new[] { new StatusStatModifier(key, 2) };
            var statuses = source.AddComponent<StatusController>(); statuses.Apply(buff);
            var target = Target(Vector3.forward * 2);
            var recipe = Asset<AreaPulseDefinition>(); recipe.Attack = attack; recipe.Duration = 1; recipe.Interval = 1; recipe.ApplyImmediately = false;
            var go = new GameObject("Buffed field"); cleanup.Add(go); go.transform.position = center;
            var field = go.AddComponent<DamageArea>(); Physics.SyncTransforms(); field.Begin(recipe, source);
            statuses.Clear(); field.Advance(1);
            Assert.That(target.Current, Is.EqualTo(80)); yield return null;
        }
        [UnityTest] public IEnumerator WarmAreaWithJournalDoesNotAllocateManagedMemory()
        {
            var target = Target(Vector3.forward * 2); target.SetMaximum(100000); target.ResetToFull();
            var go = new GameObject("Journal"); cleanup.Add(go); go.AddComponent<CombatJournal>();
            Physics.SyncTransforms(); var query = new AreaDamageQuery(); var shot = Shot(); var settings = new AreaSettings(area);
            for (var i = 0; i < 32; i++) query.Apply(center, Vector3.forward, shot, settings, source.transform);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) query.Apply(center, Vector3.forward, shot, settings, source.transform);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero, "Measured warmed query, health and journal; excludes presentation."); yield return null;
        }
    }
}
