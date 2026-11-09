using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class EnvironmentalCombatTests
    {
        private readonly List<Object> owned=new List<Object>();
        private readonly Vector3 origin=new Vector3(-1420,200,-1700);
        private GameObject Go(string name,Vector3 pos) { var go=new GameObject(name); owned.Add(go); go.transform.position=origin+pos; return go; }
        private T SO<T>() where T:ScriptableObject { var o=ScriptableObject.CreateInstance<T>(); owned.Add(o); return o; }
        private MovementInfluenceDefinition Effect(float value) { var effect=SO<MovementInfluenceDefinition>(); effect.SpeedMultiplier=value; return effect; }
        private ImpactDamageDefinition Impact(ImpactMeasure measure,float safe)
        { var d=SO<ImpactDamageDefinition>(); d.Measure=measure; d.SafeThreshold=safe; d.DamageByExcess=AnimationCurve.Linear(0,0,20,20); d.MaximumDamage=9; d.RepeatDelay=.5f; return d; }
        [UnityTearDown] public IEnumerator Cleanup()
        { for(int i=owned.Count-1;i>=0;i--) if(owned[i]) Object.Destroy(owned[i]); owned.Clear(); yield return null; }
        [Test] public void ImpactUsesThresholdCapAndCooldownPerLife()
        {
            var go=Go("Target",Vector3.zero); var hp=go.AddComponent<Health>(); var recv=go.AddComponent<ImpactDurability>();
            recv.Target=hp; recv.Profile=Impact(ImpactMeasure.NormalSpeed,3);
            bool Hit(float magnitude)=>recv.TryImpact(ImpactMeasure.NormalSpeed,magnitude,go.transform.position,Vector3.up,go.transform.position,out _);
            Assert.IsFalse(Hit(3));
            Assert.IsTrue(Hit(100)); Assert.AreEqual(91,hp.Current); Assert.IsFalse(Hit(100));
            hp.ResetToFull(); Assert.IsTrue(Hit(5)); Assert.That(hp.Current,Is.EqualTo(98).Within(.001));
        }
        [UnityTest] public IEnumerator FallingTargetTakesDamageOnlyOnLanding()
        {
            var ground=Go("Road",Vector3.down); ground.AddComponent<BoxCollider>().size=new Vector3(8,1,8);
            var go=Go("Falling target",Vector3.up*3); go.AddComponent<BoxCollider>(); var body=go.AddComponent<Rigidbody>();
            var hp=go.AddComponent<Health>(); var recv=go.AddComponent<ImpactDurability>(); recv.Target=hp; recv.Profile=Impact(ImpactMeasure.DropHeight,1);
            var source=go.AddComponent<RigidbodyImpactSource>(); source.Receiver=recv;
            yield return new WaitForFixedUpdate(); Assert.AreEqual(100,hp.Current);
            for(int i=0;i<90 && hp.Current==100;i++) yield return new WaitForFixedUpdate();
            Assert.That(hp.Current,Is.EqualTo(98).Within(.35f)); var after=hp.Current;
            for(int i=0;i<12;i++) yield return new WaitForFixedUpdate(); Assert.AreEqual(after,hp.Current);
        }
        private MovementField CreateCone(float slow)
        {
            var go=Go("Turret",Vector3.zero); var sensor=go.AddComponent<CombatAreaTrigger>(); sensor.AutomaticAdvance=false;
            var field=go.AddComponent<MovementField>(); field.StartOnEnable=false; field.Effect=Effect(slow);
            var settings=SO<AreaTriggerDefinition>(); settings.Duration=0; settings.EmitExitOnStop=false; settings.Area=SO<AreaDefinition>();
            settings.Area.Shape=AreaShape.Cone; settings.Area.Radius=8; settings.Area.ConeAngle=60; field.SensorProfile=settings; return field;
        }
        [UnityTest] public IEnumerator NormalSpeedAndImpulseOnlyDamageOnPhysicalContact()
        {
            var floor=Go("Road",Vector3.down).AddComponent<BoxCollider>(); floor.size=new Vector3(12,1,8);
            var actors=new Health[2];
            for(int i=0;i<2;i++)
            {
                var go=Go("Target",new Vector3(i*2,3,0)); go.AddComponent<BoxCollider>(); var rb=go.AddComponent<Rigidbody>(); rb.mass=2;
                var hp=actors[i]=go.AddComponent<Health>(); var receiver=go.AddComponent<ImpactDurability>(); receiver.Target=hp;
                receiver.Profile=Impact(i==0 ? ImpactMeasure.NormalSpeed : ImpactMeasure.NormalImpulse,1);
                go.AddComponent<RigidbodyImpactSource>().Receiver=receiver;
            }
            for(int i=0;i<85 && (actors[0].Current==100 || actors[1].Current==100);i++) yield return new WaitForFixedUpdate();
            Assert.That(actors[0].Current,Is.LessThan(100)); Assert.That(actors[1].Current,Is.LessThan(100));
        }
        [UnityTest] public IEnumerator CompoundActorKeepsVolumeUntilLastColliderLeaves()
        {
            var zone=Go("Volume",Vector3.zero); var volume=zone.AddComponent<BoxCollider>(); volume.size=Vector3.one*3; volume.isTrigger=true;
            zone.AddComponent<MovementSurface>().Effect=Effect(.5f);
            var actor=Go("Compound motor",Vector3.zero); var rb=actor.AddComponent<Rigidbody>(); rb.isKinematic=true;
            var receiver=actor.AddComponent<MovementInfluences>(); var a=Go("Foot A",Vector3.zero); var b=Go("Foot B",Vector3.right*.5f);
            a.transform.SetParent(actor.transform,true); b.transform.SetParent(actor.transform,true); a.AddComponent<BoxCollider>(); b.AddComponent<BoxCollider>();
            Physics.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.AreEqual(.5f,receiver.Multiplier); a.transform.position+=Vector3.right*8; Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate(); Assert.AreEqual(.5f,receiver.Multiplier);
            zone.SetActive(false); Assert.AreEqual(1,receiver.Multiplier);
        }
        [Test] public void OverlappingConesReleaseOnlyTheirOwnSlowAndNeverDealDamage()
        {
            var go=Go("Agent",Vector3.forward*3); go.AddComponent<CapsuleCollider>(); var hp=go.AddComponent<Health>(); var effects=go.AddComponent<MovementInfluences>();
            var a=CreateCone(.5f); var b=CreateCone(.75f); Physics.SyncTransforms(); Assert.IsTrue(a.Fire()); Assert.IsTrue(b.Fire());
            Assert.AreEqual(.5f,effects.Multiplier); Assert.AreEqual(2,effects.SourceCount); a.transform.forward=Vector3.back; a.GetComponent<CombatAreaTrigger>().Advance(.02f);
            Assert.AreEqual(.75f,effects.Multiplier); a.transform.forward=Vector3.forward; a.GetComponent<CombatAreaTrigger>().Advance(.02f); Assert.AreEqual(.5f,effects.Multiplier);
            a.GetComponent<CombatAreaTrigger>().Stop(); Assert.AreEqual(.75f,effects.Multiplier); b.enabled=false; Assert.AreEqual(1,effects.Multiplier);
            Assert.AreEqual(100,hp.Current);
        }
        [Test] public void NavigationUsesCurrentGaitAcrossZoneChanges()
        {
            // A real NavMesh avoids off-mesh agent warnings and exercises the production adapter.
            var settings=NavMesh.GetSettingsByIndex(0);
            var source=new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,size=new Vector3(18,.2f,18),transform=Matrix4x4.identity };
            var data=NavMeshBuilder.BuildNavMeshData(settings,new List<NavMeshBuildSource>{source},new Bounds(Vector3.zero,new Vector3(20,5,20)),Vector3.zero,Quaternion.identity);
            owned.Add(data); var nav=NavMesh.AddNavMeshData(data,origin,Quaternion.identity);
            GameObject go=null;
            try
            {
                go=Go("Patroller",Vector3.up*.15f); var agent=go.AddComponent<NavMeshAgent>(); agent.speed=6;
                var adapter=go.AddComponent<NavMeshSpeedAdapter>(); var receiver=go.GetComponent<MovementInfluences>();
                receiver.Set(adapter,Effect(.5f)); adapter.Refresh(); Assert.AreEqual(3,agent.speed);
                adapter.BaseSpeed=8; Assert.AreEqual(4,agent.speed); receiver.Release(adapter); adapter.Refresh(); Assert.AreEqual(8,agent.speed);
            }
            finally { if(go) go.SetActive(false); nav.Remove(); }
        }
    }
}
