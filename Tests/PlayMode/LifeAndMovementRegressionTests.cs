using System;
using System.Collections;
using System.Collections.Generic;
using EasyCombat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Obj=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class LifeAndMovementRegressionTests
    {
        private readonly List<Obj> cleanup=new List<Obj>();
        private static readonly Vector3 At=new Vector3(900,35,800);
        private GameObject Go(string name,Vector3 offset=default) { var go=new GameObject(name); cleanup.Add(go); go.transform.position=At+offset; return go; }
        private T Asset<T>() where T:ScriptableObject { var value=ScriptableObject.CreateInstance<T>(); cleanup.Add(value); return value; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            for(int i=cleanup.Count-1;i>=0;i--) if(cleanup[i]) Obj.Destroy(cleanup[i]); cleanup.Clear(); yield return null;
        }

        [Test] public void ReflectedDelayedShotMustNotHitNewLifeOfItsShooter()
        {
            var shooter=Go("pooled shooter").AddComponent<Health>();
            var target=Go("reflecting target",Vector3.forward*4); target.AddComponent<BoxCollider>(); var hp=target.AddComponent<Health>();
            var reflect=Asset<ReactionDamageAction>(); reflect.FixedDamage=7;
            var rule=Asset<ReactionDefinition>(); rule.Trigger=ReactionTrigger.DamageTaken; rule.Target=ReactionTarget.Attacker; rule.Actions=new CombatReactionAction[]{reflect};
            Assert.IsTrue(target.AddComponent<CombatReactions>().Configure(new[]{rule}));
            var weapon=Asset<AttackDefinition>(); weapon.Delivery=DamageDelivery.Projectile; weapon.Damage=10; weapon.CriticalChance=0; weapon.Range=30; weapon.ProjectileSpeed=12;
            var request=weapon.RollDamage(shooter.gameObject,At,Vector3.forward,0,1); long original=shooter.Identity.Id;
            var shot=Go("shot").AddComponent<DamageProjectile>(); shot.Launch(At,Vector3.forward,request,shooter.transform,weapon,null,p=>p.gameObject.SetActive(false));
            shooter.ResetToFull(); Physics.SyncTransforms(); shot.Simulate(1);
            Assert.AreEqual(90,hp.Current,"Projectile must hit its intended victim.");
            Assert.AreEqual(100,shooter.Current,$"Old source life {original}, live source {shooter.Identity.Id}; HP of replacement {shooter.Current}.");
        }

        [Test] public void FatalHitMustNotDispatchDeathToRespawnedActor()
        {
            var h=Go("respawned").AddComponent<Health>(); long original=h.Identity.Id,deathLife=0; float deathHP=-1;
            h.Damaged+=_=>h.ResetToFull();
            h.Died+=actor=> { deathLife=actor.Identity.Id; deathHP=actor.Current; };
            var result=h.ApplyDamage(new DamageInfo(150));
            Assert.IsTrue(result.Killed);
            Assert.IsFalse(deathLife!=0 && deathLife!=original && deathHP>0,
                $"Incoming life {original}; Died observed life {deathLife} at {deathHP} HP.");
        }
        [Test] public void SourceObserverThatRespawnsStopsOldLifeFanout()
        {
            var source=Go("attacker").AddComponent<Health>(); var target=Go("victim").AddComponent<Health>();
            bool deliveredToReplacement=false;
            source.Resolved+=_=>source.ResetToFull();
            source.Resolved+=_=>deliveredToReplacement=true;
            target.ApplyDamage(new DamageInfo(5,source.gameObject));
            Assert.IsFalse(deliveredToReplacement);
        }
        [Test] public void ResetFromNestedHitFinishesBeforeDeathReactions()
        {
            var source=Go("attacker").AddComponent<Health>(); var go=Go("victim"); var h=go.AddComponent<Health>();
            var action=Asset<ReactionDamageAction>(); action.FixedDamage=7;
            var rule=Asset<ReactionDefinition>(); rule.Trigger=ReactionTrigger.Death; rule.Target=ReactionTarget.Attacker;
            rule.Actions=new CombatReactionAction[]{action}; Assert.IsTrue(go.AddComponent<CombatReactions>().Configure(new[]{rule}));
            h.ApplyDamage(new DamageInfo(5)); long before=h.Identity.Id;
            h.Healed+=_=>h.ApplyDamage(new DamageInfo(200,source.gameObject));
            h.Died+=_=>{h.ResetToFull();h.ResetToFull();}; int resets=0;
            h.Changed+=c=>{if(c.Reason==HealthChangeReason.Reset) resets++;};
            h.Heal(1);
            Assert.AreEqual(1,resets); Assert.AreNotEqual(before,h.Identity.Id); Assert.AreEqual(100,h.Current);
            Assert.AreEqual(100,source.Current,"The queued action belonged to the victim's old life.");
        }

        [Test] public void ResetDuringFirstOnHitStatusMustNotRelabelHitOrApplySecondStatusToNewLife()
        {
            var go=Go("target"); var h=go.AddComponent<Health>(); var statuses=go.AddComponent<StatusController>();
            var a=Asset<StatusDefinition>(); var b=Asset<StatusDefinition>(); long original=h.Identity.Id,logged=0;
            statuses.Lifecycle+=c=> { if(c.Kind==StatusChangeKind.Applied && c.Definition==a) h.ResetToFull(); };
            h.Resolved+=rec=>logged=rec.Target.Id;
            h.ApplyDamage(new DamageInfo(5,onHitStatuses:new[]{new StatusApplication(a),new StatusApplication(b)}));
            Assert.AreNotEqual(original,h.Identity.Id,"Observer did not initiate a new life.");
            Assert.IsTrue(statuses.GetStacks(b)==0 && logged==original,
                $"Original life {original}; current {h.Identity.Id}, recorded target {logged}, second-status stacks on replacement {statuses.GetStacks(b)}.");
        }

        [Test] public void NavGaitAssignedWhilePooledShouldSurviveEnable()
        {
            var settings=NavMesh.GetSettingsByIndex(0);
            var src=new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,size=new Vector3(12,.2f,12),transform=Matrix4x4.identity};
            var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{src},new Bounds(Vector3.zero,new Vector3(15,5,15)),Vector3.zero,Quaternion.identity);
            cleanup.Add(data); var nav=NavMesh.AddNavMeshData(data,At,Quaternion.identity);
            var go=Go("pooled npc",Vector3.up*.15f);
            try
            {
                var agent=go.AddComponent<NavMeshAgent>(); agent.speed=6; var adapter=go.AddComponent<NavMeshSpeedAdapter>();
                go.SetActive(false); adapter.BaseSpeed=8; go.SetActive(true);
                Assert.AreEqual(8,agent.speed,$"Gait assigned while inactive: 8; after enable, BaseSpeed={adapter.BaseSpeed}, agent.speed={agent.speed}.");
            }
            finally { go.SetActive(false); nav.Remove(); }
        }

        [UnityTest] public IEnumerator ReparentedColliderInsideVolumeShouldTransferItsSlow()
        {
            var zone=Go("slow volume"); var trigger=zone.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.size=Vector3.one*4;
            var effect=Asset<MovementInfluenceDefinition>(); effect.SpeedMultiplier=.5f; zone.AddComponent<MovementSurface>().Effect=effect;
            var a=Go("previous actor").AddComponent<MovementInfluences>(); var b=Go("new actor").AddComponent<MovementInfluences>();
            var foot=Go("movable contact"); foot.transform.SetParent(a.transform,true); var body=foot.AddComponent<Rigidbody>(); body.isKinematic=true; foot.AddComponent<BoxCollider>();
            Physics.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); yield return null;
            Assert.AreEqual(.5f,a.Multiplier,"Precondition: original actor must be inside.");
            foot.transform.SetParent(b.transform,true); Physics.SyncTransforms();
            for(int i=0;i<4;i++) { body.WakeUp(); yield return new WaitForFixedUpdate(); } yield return null;
            Assert.IsTrue(a.Multiplier==1 && b.Multiplier==.5f,$"After transfer in place: old actor multiplier {a.Multiplier}, new actor {b.Multiplier}.");
        }

    }
}
