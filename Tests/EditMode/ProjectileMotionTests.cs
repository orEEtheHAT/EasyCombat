using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ProjectileMotionTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly Vector3 origin=new Vector3(0,4600,0);
        private GameObject source;
        private DamageProjectile projectile;
        private AttackDefinition attack;
        private ProjectileMotionDefinition motion;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name,Vector3 offset) { var go=new GameObject(name); cleanup.Add(go); go.transform.position=origin+offset; return go; }
        private Health Target(Vector3 offset) { var go=Actor("Target",offset); go.AddComponent<SphereCollider>().radius=.3f; return go.AddComponent<Health>(); }
        [SetUp] public void Setup()
        {
            source=Actor("Source",Vector3.zero); source.AddComponent<Health>(); projectile=Actor("Projectile",Vector3.zero).AddComponent<DamageProjectile>();
            attack=Asset<AttackDefinition>(); attack.Delivery=DamageDelivery.Projectile; attack.Damage=10; attack.ProjectileSpeed=10; attack.ProjectileLifetime=10; attack.Range=50; attack.ProjectileRadius=.1f;
            motion=Asset<ProjectileMotionDefinition>(); motion.AutoAcquire=false; attack.ProjectileMotion=motion;
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private void Launch(Health target=null,System.Action<DamageImpact> impact=null)
        {
            Physics.SyncTransforms(); projectile.Launch(origin,Vector3.forward,attack.RollDamage(source,origin,Vector3.forward,0,1),source.transform,attack,impact,p=>p.gameObject.SetActive(false),homingTarget:target);
        }
        private void Advance(int steps) { for(var i=0;i<steps && projectile.IsFlying;i++) projectile.Simulate(.05f); }
        [Test] public void HomingSteersToExplicitOffAxisTarget()
        {
            var target=Target(new Vector3(3,0,5)); motion.Homing=true; motion.TurnDegreesPerSecond=720; Launch(target); Advance(30);
            Assert.That(target.Current,Is.EqualTo(90)); Assert.That(projectile.IsFlying,Is.False);
        }
        [Test] public void RicochetUsesPhysicalWallAndCapturedSettings()
        {
            var wall=Actor("Wall",Vector3.forward*3); wall.AddComponent<BoxCollider>().size=new Vector3(4,4,.2f); var target=Target(Vector3.back*2);
            motion.Ricochets=1; motion.RicochetDamageMultiplier=.5f; Launch(); motion.Ricochets=0; motion.RicochetDamageMultiplier=0;
            Advance(30); Assert.That(target.Current,Is.EqualTo(95)); Assert.That(projectile.IsFlying,Is.False);
        }
        [Test] public void ChainChoosesUnvisitedTargetAndScalesOnlyNextLeg()
        {
            var first=Target(Vector3.forward*3); var next=Target(new Vector3(3,0,3)); var third=Target(new Vector3(3,0,6));
            motion.ChainJumps=1; motion.ChainDamageMultiplier=.5f; motion.SearchArea=Asset<AreaDefinition>(); motion.SearchArea.Radius=4;
            Launch(); Advance(30);
            Assert.That(first.Current,Is.EqualTo(90)); Assert.That(next.Current,Is.EqualTo(95)); Assert.That(third.Current,Is.EqualTo(100));
        }
        [TestCase(true,80)] [TestCase(false,90)]
        public void ReturnCanHaveSeparateContactMemory(bool again,float expected)
        {
            var target=Target(Vector3.forward*2); attack.ContactBehavior=ContactBehavior.PierceTargets; motion.ReturnAfterSeconds=.5f; motion.HitAgainOnReturn=again;
            Launch(); Advance(30); Assert.That(target.Current,Is.EqualTo(expected)); Assert.That(projectile.IsFlying,Is.False);
            Assert.That((projectile.transform.position-origin).magnitude,Is.LessThan(.31f));
        }
        [Test] public void CancellationAtImpactPreventsChainRestart()
        {
            var first=Target(Vector3.forward*3); var next=Target(new Vector3(3,0,3)); motion.ChainJumps=1; motion.SearchArea=Asset<AreaDefinition>(); motion.SearchArea.Radius=4;
            Launch(impact:_=>projectile.Despawn()); Advance(30); Assert.That(first.Current,Is.EqualTo(90)); Assert.That(next.Current,Is.EqualTo(100)); Assert.That(projectile.IsFlying,Is.False);
        }
        [Test] public void BoundedGuidanceKeepsUnprocessedSimulationTime()
        {
            Launch(); projectile.Simulate(1); Assert.That(projectile.transform.position.z,Is.EqualTo(4).Within(.001));
            projectile.Simulate(0); Assert.That(projectile.transform.position.z,Is.EqualTo(8).Within(.001));
            projectile.Simulate(0); Assert.That(projectile.transform.position.z,Is.EqualTo(10).Within(.001));
        }
        [Test] public void ReturningProjectileDoesNotFollowNewSourceLife()
        {
            motion.ReturnAfterSeconds=.2f; Launch(); source.GetComponent<Health>().ResetToFull(); Advance(10); Assert.That(projectile.IsFlying,Is.False);
        }
        [Test] public void HomingTargetReuseFallsBackToStraightFlightWhenAcquireDisabled()
        {
            var target=Target(new Vector3(3,0,5)); motion.Homing=true; Launch(target); target.ResetToFull(); Advance(12);
            Assert.That(target.Current,Is.EqualTo(100)); Assert.That(projectile.transform.position.x,Is.Zero.Within(.001));
        }
    }
}
