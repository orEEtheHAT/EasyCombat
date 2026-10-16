using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class CompositeTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health core;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name) { var go=new GameObject(name); cleanup.Add(go); return go; }
        [SetUp] public void Setup() { core=Actor("Core").AddComponent<Health>(); core.transform.position=new Vector3(0,2700,5); }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private CombatBodyPart Part(out BodyPartDefinition profile,float hp=20,float z=0)
        {
            profile=Asset<BodyPartDefinition>(); profile.Health=Asset<HealthDefinition>(); profile.Health.MaximumHealth=hp;
            var go=Actor("Part"); go.transform.SetParent(core.transform,false); go.transform.localPosition=Vector3.forward*z;
            go.AddComponent<BoxCollider>(); var part=go.AddComponent<CombatBodyPart>(); Assert.That(part.Configure(profile,core),Is.True); return part;
        }
        [TestCase(true,75)] [TestCase(false,90)]
        public void SharedDamageCanIncludeLocalOverkill(bool beforeClamp,float expected)
        {
            var part=Part(out var profile); profile.ShareBeforeLocalHpClamp=beforeClamp; profile.CoreDamageFraction=.5f;
            var breaks=0; part.Broken+=_=>breaks++;
            part.ApplyDamage(new DamageInfo(50)); part.ApplyDamage(new DamageInfo(50));
            Assert.That(core.Current,Is.EqualTo(expected)); Assert.That(breaks,Is.EqualTo(1)); Assert.That(part.GetComponent<Collider>().enabled,Is.False);
            core.ResetToFull(); Assert.That(part.IsBroken,Is.False); Assert.That(part.GetComponent<Collider>().enabled,Is.True);
        }
        [Test] public void DisabledPartReactivatesOnCoreReset()
        {
            var part=Part(out var profile); profile.OnBreak=BodyPartBreakBehavior.DisableObject;
            part.ApplyDamage(new DamageInfo(20)); Assert.That(part.gameObject.activeSelf,Is.False);
            core.ResetToFull(); Assert.That(part.gameObject.activeSelf,Is.True); Assert.That(part.LocalHealth.Current,Is.EqualTo(20));
        }
        [Test] public void BreakCallbackResetDoesNotDisableNewLife()
        {
            var part=Part(out _); part.Broken+=p=>p.ResetPart(); part.ApplyDamage(new DamageInfo(20));
            Assert.That(part.LocalHealth.Current,Is.EqualTo(20)); Assert.That(part.GetComponent<Collider>().enabled,Is.True);
        }
        [Test] public void PercentageUsesCoreHealthAndStatusTransfersOnce()
        {
            var part=Part(out _,100); core.SetMaximum(1000);
            var controller=core.gameObject.AddComponent<StatusController>(); var local=part.gameObject.AddComponent<StatusController>(); var status=Asset<StatusDefinition>();
            var attack=Asset<AttackDefinition>(); attack.Formula=Asset<DamageFormulaDefinition>();
            attack.Formula.Terms=new[] { new DamageFormulaTerm(null,StatusDamageBasis.TargetMaximumHealth,.05f) };
            attack.OnHitStatuses=new[] { new StatusApplication(status) };
            part.ApplyDamage(attack.RollDamage(null,default,Vector3.forward,0,1));
            Assert.That(part.LocalHealth.Current,Is.EqualTo(50)); Assert.That(core.Current,Is.EqualTo(950));
            Assert.That(controller.GetStacks(status),Is.EqualTo(1)); Assert.That(local.GetStacks(status),Is.Zero);
        }
        [Test] public void CoreInvulnerabilityProtectsAllParts()
        {
            var hp=Asset<HealthDefinition>(); hp.HitRules=Asset<HitRulesDefinition>(); hp.HitRules.Invulnerable=true; core.ApplyDefinition(hp);
            var part=Part(out _); Assert.That(part.ApplyDamage(new DamageInfo(10)).Rejection,Is.EqualTo(DamageRejection.Invulnerable));
            Assert.That(part.LocalHealth.Current,Is.EqualTo(20)); Assert.That(core.Current,Is.EqualTo(100));
        }
        [TestCase(true,90)] [TestCase(false,80)]
        public void AreaGroupsByActorOrIndividualPart(bool grouped,float expected)
        {
            Part(out var first,100,-1); Part(out var second,100,1); first.GroupWithCore=second.GroupWithCore=grouped;
            var area=Asset<AreaDefinition>(); area.Radius=3; Physics.SyncTransforms();
            new AreaDamageQuery().Apply(core.transform.position,Vector3.forward,new DamageInfo(10),new AreaSettings(area));
            Assert.That(core.Current,Is.EqualTo(expected));
        }
        [TestCase(true,90)] [TestCase(false,80)]
        public void PiercingGroupsByActorOrIndividualPart(bool grouped,float expected)
        {
            Part(out var first,100,-1); Part(out var second,100,1); first.GroupWithCore=second.GroupWithCore=grouped;
            var source=Actor("Shooter"); var emitter=source.AddComponent<DamageEmitter>(); var attack=Asset<AttackDefinition>(); attack.Damage=10;
            attack.ContactBehavior=ContactBehavior.PierceTargets; attack.MaximumTargets=0; attack.RoundsPerMinute=0; emitter.Attack=attack;
            Physics.SyncTransforms(); Assert.That(emitter.TryFire(new Vector3(0,2700,0),Vector3.forward),Is.True); Assert.That(core.Current,Is.EqualTo(expected));
        }
        [Test] public void LifestealDoesNotCountLocalDurabilityTwice()
        {
            var source=Actor("Shooter").AddComponent<Health>(); source.ApplyDamage(new DamageInfo(50)); var part=Part(out _,100);
            var healing=Asset<HealingDefinition>(); healing.Terms=new[] { new HealingTerm(HealingBasis.TriggeringHealthDamage,1) };
            var action=Asset<ReactionHealingAction>(); action.Healing=healing;
            var rule=Asset<ReactionDefinition>(); rule.Trigger=ReactionTrigger.DamageDealt; rule.Target=ReactionTarget.Owner; rule.Actions=new CombatReactionAction[] { action }; rule.MaximumProcsPerResolution=8;
            source.gameObject.AddComponent<CombatReactions>().Configure(new[] { rule }); part.ApplyDamage(new DamageInfo(10,source.gameObject));
            Assert.That(source.Current,Is.EqualTo(60));
        }
        [Test] public void ResolvedRoutesOnlyToParticipantsAndJournal()
        {
            var source=Actor("Source").AddComponent<Health>(); var other=Actor("Unrelated").AddComponent<Health>(); var dealt=0; var taken=0; var unrelated=0;
            source.Resolved+=_=>dealt++; core.Resolved+=_=>taken++; other.Resolved+=_=>unrelated++;
            var journal=Actor("Journal").AddComponent<CombatJournal>(); journal.StartRecording();
            core.ApplyDamage(new DamageInfo(10,source.gameObject));
            Assert.That(dealt,Is.EqualTo(1)); Assert.That(taken,Is.EqualTo(1)); Assert.That(unrelated,Is.Zero); Assert.That(journal.Count,Is.EqualTo(1));
        }
    }
}
