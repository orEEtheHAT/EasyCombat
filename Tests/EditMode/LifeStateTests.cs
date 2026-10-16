using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class LifeStateTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health health;
        private LifeRulesDefinition rules;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        [SetUp] public void Setup()
        {
            var actor=new GameObject("Life"); cleanup.Add(actor); health=actor.AddComponent<Health>();
            rules=Asset<LifeRulesDefinition>(); rules.AllowDowned=true; rules.ReviveInvulnerability=0;
            var hp=Asset<HealthDefinition>(); hp.LifeRules=rules; health.ApplyDefinition(hp);
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void DowningIsDistinctFromDeathAndFinishingNotifiesExactlyOnce()
        {
            var downed=0; var died=0; health.Downed+=_=>downed++; health.Died+=_=>died++;
            var first=health.ApplyDamage(new DamageInfo(200));
            Assert.That(first.Downed,Is.True); Assert.That(first.Killed,Is.False); Assert.That(health.IsAlive,Is.True); Assert.That(health.IsStanding,Is.False);
            Assert.That(health.Heal(100),Is.Zero); Assert.That(died,Is.Zero);
            Assert.That(health.ApplyDamage(new DamageInfo(1)).Killed,Is.True);
            health.ApplyDamage(new DamageInfo(1)); Assert.That(downed,Is.EqualTo(1)); Assert.That(died,Is.EqualTo(1));
        }
        [Test] public void PeriodicFinishingIsExplicit()
        {
            health.ApplyDamage(new DamageInfo(100));
            Assert.That(health.ApplyDamage(new DamageInfo(10,isPeriodic:true)).Killed,Is.False);
            rules.PeriodicCanFinish=true; Assert.That(health.ApplyDamage(new DamageInfo(10,isPeriodic:true)).Killed,Is.True);
        }
        [Test] public void NonlethalAndMinimumHealthDoNotHealAndFinisherCanBypassThem()
        {
            rules.MinimumHealth=10;
            health.ApplyDamage(new DamageInfo(1000)); Assert.That(health.Current,Is.EqualTo(10));
            rules.MinimumHealth=50; health.ApplyDamage(new DamageInfo(1)); Assert.That(health.Current,Is.EqualTo(10));
            var policy=Asset<DamageProtectionPolicy>(); policy.IgnoreMinimumHealth=true; policy.NonLethal=true;
            var nonlethal=health.ApplyDamage(new DamageInfo(1000,protection:new DamageProtection(policy))); Assert.That(health.Current,Is.EqualTo(1));
            Assert.That(nonlethal.LifePrevented,Is.EqualTo(1)); Assert.That(nonlethal.Overkill,Is.EqualTo(990));
            Assert.That(nonlethal.Breakdown[0].Overkill,Is.EqualTo(nonlethal.Overkill));
            policy.NonLethal=false; policy.PreventDownedState=true;
            Assert.That(health.ApplyDamage(new DamageInfo(1,protection:new DamageProtection(policy))).Killed,Is.True);
        }
        [Test] public void RevivePreservesDownedLifeButCreatesNewIdentityAfterDeath()
        {
            var id=health.Identity.Id; health.ApplyDamage(new DamageInfo(100));
            Assert.That(health.Revive(.5f),Is.True); Assert.That(health.Current,Is.EqualTo(50)); Assert.That(health.Identity.Id,Is.EqualTo(id));
            health.ApplyDamage(new DamageInfo(100)); health.ApplyDamage(new DamageInfo(1));
            Assert.That(health.Revive(),Is.True); Assert.That(health.Current,Is.EqualTo(25)); Assert.That(health.Identity.Id,Is.Not.EqualTo(id));
        }
        [Test] public void SnapshotsPreserveDownedWithoutDeathSideEffectsAndReadVersionOne()
        {
            health.ApplyDamage(new DamageInfo(100)); var state=health.CaptureState(); health.ResetToFull();
            var died=0; health.Died+=_=>died++;
            Assert.That(health.RestoreState(state),Is.True); Assert.That(health.LifeState,Is.EqualTo(CombatLifeState.Downed)); Assert.That(died,Is.Zero);
            Assert.That(health.RestoreState(new HealthSnapshot(100,40) { Version=1 }),Is.True); Assert.That(health.IsStanding,Is.True);
        }
        [Test] public void DownedRestrictionsAreVisibleWithoutControlComponent()
        {
            health.ApplyDamage(new DamageInfo(100)); Assert.That(CombatControl.IsAllowed(health.gameObject,CombatRestriction.Attack),Is.False);
            health.Revive(); Assert.That(CombatControl.IsAllowed(health.gameObject,CombatRestriction.Attack),Is.True);
        }
    }
}
