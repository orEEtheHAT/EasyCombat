using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ProjectileTraceBufferTests
    {
        private readonly List<Object> owned=new List<Object>();
        private readonly Vector3 start=new Vector3(-1400,750,-1250);
        private AttackDefinition attack;
        [SetUp] public void Setup()
        {
            attack=ScriptableObject.CreateInstance<AttackDefinition>(); owned.Add(attack);
            attack.Delivery=DamageDelivery.Projectile; attack.ProjectileSpeed=10; attack.ProjectileRadius=.2f; attack.Range=40; attack.ProjectileLifetime=4;
            attack.HitMask=1<<10; attack.Triggers=QueryTriggerInteraction.Collide;
        }
        [UnityTearDown] public IEnumerator Cleanup() { for(int i=owned.Count-1;i>=0;i--) if(owned[i]) Object.Destroy(owned[i]); owned.Clear(); yield return null; }
        private GameObject Go(Vector3 at) { var go=new GameObject("Trace test"); owned.Add(go); go.transform.position=at; go.layer=10; return go; }
        private Health Target(Vector3 at) { var go=Go(at); go.AddComponent<BoxCollider>().size=Vector3.one*.18f; return go.AddComponent<Health>(); }
        private DamageProjectile Shot(Vector3 at,Action<DamageImpact> callback=null)
        {
            var shot=Go(at).AddComponent<DamageProjectile>();
            shot.Launch(at,Vector3.forward,new DamageInfo(5),null,attack,callback,p=>p.gameObject.SetActive(false)); return shot;
        }
        [Test] public void MovingTriggerEnteringBulletAfterEmptyStepIsNotMissed()
        {
            var hp=Target(start+Vector3.right*6); hp.GetComponent<BoxCollider>().isTrigger=true; var shot=Shot(start);
            Physics.SyncTransforms(); shot.Simulate(.03f); Assert.IsTrue(shot.IsFlying);
            hp.transform.position=shot.transform.position+Vector3.back*.12f; Physics.SyncTransforms(); shot.Simulate(.03f);
            Assert.AreEqual(95,hp.Current); Assert.IsFalse(shot.IsFlying);
        }
        [Test] public void GrazingEndOfShortSweepStillHits()
        {
            var hp=Target(start+new Vector3(.26f,0,.47f)); var shot=Shot(start);
            Physics.SyncTransforms(); shot.Simulate(.03f);
            Assert.AreEqual(95,hp.Current); Assert.IsFalse(shot.IsFlying);
        }
        [UnityTest] public IEnumerator ShortStepUsesSelectedPhysicsScene()
        {
            var scene=SceneManager.CreateScene("Short sweep test "+GetHashCode(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try
            {
                var local=Target(start+Vector3.forward*.5f); SceneManager.MoveGameObjectToScene(local.gameObject,scene);
                Physics.SyncTransforms(); var world=scene.GetPhysicsScene(); world.Simulate(.02f);
                var shot=Shot(start); using(new CombatExecutionScope(physicsScene:world)) shot.Simulate(.04f);
                Assert.AreEqual(95,local.Current,"The broad and precise queries must use the same physics scene.");
            }
            finally { SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        [Test] public void NestedProjectileKeepsOuterContactSnapshot()
        {
            attack.ContactBehavior=ContactBehavior.PierceTargets; attack.MaximumTargets=2;
            var a=Target(start+Vector3.forward*3); var b=Target(start+Vector3.forward*6);
            var side=start+Vector3.right*4; var c=Target(side+Vector3.forward*3); var d=Target(side+Vector3.forward*6);
            var inner=Shot(side); bool fired=false; var outer=Shot(start,_=>{if(!fired){fired=true; inner.Simulate(1);}});
            Physics.SyncTransforms(); outer.Simulate(1);
            Assert.IsFalse(inner.IsFlying); Assert.IsFalse(outer.IsFlying);
            foreach(var hp in new[]{a,b,c,d}) Assert.AreEqual(95,hp.Current);
        }
        [Test] public void WidePierceDeduplicatesBeyondInlineKeysAndClearsOnReuse()
        {
            attack.ContactBehavior=ContactBehavior.PierceTargets; attack.MaximumTargets=7;
            var targets=new Health[7];
            for(int i=0;i<targets.Length;i++)
            {
                targets[i]=Target(start+Vector3.forward*(1+i)); var child=Go(targets[i].transform.position+Vector3.forward*.1f);
                child.transform.SetParent(targets[i].transform,true); child.AddComponent<SphereCollider>().radius=.15f;
                child.AddComponent<Hurtbox>().Configure(targets[i]);
            }
            Physics.SyncTransforms(); var shot=Shot(start);
            for(int round=0;round<2;round++)
            {
                if(round==1) shot.Launch(start,Vector3.forward,new DamageInfo(5),null,attack,null,p=>p.gameObject.SetActive(false));
                for(int i=0;i<50 && shot.IsFlying;i++) shot.Simulate(.02f);
                Assert.IsFalse(shot.IsFlying); foreach(var hp in targets) Assert.AreEqual(95-5*round,hp.Current);
            }
        }
    }
}
