using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class SharedFormulaDeliveryTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 origin = new Vector3(0, 5500, 0);
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name, float z)
        { var go = new GameObject(name); cleanup.Add(go); go.transform.position = origin + Vector3.forward * z; return go; }
        [UnityTearDown] public IEnumerator Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }

        [TestCase(DamageDelivery.Hitscan, false)] [TestCase(DamageDelivery.Projectile, false)]
        [TestCase(DamageDelivery.Hitscan, true)] [TestCase(DamageDelivery.Projectile, true)]
        [TestCase(DamageDelivery.Area, false)]
        public void DeliveriesResolveSameRecipeIndependentlyForEachTarget(DamageDelivery delivery, bool explosion)
        {
            var source = Actor("Source", 0); var emitter = source.AddComponent<DamageEmitter>();
            var near = Actor("Near", 3).AddComponent<Health>(); near.gameObject.AddComponent<BoxCollider>();
            var far = Actor("Far", 6).AddComponent<Health>(); far.SetMaximum(1000); far.gameObject.AddComponent<BoxCollider>();
            var formula = Asset<DamageFormulaDefinition>(); formula.Terms = new[] { new DamageFormulaTerm(null, StatusDamageBasis.Fixed, 10),
                new DamageFormulaTerm(null, StatusDamageBasis.TargetMaximumHealth, .05f) };
            var attack = Asset<AttackDefinition>(); attack.Formula = formula; attack.Delivery = delivery;
            attack.ContactBehavior = explosion ? ContactBehavior.StopOnFirstHit : ContactBehavior.PierceTargets;
            attack.ProjectileSpeed = 20;
            if (explosion || delivery == DamageDelivery.Area) { attack.Area = Asset<AreaDefinition>(); attack.Area.Radius = 8; }
            emitter.Attack = attack; Physics.SyncTransforms();
            if (delivery == DamageDelivery.Projectile)
            {
                var projectile = Actor("Projectile", 0).AddComponent<DamageProjectile>();
                projectile.Launch(origin, Vector3.forward, attack.RollDamage(source, origin, Vector3.forward, 0, 1),
                    source.transform, attack, null, p => p.gameObject.SetActive(false));
                formula.Terms[0].Minimum = formula.Terms[0].Maximum = 999;
                projectile.Simulate(1);
            }
            else Assert.That(emitter.TryFire(origin, Vector3.forward), Is.True);
            Assert.That(near.Current, Is.EqualTo(85)); Assert.That(far.Current, Is.EqualTo(940));
        }
        [Test] public void FieldReevaluatesCurrentHealthEveryPulse()
        {
            var target = Actor("Target", 1).AddComponent<Health>(); target.gameObject.AddComponent<BoxCollider>();
            var formula = Asset<DamageFormulaDefinition>(); formula.Terms = new[] { new DamageFormulaTerm(null, StatusDamageBasis.TargetCurrentHealth, .1f) };
            var attack = Asset<AttackDefinition>(); attack.Formula = formula; attack.Delivery = DamageDelivery.Area; attack.Area = Asset<AreaDefinition>();
            var recipe = Asset<AreaPulseDefinition>(); recipe.Attack = attack; recipe.Interval = 1; recipe.Duration = 2; recipe.ApplyImmediately = false;
            var field = Actor("Field", 0).AddComponent<DamageArea>(); Physics.SyncTransforms(); Assert.That(field.Begin(recipe), Is.True);
            field.Advance(1); field.Advance(1); Assert.That(target.Current, Is.EqualTo(81));
        }
    }
}
