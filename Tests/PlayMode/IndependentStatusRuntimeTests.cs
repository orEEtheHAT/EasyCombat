using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EasyCombat.Tests
{
    public sealed class IndependentStatusRuntimeTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private T Asset<T>() where T : ScriptableObject { var item = ScriptableObject.CreateInstance<T>(); cleanup.Add(item); return item; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        [UnityTearDown] public IEnumerator Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }
        [UnityTest] public IEnumerator SharedSchedulerExpiresIndependentStacksSeparately()
        {
            var go = Actor("Scheduled target"); var health = go.AddComponent<Health>(); var controller = go.AddComponent<StatusController>();
            var effect = Asset<PeriodicDamageEffect>(); effect.DamagePerStack = 1;
            var status = Asset<StatusDefinition>(); status.DisplayName = "Scheduled burn";
            status.Duration = .6f; status.TickInterval = .1f; status.TickEffects = new StatusTickEffect[] { effect };
            controller.Apply(status);
            yield return new WaitForSeconds(.35f);
            controller.Apply(status);
            yield return new WaitForSeconds(.35f);
            Assert.That(controller.GetStacks(status), Is.EqualTo(1));
            Assert.That(controller.GetStatus(0).Remaining, Is.GreaterThan(0));
            yield return new WaitForSeconds(.5f);
            Assert.That(controller.Count, Is.Zero); Assert.That(controller.IndependentRecordCount, Is.Zero);
            Assert.That(health.Current, Is.EqualTo(88));
        }
        [UnityTest] public IEnumerator DisableClearsAllStacksAndReenableStartsClean()
        {
            var go = Actor("Pooled target"); var controller = go.AddComponent<StatusController>();
            var status = Asset<StatusDefinition>(); controller.Apply(status); controller.Apply(status);
            Assert.That(controller.GetStacks(status), Is.EqualTo(2));
            go.SetActive(false); Assert.That(controller.Count, Is.Zero); Assert.That(controller.IndependentRecordCount, Is.Zero);
            go.SetActive(true); Assert.That(controller.Count, Is.Zero); controller.Apply(status);
            Assert.That(controller.GetStacks(status), Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator PeriodicAreaKeepsEachApplicationSourceWhenStackExpires()
        {
            var position = new Vector3(0, 4000, 0);
            var target = Actor("Area target"); target.transform.position = position + Vector3.forward;
            target.AddComponent<BoxCollider>(); target.AddComponent<Health>(); var controller = target.AddComponent<StatusController>();
            var status = Asset<StatusDefinition>(); status.DisplayName = "Field status"; status.Duration = 1; status.TickInterval = .25f;
            var first = Actor("First source"); first.AddComponent<Health>(); var second = Actor("Second source"); second.AddComponent<Health>();
            var shape = Asset<AreaDefinition>(); shape.Radius = 2;
            var attack = Asset<AttackDefinition>(); attack.Delivery = DamageDelivery.Area; attack.Area = shape; attack.StatusOnly = true;
            attack.OnHitStatuses = new[] { new StatusApplication(status) };
            var pulses = Asset<AreaPulseDefinition>(); pulses.Attack = attack; pulses.Duration = .1f; pulses.Interval = 1;
            var fieldObject = Actor("Field"); fieldObject.transform.position = position; var field = fieldObject.AddComponent<DamageArea>();
            Physics.SyncTransforms(); field.Begin(pulses, first); field.Stop(); controller.Advance(.5f);
            field.Begin(pulses, second); field.Stop();
            Assert.That(controller.GetStacks(status), Is.EqualTo(2)); controller.Advance(.5f);
            Assert.That(controller.GetStacks(status), Is.EqualTo(1));
            Assert.That(controller.GetStack(status, 0).Origin.Actor.Name, Is.EqualTo("Second source")); yield return null;
        }
    }
}
