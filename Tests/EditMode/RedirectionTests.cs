using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class RedirectionTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private Health Actor(string name) { var a=new GameObject(name); cleanup.Add(a); return a.AddComponent<Health>(); }
        private DamageRedirector Link(Health target,Health guardian,float fraction=.5f)
        { var redirector=target.gameObject.AddComponent<DamageRedirector>(); redirector.Definition=Asset<DamageRedirectionDefinition>(); redirector.Definition.Fraction=fraction; redirector.Guardian=guardian; return redirector; }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void RedirectionIsAfterTargetDefensesAndGuardianReceivesItsOwnDefenses()
        {
            var target=Actor("Target"); var guardian=Actor("Guardian"); Link(target,guardian);
            var armor=Asset<FlatArmorLayer>(); armor.Reduction=10; var defenses=Asset<DefenseProfile>(); defenses.Layers=new[] { new DefenseSlot(armor) };
            var hp=Asset<HealthDefinition>(); hp.Defense=defenses; target.ApplyDefinition(hp);
            var type=Asset<DamageTypeDefinition>(); var resist=Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(type,.5f));
            var guardHp=Asset<HealthDefinition>(); guardHp.Resistances=resist; guardian.ApplyDefinition(guardHp);
            var result=target.ApplyDamage(new DamageInfo(50,type:type));
            Assert.That(result.ArmorPrevented,Is.EqualTo(10)); Assert.That(result.Redirected,Is.EqualTo(20)); Assert.That(result.AppliedAmount,Is.EqualTo(20));
            Assert.That(guardian.Current,Is.EqualTo(90)); Assert.That(result.Breakdown[0].Redirected,Is.EqualTo(20));
        }
        [Test] public void FullTransferDoesNotReportImmunityAndRunsAfterOriginalNotifications()
        {
            var target=Actor("Target"); var guardian=Actor("Guardian"); Link(target,guardian,1);
            var originalPublished=false; var sawOriginal=false;
            System.Action<CombatRecord> listener=record=> { if(record.Target.Id==target.Identity.Id) originalPublished=true; };
            CombatEvents.DamageResolved+=listener; guardian.Damaged+=_=>sawOriginal=originalPublished;
            try
            {
                var result=target.ApplyDamage(new DamageInfo(20)); Assert.That(result.Rejection,Is.EqualTo(DamageRejection.None));
                Assert.That(target.Current,Is.EqualTo(100)); Assert.That(guardian.Current,Is.EqualTo(80)); Assert.That(sawOriginal,Is.True);
            }
            finally { CombatEvents.DamageResolved-=listener; }
        }
        [Test] public void CircularLinksAreBoundedAndRemainderStaysOnLastEligibleTarget()
        {
            var a=Actor("A"); var b=Actor("B"); Link(a,b,1); Link(b,a,1); var dropped=CombatReactionQueue.DroppedActions;
            a.ApplyDamage(new DamageInfo(20)); Assert.That(a.Current+b.Current,Is.EqualTo(180)); Assert.That(CombatReactionQueue.DroppedActions,Is.GreaterThan(dropped));
        }
        [Test] public void InvalidOrDistantGuardianLeavesDamageOnOriginalTarget()
        {
            var target=Actor("Target"); var guardian=Actor("Guardian"); var link=Link(target,guardian,1); link.Definition.MaximumDistance=1;
            guardian.transform.position=Vector3.right*5; Assert.That(target.ApplyDamage(new DamageInfo(10)).AppliedAmount,Is.EqualTo(10));
            guardian.transform.position=Vector3.zero; guardian.enabled=false; Assert.That(target.ApplyDamage(new DamageInfo(10)).AppliedAmount,Is.EqualTo(10));
        }
        [Test] public void TransferCapAndTypeFilterApplyToOneSharedBudget()
        {
            var target=Actor("Target"); var guardian=Actor("Guardian"); var link=Link(target,guardian,1); var fire=Asset<DamageTypeDefinition>();
            link.Definition.MaximumDamage=10; link.Definition.FilterTypes=true; link.Definition.Types=new[] { fire };
            var parts=new DamageAmounts(); parts.Add(fire,20); parts.Add(fire,20); parts.Add(null,20);
            var result=target.ApplyDamage(new DamageInfo(parts)); Assert.That(result.Redirected,Is.EqualTo(10)); Assert.That(result.AppliedAmount,Is.EqualTo(50)); Assert.That(guardian.Current,Is.EqualTo(90));
        }
    }
}
