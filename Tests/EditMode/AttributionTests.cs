using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AttributionTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private Health Actor(string name) { var actor=new GameObject(name); cleanup.Add(actor); return actor.AddComponent<Health>(); }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [TestCase(KillCreditMode.LastHit)] [TestCase(KillCreditMode.HighestDamage)] public void KillerAndAssistsUseActualHealthDamage(KillCreditMode mode)
        {
            var target=Actor("Victim"); var a=Actor("A"); var b=Actor("B"); var profile=Asset<AttributionDefinition>(); profile.Killer=mode;
            var tracker=target.gameObject.AddComponent<CombatAttribution>(); tracker.Configure(profile); KillCredit result=default; var count=0;
            tracker.Credited+=credit=> { result=credit; count++; };
            target.ApplyDamage(new DamageInfo(70,a.gameObject)); target.ApplyDamage(new DamageInfo(1000,b.gameObject)); target.ApplyDamage(new DamageInfo(1,b.gameObject));
            Assert.That(count,Is.EqualTo(1)); Assert.That(result.Killer.Id,Is.EqualTo(mode==KillCreditMode.LastHit ? b.Identity.Id : a.Identity.Id));
            Assert.That(result.Assists.Count,Is.EqualTo(1)); Assert.That(result.Assists[0].Damage,Is.EqualTo(mode==KillCreditMode.LastHit ? 70 : 30));
        }
        [Test] public void FinishingDownedTargetGetsLastHitCreditWithZeroHpLoss()
        {
            var target=Actor("Victim"); var a=Actor("A"); var b=Actor("B");
            var hp=Asset<HealthDefinition>(); hp.LifeRules=Asset<LifeRulesDefinition>(); hp.LifeRules.AllowDowned=true; target.ApplyDefinition(hp);
            var tracker=target.gameObject.AddComponent<CombatAttribution>(); tracker.Configure(Asset<AttributionDefinition>()); KillCredit result=default; var count=0;
            tracker.Credited+=credit=> { result=credit; count++; };
            target.ApplyDamage(new DamageInfo(100,a.gameObject)); Assert.That(count,Is.Zero);
            target.ApplyDamage(new DamageInfo(1,b.gameObject)); Assert.That(count,Is.EqualTo(1)); Assert.That(result.Killer.Id,Is.EqualTo(b.Identity.Id));
        }
        [Test] public void ExpiredContributionAndPreviousLifeDoNotEarnAssists()
        {
            var target=Actor("Victim"); var a=Actor("A"); var b=Actor("B"); var profile=Asset<AttributionDefinition>(); profile.ContributionWindow=1;
            var tracker=target.gameObject.AddComponent<CombatAttribution>(); tracker.Configure(profile); KillCredit result=default; tracker.Credited+=credit=>result=credit;
            var clock=new ManualCombatClock();
            using(new CombatExecutionScope(clock:clock))
            {
                target.ApplyDamage(new DamageInfo(50,a.gameObject)); clock.Advance(2); target.ApplyDamage(new DamageInfo(100,b.gameObject)); Assert.That(result.Assists.Count,Is.Zero);
                target.Revive(1); target.ApplyDamage(new DamageInfo(100,a.gameObject)); Assert.That(result.Assists.Count,Is.Zero);
            }
        }
        [Test] public void DestroyedEditModeSubscribersArePrunedBeforeDispatch()
        {
            var target=Actor("Destroyed tracker"); var profile=Asset<AttributionDefinition>();
            var before=CombatEvents.SubscriberCount; target.gameObject.AddComponent<CombatAttribution>().Configure(profile);
            Assert.That(CombatEvents.SubscriberCount,Is.EqualTo(before+1));
            Object.DestroyImmediate(target.gameObject);
            Actor("Survivor").ApplyDamage(new DamageInfo(1));
            Assert.That(CombatEvents.SubscriberCount,Is.LessThanOrEqualTo(before));
            UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
        }
    }
}
