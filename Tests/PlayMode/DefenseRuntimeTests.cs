using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class DefenseRuntimeTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private readonly Vector3 origin = new Vector3(0, 6500, 0);
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name, float z = 0)
        { var go = new GameObject(name); go.transform.position = origin + Vector3.forward * z; cleanup.Add(go); return go; }
        [UnityTearDown] public IEnumerator Teardown()
        {
            foreach (var scheduler in Object.FindObjectsByType<AreaOverlapScheduler>(FindObjectsSortMode.None)) Object.DestroyImmediate(scheduler.gameObject);
            for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null;
        }
        private Health Target(bool shield = true)
        {
            var target = Actor("Target", 3).AddComponent<Health>(); target.gameObject.AddComponent<BoxCollider>(); target.gameObject.AddComponent<CombatTeam>().Team = 1;
            var rules = Asset<HitRulesDefinition>(); rules.AllowAllies = false;
            var profile = Asset<HealthDefinition>(); profile.HitRules = rules;
            if (shield) { var defense = Asset<DefenseProfile>(); defense.Layers = new[] { new DefenseSlot(Asset<ShieldLayer>()) }; profile.Defense = defense; }
            target.ApplyDefinition(profile); return target;
        }
        [TestCase(DamageDelivery.Hitscan, false)] [TestCase(DamageDelivery.Projectile, false)]
        [TestCase(DamageDelivery.Hitscan, true)] [TestCase(DamageDelivery.Projectile, true)] [TestCase(DamageDelivery.Area, true)]
        public void AllDeliveriesRespectCommonRulesThenSpendShield(DamageDelivery delivery, bool explosion)
        {
            var source = Actor("Source"); var team = source.AddComponent<CombatTeam>(); team.Team = 1;
            var target = Target(); var controller = target.gameObject.AddComponent<StatusController>(); var status = Asset<StatusDefinition>();
            var attack = Asset<AttackDefinition>(); attack.Delivery = delivery; attack.Damage = 20; attack.Cooldown = 0; attack.ProjectileSpeed = 20;
            attack.OnHitStatuses = new[] { new StatusApplication(status) }; attack.StatusPolicy = StatusApplyPolicy.AfterDamageOrShield;
            if (explosion) { attack.Area = Asset<AreaDefinition>(); attack.Area.Radius = 5; }
            var emitter = source.AddComponent<DamageEmitter>(); emitter.Attack = attack;
            var projectile = Actor("Projectile").AddComponent<DamageProjectile>(); Physics.SyncTransforms();
            void Fire()
            {
                if (delivery == DamageDelivery.Projectile)
                {
                    projectile.Launch(origin, Vector3.forward, attack.RollDamage(source, origin, Vector3.forward, 0, 1), source.transform, attack, null, p => p.gameObject.SetActive(false));
                    projectile.Simulate(1);
                }
                else Assert.That(emitter.TryFire(origin, Vector3.forward), Is.True);
            }
            Fire(); Assert.That(target.Current, Is.EqualTo(100)); Assert.That(target.ShieldCurrent, Is.EqualTo(50)); Assert.That(controller.Count, Is.Zero);
            team.Team = 2; Fire(); Assert.That(target.Current, Is.EqualTo(100)); Assert.That(target.ShieldCurrent, Is.EqualTo(30)); Assert.That(controller.GetStacks(status), Is.EqualTo(1));
        }
        [Test] public void PeriodicStatusUsesSameRelationshipRulesAndShieldPipeline()
        {
            var source = Actor("Source"); source.AddComponent<CombatTeam>().Team = 1;
            var target = Target(); var controller = target.gameObject.AddComponent<StatusController>();
            var effect = Asset<PeriodicDamageEffect>(); effect.DamagePerStack = 10;
            var status = Asset<StatusDefinition>(); status.TickEffects = new StatusTickEffect[] { effect };
            controller.Apply(status, source); controller.Advance(1); Assert.That(target.ShieldCurrent, Is.EqualTo(50));
            controller.Clear(); source.GetComponent<CombatTeam>().Team = 2; controller.Apply(status, source); controller.Advance(1);
            Assert.That(target.ShieldCurrent, Is.EqualTo(40)); Assert.That(target.Current, Is.EqualTo(100));
        }
        [Test] public void ExclusiveAreasChooseAllowedSourceBeforePriority()
        {
            var target = Target(false); var ally = Actor("Ally", 20); ally.AddComponent<CombatTeam>().Team = 1;
            var enemy = Actor("Enemy", 30); enemy.AddComponent<CombatTeam>().Team = 2;
            var group = Asset<AreaOverlapGroupDefinition>(); group.Mode = AreaOverlapMode.OnePerTarget;
            var attack = Asset<AttackDefinition>(); attack.Delivery = DamageDelivery.Area; attack.Damage = 10; attack.Area = Asset<AreaDefinition>(); attack.Area.Radius = 5;
            var recipe = Asset<AreaPulseDefinition>(); recipe.OverlapGroup = group; recipe.Attack = attack;
            var enemyField = Actor("Enemy field").AddComponent<DamageArea>(); Physics.SyncTransforms(); enemyField.Begin(recipe, enemy);
            recipe.Priority = 100; attack.Damage = 50; Actor("Ally field").AddComponent<DamageArea>().Begin(recipe, ally);
            AreaOverlapScheduler.AdvanceSimulation(1); Assert.That(target.Current, Is.EqualTo(90));
        }
        [Test] public void DisabledHealthPausesDefenseTimersAndPoolResetRestoresState()
        {
            var target = Target(); target.GrantInvulnerability(1); target.enabled = false; target.AdvanceDefense(2);
            Assert.That(target.InvulnerabilityRemaining, Is.EqualTo(1)); target.enabled = true; target.AdvanceDefense(1);
            target.ApplyDamage(new DamageInfo(20)); Assert.That(target.ShieldCurrent, Is.EqualTo(30));
            target.gameObject.SetActive(false); target.gameObject.SetActive(true); Assert.That(target.ShieldCurrent, Is.EqualTo(30));
            target.ResetToFull(); Assert.That(target.ShieldCurrent, Is.EqualTo(50));
        }
        [UnityTest] public IEnumerator SchedulerRechargesAndExpiresInvulnerabilityAutomatically()
        {
            var target = Target(); var layer = (ShieldLayer)target.Defense.GetLayer(0);
            layer.RechargePerSecond = 1000; layer.DelayAfterAbsorption = .02f;
            target.ApplyDamage(new DamageInfo(20)); target.GrantInvulnerability(.05f);
            yield return new WaitForSeconds(.2f);
            Assert.That(target.ShieldCurrent, Is.EqualTo(50)); Assert.That(target.InvulnerabilityRemaining, Is.Zero);
        }
        [UnityTest] public IEnumerator RevivingWithHealthSnapshotResumesExistingShieldRecharge()
        {
            var target = Target(); var layer = (ShieldLayer)target.Defense.GetLayer(0); layer.RechargePerSecond = 1000; layer.DelayAfterAbsorption = 0;
            target.ApplyDamage(new DamageInfo(200)); Assert.That(target.IsAlive, Is.False);
            yield return null;
            target.RestoreState(new HealthSnapshot(100, 100)); yield return new WaitForSeconds(.15f);
            Assert.That(target.ShieldCurrent, Is.EqualTo(50));
        }
    }
}
