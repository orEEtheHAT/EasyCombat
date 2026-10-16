using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class Delivery2DTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly Vector2 origin=new Vector2(0,5000);
        private GameObject source;
        private DamageEmitter2D emitter;
        private AttackDefinition attack;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name,Vector2 offset)
        { var go=new GameObject(name); cleanup.Add(go); go.transform.position=origin+offset; return go; }
        private Health Target(Vector2 offset)
        { var go=Actor("Target",offset); go.AddComponent<CircleCollider2D>().radius=.25f; return go.AddComponent<Health>(); }
        [SetUp] public void Setup()
        {
            source=Actor("Source",Vector2.zero); source.AddComponent<Health>(); emitter=source.AddComponent<DamageEmitter2D>();
            attack=Asset<AttackDefinition>(); attack.Damage=10; attack.Range=20; attack.RoundsPerMinute=0; attack.HitscanRadius=attack.ProjectileRadius=.5f; emitter.Attack=attack;
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private DamageProjectile2D Projectile()
        { var go=Actor("Projectile",Vector2.zero); var projectile=go.AddComponent<DamageProjectile2D>(); projectile.AutomaticAdvance=false; return projectile; }
        [TestCase(DamageDelivery.Hitscan)] [TestCase(DamageDelivery.Projectile)]
        public void Wide2DAttacksShareTypedDamageAndStatuses(DamageDelivery delivery)
        {
            var target=Target(new Vector2(5,.6f)); var fire=Asset<DamageTypeDefinition>(); var poison=Asset<DamageTypeDefinition>();
            attack.DamageChannels=new[] { new DamageChannel(fire,2,2),new DamageChannel(poison,3,3) }; var status=Asset<StatusDefinition>(); attack.OnHitStatuses=new[] { new StatusApplication(status) };
            var statuses=target.gameObject.AddComponent<StatusController>(); attack.Delivery=delivery; Physics2D.SyncTransforms();
            if(delivery==DamageDelivery.Hitscan) Assert.That(emitter.TryFire(origin,Vector2.right),Is.True);
            else { var projectile=Projectile(); projectile.Launch(origin,Vector2.right,attack.RollDamage(source,origin,Vector2.right,0,1),source.transform,attack,returnToPool:p=>p.gameObject.SetActive(false)); projectile.Simulate(1); }
            Assert.That(target.Current,Is.EqualTo(95)); Assert.That(statuses.GetStacks(status),Is.EqualTo(1));
        }
        [Test] public void PiercingDeduplicatesWeakpointsAndStopsAt2DWall()
        {
            var target=Target(new Vector2(5,0)); var next=Target(new Vector2(12,0)); var weak=Actor("Weakpoint",new Vector2(4,0)); weak.transform.SetParent(target.transform,true);
            weak.AddComponent<CircleCollider2D>().radius=.2f; weak.AddComponent<Hurtbox>().Configure(target,2);
            var wall=Actor("Wall",new Vector2(8,0)); wall.AddComponent<BoxCollider2D>().size=new Vector2(.1f,3);
            attack.ContactBehavior=ContactBehavior.PierceTargets; Physics2D.SyncTransforms(); emitter.TryFire(origin,Vector2.right);
            Assert.That(target.Current,Is.EqualTo(80)); Assert.That(next.Current,Is.EqualTo(100));
        }
        [Test] public void AreaDeduplicatesCollidersAndFiltersCone()
        {
            var target=Target(new Vector2(2,0)); var behind=Target(new Vector2(-2,0)); var extra=Actor("Extra",new Vector2(2,0)); extra.transform.SetParent(target.transform,true); extra.AddComponent<BoxCollider2D>();
            attack.Delivery=DamageDelivery.Area; attack.Area=Asset<AreaDefinition>(); attack.Area.Shape=AreaShape.Cone; attack.Area.Radius=4; attack.Area.ConeAngle=60;
            Physics2D.SyncTransforms(); emitter.TryFire(origin,Vector2.right); Assert.That(target.Current,Is.EqualTo(90)); Assert.That(behind.Current,Is.EqualTo(100));
        }
        [Test] public void AoeLineOfSightUses2DColliders()
        {
            var target=Target(new Vector2(3,0)); var wall=Actor("Wall",new Vector2(1.5f,0)); wall.AddComponent<BoxCollider2D>().size=new Vector2(.2f,3);
            var area=Asset<AreaDefinition>(); area.Radius=4; area.RequireLineOfSight=true; Physics2D.SyncTransforms();
            new AreaDamageQuery2D().Apply(origin,Vector2.right,new DamageInfo(10,source),new AreaSettings(area),source.transform);
            Assert.That(target.Current,Is.EqualTo(100));
        }
        [Test] public void StaleContactDoesNotHitRespawnedLife()
        {
            var near=Target(new Vector2(3,0)); var far=Target(new Vector2(7,0)); near.Damaged+=_=>far.ResetToFull(); attack.ContactBehavior=ContactBehavior.PierceTargets;
            Physics2D.SyncTransforms(); emitter.TryFire(origin,Vector2.right); Assert.That(near.Current,Is.EqualTo(90)); Assert.That(far.Current,Is.EqualTo(100));
        }
        [Test] public void PoolReusesReturnedProjectileAndClearsActiveExplicitly()
        {
            var prefab=Projectile(); prefab.gameObject.SetActive(false); var pool=Actor("Pool",Vector2.zero).AddComponent<ProjectilePool2D>(); pool.Prefab=prefab; pool.Prewarm(1); attack.Delivery=DamageDelivery.Projectile;
            var request=attack.RollDamage(source,origin,Vector2.right,0,1); var first=pool.Spawn(origin,Vector2.right,request,source.transform,attack); first.Despawn();
            var second=pool.Spawn(origin,Vector2.right,request,source.transform,attack); Assert.That(second,Is.SameAs(first)); Assert.That(pool.ActiveCount,Is.EqualTo(1));
            pool.ClearActive(); Assert.That(pool.ActiveCount,Is.Zero);
        }
        [Test] public void Unsupported3DMotionIsRejectedExplicitly()
        {
            attack.ProjectileMotion=Asset<ProjectileMotionDefinition>(); attack.ProjectileMotion.AutoAcquire=false;
            Assert.That(emitter.TryFire(origin,Vector2.right),Is.False);
        }
        [Test] public void BodyPartAdapterDisablesAndRestores2DColliders()
        {
            var core=Target(new Vector2(8,0)); var child=Actor("Part",new Vector2(5,0)); child.transform.SetParent(core.transform,true); var collider=child.AddComponent<CircleCollider2D>();
            var profile=Asset<BodyPartDefinition>(); profile.Health=Asset<HealthDefinition>(); profile.Health.MaximumHealth=20;
            var part=child.AddComponent<CombatBodyPart>(); part.Configure(profile,core); child.AddComponent<BodyPartColliders2D>().Initialize();
            part.ApplyDamage(new DamageInfo(20,source)); Assert.That(collider.enabled,Is.False); core.ResetToFull(); Assert.That(collider.enabled,Is.True);
        }
        [Test] public void DirectionalBlockCanUseActorLocalAxisInXYPlane()
        {
            var target=Target(new Vector2(5,0)); var block=Asset<BlockLayer>(); block.LocalFacing=Vector3.left; block.Arc=90;
            var defense=Asset<DefenseProfile>(); defense.Layers=new[] { new DefenseSlot(block) }; var hp=Asset<HealthDefinition>(); hp.Defense=defense; target.ApplyDefinition(hp);
            Physics2D.SyncTransforms(); emitter.TryFire(origin,Vector2.right); Assert.That(target.Current,Is.EqualTo(100));
            target.transform.rotation=Quaternion.Euler(0,0,180); Physics2D.SyncTransforms(); emitter.TryFire(origin,Vector2.right); Assert.That(target.Current,Is.EqualTo(90));
        }
    }
}
