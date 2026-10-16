using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EasyCombat.Tests
{
    public sealed class StatusFormulaRuntimeTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private T Asset<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); cleanup.Add(item); return item; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        [UnityTearDown] public IEnumerator Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }
        private StatusDefinition Formula()
        {
            var effect = Asset<PeriodicDamageEffect>();
            effect.Terms = new[] { new StatusDamageTerm(StatusDamageBasis.TriggeringHit, .2f) };
            var status = Asset<StatusDefinition>(); status.Duration = 4; status.TickInterval = 1;
            status.TickEffects = new StatusTickEffect[] { effect }; return status;
        }
        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)] [TestCase(DamageDelivery.Area)]
        public void AllDeliveriesCaptureActualCriticalImpactSeparatelyPerStack(DamageDelivery delivery)
        {
            var origin = new Vector3(0, 6000, 0);
            var source = Actor("Source"); source.transform.position = origin; source.AddComponent<Health>();
            var target = Actor("Target"); target.transform.position = origin + Vector3.forward * 5;
            target.AddComponent<BoxCollider>(); var health = target.AddComponent<Health>(); health.SetMaximum(1000);
            var statuses = target.AddComponent<StatusController>(); var status = Formula();
            var attack = Asset<AttackDefinition>(); attack.Delivery = delivery; attack.Damage = 10;
            attack.CriticalChance = 1; attack.RoundsPerMinute = 0; attack.ProjectileSpeed = 20;
            attack.OnHitStatuses = new[] { new StatusApplication(status) };
            if (delivery == DamageDelivery.Area) { var area = Asset<AreaDefinition>(); area.Radius = 8; attack.Area = area; }
            var emitter = source.AddComponent<DamageEmitter>(); emitter.Attack = attack;
            var template = Actor("Projectile template").AddComponent<DamageProjectile>(); template.gameObject.SetActive(false);
            var pool = Actor("Pool").AddComponent<ProjectilePool>(); pool.Prefab = template; emitter.Pool = pool;
            Physics.SyncTransforms();
            Assert.That(emitter.TryFire(origin, Vector3.forward), Is.True);
            if (delivery == DamageDelivery.Projectile) pool.GetComponentInChildren<DamageProjectile>().Simulate(1);
            attack.Damage = 20;
            Assert.That(emitter.TryFire(origin, Vector3.forward), Is.True);
            if (delivery == DamageDelivery.Projectile) pool.GetComponentInChildren<DamageProjectile>().Simulate(1);
            Assert.That(health.Current, Is.EqualTo(940));
            Assert.That(statuses.TryGetStackDamage(status, 0, 0, out var first), Is.True); Assert.That(first, Is.EqualTo(4));
            Assert.That(statuses.TryGetStackDamage(status, 1, 0, out var second), Is.True); Assert.That(second, Is.EqualTo(8));
            statuses.Advance(1); Assert.That(health.Current, Is.EqualTo(928));
        }
        [UnityTest] public IEnumerator SchedulerPreservesCapturedPowerAfterSourceDisappears()
        {
            var source = Actor("Caster"); source.AddComponent<Health>();
            var target = Actor("Scheduled target"); var health = target.AddComponent<Health>(); health.SetMaximum(1000);
            var controller = target.AddComponent<StatusController>(); var status = Formula();
            status.Duration = .3f; status.TickInterval = .1f;
            var applications = new[] { new StatusApplication(status) };
            health.ApplyDamage(new DamageInfo(10, source, onHitStatuses: applications));
            health.ApplyDamage(new DamageInfo(20, source, onHitStatuses: applications));
            Object.Destroy(source);
            yield return new WaitForSeconds(.6f);
            Assert.That(controller.Count, Is.Zero); Assert.That(health.Current, Is.EqualTo(952));
        }
    }
}
