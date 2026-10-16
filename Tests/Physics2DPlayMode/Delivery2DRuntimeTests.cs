using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class Delivery2DRuntimeTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly Vector2 origin=new Vector2(0,5400);
        private GameObject source;
        private ProjectilePool2D pool;
        private AttackDefinition attack;
        private GameObject Actor(string name) { var go=new GameObject(name); cleanup.Add(go); go.transform.position=origin; return go; }
        [SetUp] public void Setup()
        {
            source=Actor("Source"); source.AddComponent<Health>(); var prefab=Actor("Template").AddComponent<DamageProjectile2D>(); prefab.gameObject.SetActive(false);
            pool=Actor("Pool").AddComponent<ProjectilePool2D>(); pool.Prefab=prefab;
            attack=ScriptableObject.CreateInstance<AttackDefinition>(); cleanup.Add(attack); attack.Delivery=DamageDelivery.Projectile; attack.Damage=10; attack.ProjectileSpeed=50; attack.Range=100;
        }
        [UnityTearDown] public IEnumerator Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }
        [UnityTest] public IEnumerator SchedulerMovesPooledProjectileAndReturnsAfterImpact()
        {
            var victim=Actor("Victim"); victim.transform.position=origin+Vector2.right*5; victim.AddComponent<CircleCollider2D>(); var health=victim.AddComponent<Health>(); Physics2D.SyncTransforms();
            var emitter=source.AddComponent<DamageEmitter2D>(); emitter.Attack=attack; emitter.Pool=pool; Assert.That(emitter.TryFire(origin,Vector2.right),Is.True);
            yield return new WaitForSeconds(.25f); Assert.That(health.Current,Is.EqualTo(90)); Assert.That(pool.ActiveCount,Is.Zero);
        }
        [UnityTest] public IEnumerator DisablingPoolReturnsEveryActiveProjectile()
        {
            var request=attack.RollDamage(source,origin,Vector2.right,0,1); pool.Spawn(origin,Vector2.right,request,source.transform,attack);
            Assert.That(pool.ActiveCount,Is.EqualTo(1)); pool.gameObject.SetActive(false); yield return null; Assert.That(pool.ActiveCount,Is.Zero);
        }
    }
}
