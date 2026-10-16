using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AreaContactTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly Vector3 center=new Vector3(0,3600,0);
        private Health target;
        private CombatAreaTrigger field;
        private AreaTriggerDefinition recipe;
        private readonly List<AreaContactKind> events=new List<AreaContactKind>();
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name,Vector3 position) { var go=new GameObject(name); cleanup.Add(go); go.transform.position=position; return go; }
        private AttackDefinition Attack(float amount) { var attack=Asset<AttackDefinition>(); attack.Damage=amount; return attack; }
        [SetUp] public void Setup()
        {
            var go=Actor("Target",center); go.AddComponent<SphereCollider>(); target=go.AddComponent<Health>();
            field=Actor("Field",center).AddComponent<CombatAreaTrigger>(); field.AutomaticAdvance=false; field.Contact+=e=>events.Add(e.Kind);
            recipe=Asset<AreaTriggerDefinition>(); recipe.Area=Asset<AreaDefinition>(); recipe.Area.Radius=2; recipe.EnterAttack=Attack(10); recipe.StayAttack=Attack(2); recipe.ExitAttack=Attack(5);
            Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); events.Clear(); }
        [Test] public void EnterStayExitHaveIndependentPayloads()
        {
            field.Begin(recipe); Assert.That(target.Current,Is.EqualTo(90)); field.Advance(.5f); Assert.That(target.Current,Is.EqualTo(88));
            target.transform.position=center+Vector3.right*5; Physics.SyncTransforms(); field.Advance(0); Assert.That(target.Current,Is.EqualTo(83));
            CollectionAssert.AreEqual(new[] { AreaContactKind.Enter,AreaContactKind.Stay,AreaContactKind.Exit },events); Assert.That(field.OccupantCount,Is.Zero);
        }
        [Test] public void StopEmitsExitOnlyOnceAndCanDisableExitDamage()
        {
            recipe.EmitExitOnStop=false; field.Begin(recipe); field.Stop(); field.Stop(); Assert.That(target.Current,Is.EqualTo(90)); Assert.That(events.Count,Is.EqualTo(1));
            recipe.EmitExitOnStop=true; field.Begin(recipe); field.Stop(); field.Stop(); Assert.That(target.Current,Is.EqualTo(75)); Assert.That(events.Count,Is.EqualTo(3));
        }
        [Test] public void NewPooledLifeGetsEntryWithoutOldExitDamage()
        {
            field.Begin(recipe); target.ResetToFull(); field.Advance(0);
            Assert.That(target.Current,Is.EqualTo(90)); Assert.That(field.OccupantCount,Is.EqualTo(1));
        }
        [Test] public void DeparturesFreeCapacityBeforeNewEntries()
        {
            recipe.MaximumOccupants=1; field.Begin(recipe); target.transform.position=center+Vector3.right*5;
            var next=Actor("Next",center); next.AddComponent<SphereCollider>(); var nextHealth=next.AddComponent<Health>(); Physics.SyncTransforms(); field.Advance(0);
            Assert.That(nextHealth.Current,Is.EqualTo(90)); Assert.That(field.OccupantCount,Is.EqualTo(1)); Assert.That(field.RejectedOccupants,Is.Zero);
        }
        [Test] public void MultipleCollidersDoNotRepeatEntryOrStay()
        {
            var collider=Actor("Extra",center); collider.transform.SetParent(target.transform,true); collider.AddComponent<BoxCollider>(); Physics.SyncTransforms();
            field.Begin(recipe); field.Advance(1); Assert.That(target.Current,Is.EqualTo(86)); Assert.That(events.Count,Is.EqualTo(3));
        }
        [Test] public void ExpiryDrainsBoundedCatchupThenExits()
        {
            recipe.Duration=1; recipe.StayInterval=.1f; field.Begin(recipe); field.Advance(1);
            Assert.That(field.IsRunning,Is.True); Assert.That(target.Current,Is.EqualTo(74)); field.Advance(0);
            Assert.That(field.IsRunning,Is.False); Assert.That(target.Current,Is.EqualTo(65));
        }
    }
}
