using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class PresentationTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private CombatPresentationDefinition profile;
        private T Asset<T>() where T:ScriptableObject { var value=ScriptableObject.CreateInstance<T>(); cleanup.Add(value); return value; }
        private GameObject Actor() { var value=new GameObject("Presentation actor"); cleanup.Add(value); return value; }
        [SetUp] public void Setup() { profile=Asset<CombatPresentationDefinition>(); profile.Rules=Array.Empty<CombatPresentationRule>(); profile.GlobalMaximumPerSecond=0; }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private static CombatPresentationEvent Event(float amount=1,long subject=1,long source=2,bool critical=false,ScriptableObject definition=null)
            =>new CombatPresentationEvent(CombatPresentationKind.Damage,new CombatActorIdentity(subject,"Target"),new CombatActorIdentity(source,"Source"),Vector3.one,amount,definition,critical);
        [Test] public void MergingSumsAmountsAndSeparatesCriticalSourcesAndDefinitions()
        {
            profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,.1f) };
            var buffer=new CombatPresentationBuffer(profile); var values=new List<CombatPresentationEvent>(); buffer.Emitted+=values.Add;
            buffer.Submit(Event(10),0); buffer.Submit(Event(20),.01); buffer.Submit(Event(5,critical:true),.01);
            buffer.Submit(Event(6,source:3),.01); buffer.Submit(Event(7,definition:profile),.01);
            Assert.That(buffer.Flush(.05),Is.Zero); Assert.That(buffer.Flush(.2),Is.EqualTo(4));
            Assert.That(values.Find(e=>e.Amount==30).Count,Is.EqualTo(2)); Assert.That(buffer.Merged,Is.EqualTo(1));
        }
        [Test] public void CombinedSourcesHaveNoFalseSingleOwnerAndRulesAreCaptured()
        {
            profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,.1f) { SeparateSources=false } };
            var buffer=new CombatPresentationBuffer(profile); CombatPresentationEvent value=default; buffer.Emitted+=e=>value=e;
            profile.Rules[0]=new CombatPresentationRule(CombatPresentationKind.Damage,0);
            buffer.Submit(Event(3),0); buffer.Submit(Event(4,source:3),.01); buffer.Flush(.2);
            Assert.That(value.Amount,Is.EqualTo(7)); Assert.That(value.Source.Id,Is.Zero);
        }
        [Test] public void ExpiredWindowRemovalPreservesNewWindowIndex()
        {
            profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,.1f) };
            var buffer=new CombatPresentationBuffer(profile); var values=new List<CombatPresentationEvent>(); buffer.Emitted+=values.Add;
            buffer.Submit(Event(1),0); buffer.Submit(Event(2),.2); buffer.Flush(.2);
            buffer.Submit(Event(3),.21); buffer.Flush(.4);
            Assert.That(values.Count,Is.EqualTo(2)); Assert.That(values[0].Amount,Is.EqualTo(1)); Assert.That(values[1].Amount,Is.EqualTo(5));
        }
        [Test] public void CapacityFrameAndRateBudgetsAreIndependentAndExpire()
        {
            profile.Capacity=3; profile.MaximumPerFrame=1; profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,0,1) };
            var buffer=new CombatPresentationBuffer(profile);
            buffer.Submit(Event(),0); buffer.Submit(Event(),0); buffer.Submit(Event(subject:3),0);
            Assert.That(buffer.Submit(Event(),0),Is.False); Assert.That(buffer.DroppedCapacity,Is.EqualTo(1));
            Assert.That(buffer.Flush(0),Is.EqualTo(1)); Assert.That(buffer.Count,Is.EqualTo(2));
            Assert.That(buffer.Flush(0),Is.EqualTo(1)); Assert.That(buffer.Flush(0),Is.Zero); Assert.That(buffer.DroppedRate,Is.EqualTo(1));
            buffer.Submit(Event(),1); Assert.That(buffer.Flush(1),Is.EqualTo(1));
        }
        [Test] public void GlobalAndRateKeyCapacityAreBounded()
        {
            profile.RateKeyCapacity=1; profile.GlobalMaximumPerSecond=2;
            profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,0,5) };
            var buffer=new CombatPresentationBuffer(profile);
            buffer.Submit(Event(),0); buffer.Submit(Event(subject:3),0); buffer.Submit(Event(),0); buffer.Submit(Event(),0);
            Assert.That(buffer.Flush(0),Is.EqualTo(2)); Assert.That(buffer.DroppedRate,Is.EqualTo(2));
            buffer.Submit(Event(subject:3),1); Assert.That(buffer.Flush(1),Is.EqualTo(1));
        }
        [Test] public void CallbackSubmissionsWaitUntilNextFlushAndReentrancyIsSafe()
        {
            var buffer=new CombatPresentationBuffer(profile); var calls=0;
            buffer.Emitted+=e=> { calls++; buffer.Submit(Event(),0); Assert.That(buffer.Flush(0),Is.Zero); };
            buffer.Submit(Event(),0); Assert.That(buffer.Flush(0),Is.EqualTo(1)); Assert.That(calls,Is.EqualTo(1)); Assert.That(buffer.Count,Is.EqualTo(1));
            Assert.That(buffer.Flush(0),Is.EqualTo(1));
        }
        [Test] public void CallbackClearAndObserverExceptionsCannotCorruptBuffer()
        {
            var buffer=new CombatPresentationBuffer(profile); var calls=0;
            buffer.Emitted+=e=>throw new InvalidOperationException("Presentation observer");
            buffer.Emitted+=e=> { calls++; buffer.Clear(); Assert.That(buffer.Submit(Event(),0),Is.False); };
            buffer.Submit(Event(),0); buffer.Submit(Event(),0); LogAssert.Expect(LogType.Exception,"InvalidOperationException: Presentation observer");
            Assert.That(buffer.Flush(0),Is.EqualTo(1)); Assert.That(calls,Is.EqualTo(1)); Assert.That(buffer.Count,Is.Zero);
        }
        [Test] public void RecycledLivesDropOldNumbersButDeathKeepsCachedPosition()
        {
            var health=Actor().AddComponent<Health>(); var buffer=new CombatPresentationBuffer(profile); CombatPresentationEvent delivered=default; buffer.Emitted+=e=>delivered=e;
            buffer.Submit(new CombatPresentationEvent(CombatPresentationKind.Damage,health.Identity,default,Vector3.one,10,health:health),0);
            buffer.Submit(new CombatPresentationEvent(CombatPresentationKind.Death,health.Identity,default,Vector3.one,health:health),0);
            health.ResetToFull(); Assert.That(buffer.Flush(0),Is.EqualTo(1)); Assert.That(buffer.DroppedStale,Is.EqualTo(1)); Assert.That(delivered.Kind,Is.EqualTo(CombatPresentationKind.Death));
            buffer.Submit(new CombatPresentationEvent(CombatPresentationKind.Death,health.Identity,default,Vector3.one,health:health),1);
            Object.DestroyImmediate(health.gameObject); Assert.That(buffer.Flush(1),Is.EqualTo(1)); Assert.That(delivered.Position,Is.EqualTo(Vector3.one));
        }
        [Test] public void DisabledInvalidAndBackwardsClockPoliciesAreExplicit()
        {
            profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage) { Enabled=false } };
            var buffer=new CombatPresentationBuffer(profile); Assert.That(buffer.Submit(Event(),0),Is.False); Assert.That(buffer.Suppressed,Is.EqualTo(1));
            Assert.That(buffer.Submit(default,0),Is.False); Assert.That(buffer.Submit(Event(),double.NaN),Is.False);
            var custom=new CombatPresentationEvent(CombatPresentationKind.Custom,default,default,Vector3.zero);
            buffer.Submit(custom,10); buffer.Submit(custom,5); Assert.That(buffer.Count,Is.EqualTo(1));
        }
        [Test] public void WarmMergedAndRateLimitedDeliveryDoesNotAllocate()
        {
            profile.Rules=new[] { new CombatPresentationRule(CombatPresentationKind.Damage,.01f,10) };
            var buffer=new CombatPresentationBuffer(profile); buffer.Emitted+=Ignore; var value=Event();
            for(var i=0;i<30;i++) { buffer.Submit(value,i); buffer.Submit(value,i); buffer.Flush(i+.1); }
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=30;i<1030;i++) { buffer.Submit(value,i); buffer.Submit(value,i); buffer.Flush(i+.1); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero);
        }
        private static void Ignore(CombatPresentationEvent _) { }
        [Test] public void ActorRelayPublishesHealthHealingAndStatusesWithoutChangingGameplay()
        {
            var hub=Actor().AddComponent<CombatPresentationHub>(); hub.Configure(profile); hub.AutomaticFlush=false;
            var actor=Actor(); var health=actor.AddComponent<Health>(); var statuses=actor.AddComponent<StatusController>();
            var relay=actor.AddComponent<CombatPresentationRelay>(); relay.Configure(hub);
            var values=new List<CombatPresentationEvent>(); hub.Emitted+=values.Add;
            var burn=Asset<StatusDefinition>(); var tick=Asset<PeriodicDamageEffect>(); tick.DamagePerStack=3; burn.TickEffects=new StatusTickEffect[] { tick }; burn.TickInterval=1;
            health.ApplyDamage(new DamageInfo(10)); health.ApplyHealing(new HealingInfo(5)); statuses.Apply(burn); statuses.Advance(1); hub.Flush();
            Assert.That(health.Current,Is.EqualTo(92)); Assert.That(values.FindAll(e=>e.Kind==CombatPresentationKind.Damage).Count,Is.EqualTo(2));
            Assert.That(values.Find(e=>e.Kind==CombatPresentationKind.Healing).Amount,Is.EqualTo(5));
            Assert.That(values.Exists(e=>e.Kind==CombatPresentationKind.StatusApplied && e.Definition==burn),Is.True);
            Assert.That(values.Exists(e=>e.Kind==CombatPresentationKind.StatusTick),Is.False);
            hub.Emitted-=values.Add; hub.Emitted+=Ignore;
            for(var i=0;i<30;i++) { health.ApplyDamage(new DamageInfo(.01f)); hub.Flush(); }
            var before=GC.GetAllocatedBytesForCurrentThread();
            for(var i=0;i<100;i++) { health.ApplyDamage(new DamageInfo(.01f)); hub.Flush(); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread()-before,Is.Zero,"Warm local actor relay + damage resolution + hub, no UI.");
        }
    }
}
