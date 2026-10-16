using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class BuildupTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health owner;
        private CombatBuildup buildup;
        private StatusController statuses;
        private StatusDefinition status;
        private BuildupDefinition meter;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        [SetUp] public void Setup()
        {
            var go=new GameObject("Target"); cleanup.Add(go); owner=go.AddComponent<Health>(); statuses=go.AddComponent<StatusController>(); buildup=go.AddComponent<CombatBuildup>();
            status=Asset<StatusDefinition>(); var action=Asset<ReactionStatusAction>(); action.Status=status;
            meter=Asset<BuildupDefinition>(); meter.Threshold=20; meter.Actions=new CombatReactionAction[] { action }; Assert.That(buildup.Configure(new[] { meter }),Is.True);
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void ThresholdRunsAfterDamageAndKeepsHazardSource()
        {
            var hazard=new GameObject("Hazard"); cleanup.Add(hazard); var hit=new DamageInfo(10,hazard); var stacksDuringHit=-1;
            owner.Damaged+=_=>stacksDuringHit=statuses.GetStacks(status);
            owner.ApplyDamage(hit); Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(10)); owner.ApplyDamage(hit);
            Assert.That(stacksDuringHit,Is.Zero); Assert.That(statuses.GetStacks(status),Is.EqualTo(1)); Assert.That(buildup.GetMeter(0).Value,Is.Zero);
            Assert.That(statuses.GetStack(status,0).Origin.Actor.Id,Is.EqualTo(hit.Origin.Actor.Id));
        }
        [Test] public void FilteredMetersOnlyCountTheirActualTypedDamage()
        {
            var fire=Asset<DamageTypeDefinition>(); var ice=Asset<DamageTypeDefinition>(); meter.FilterType=true; meter.DamageType=fire; meter.Threshold=100;
            var second=Asset<BuildupDefinition>(); second.Actions=meter.Actions; second.FilterType=true; second.DamageType=ice;
            Assert.That(buildup.Configure(new[] { meter,second }),Is.True);
            var damage=new DamageAmounts(); damage.Add(fire,3); damage.Add(ice,7); owner.ApplyDamage(new DamageInfo(damage));
            Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(3)); Assert.That(buildup.GetMeter(1).Value,Is.EqualTo(7));
        }
        [Test] public void DecayDelayAndCooldownAdvanceAcrossLargeStep()
        {
            meter.TriggerCooldown=5; meter.KeepOverflow=true; buildup.Add(meter,27);
            Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(7)); Assert.That(buildup.GetMeter(0).Cooldown,Is.EqualTo(5));
            buildup.Advance(3); Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(2)); Assert.That(buildup.GetMeter(0).Cooldown,Is.EqualTo(2));
            buildup.Add(meter,30); Assert.That(statuses.GetStacks(status),Is.EqualTo(1)); Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(20));
            buildup.Advance(2); buildup.Add(meter,1); Assert.That(statuses.GetStacks(status),Is.EqualTo(2));
        }
        [Test] public void PeriodicAndBlockedContactsAreExplicitOptIns()
        {
            meter.FlatPerHit=5; meter.PerHealthDamage=0; owner.ApplyDamage(new DamageInfo(1,isPeriodic:true)); Assert.That(buildup.GetMeter(0).Value,Is.Zero);
            meter.AllowPeriodic=true; owner.ApplyDamage(new DamageInfo(1,isPeriodic:true)); Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(5));
            var type=Asset<DamageTypeDefinition>(); var resist=Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(type,1)); var hp=Asset<HealthDefinition>(); hp.Resistances=resist; owner.ApplyDefinition(hp); buildup.ResetMeters();
            owner.ApplyDamage(new DamageInfo(1,type:type)); Assert.That(buildup.GetMeter(0).Value,Is.Zero);
            meter.CountBlockedContacts=true; owner.ApplyDamage(new DamageInfo(1,type:type)); Assert.That(buildup.GetMeter(0).Value,Is.EqualTo(5));
        }
        [Test] public void ResetRequestedFromNotificationClearsAllMeters()
        {
            buildup.Changed+=change=> { if(change.Meter.Value>0) owner.ResetToFull(); };
            Assert.That(buildup.Add(meter,10),Is.True); Assert.That(buildup.GetMeter(0).Value,Is.Zero); Assert.That(owner.Current,Is.EqualTo(100));
        }
        [Test] public void DeathInsideNotificationCancelsQueuedEffect()
        {
            buildup.Changed+=change=> { if(change.Triggered) owner.ApplyDamage(new DamageInfo(1000)); };
            buildup.Add(meter,20); Assert.That(owner.IsAlive,Is.False); Assert.That(buildup.GetMeter(0).Value,Is.Zero); Assert.That(statuses.GetStacks(status),Is.Zero);
        }
    }
}
