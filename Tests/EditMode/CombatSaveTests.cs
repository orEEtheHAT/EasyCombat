using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class CombatSaveTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly List<ScriptableObject> definitions=new List<ScriptableObject>();
        private CombatDefinitionCatalog catalog;
        private CombatReferenceRegistry references;
        private Health a,b;
        private sealed class CountingRandom:ICombatRandom { public int Calls; public float NextUnit() { Calls++; return .4f; } }
        private T Asset<T>() where T:ScriptableObject
        { var asset=ScriptableObject.CreateInstance<T>(); asset.name=typeof(T).Name+definitions.Count; cleanup.Add(asset); definitions.Add(asset); return asset; }
        private Health Actor(string id)
        { var go=new GameObject(id); cleanup.Add(go); var actor=go.AddComponent<Health>(); Assert.That(references.RegisterActor(id,actor),Is.True); return actor; }
        [SetUp] public void Setup()
        { references=new CombatReferenceRegistry(); a=Actor("a"); b=Actor("b"); catalog=ScriptableObject.CreateInstance<CombatDefinitionCatalog>(); cleanup.Add(catalog); }
        [TearDown] public void Cleanup()
        { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); definitions.Clear(); references.Clear(); }
        private void Catalog()
        {
            var entries=new CombatCatalogEntry[definitions.Count]; for(var i=0;i<entries.Length;i++) entries[i]=new CombatCatalogEntry("definition-"+i,definitions[i]);
            Assert.That(catalog.Configure(entries,"save-tests"),Is.True);
        }
        private CombatWorldSnapshot Capture()
        { Catalog(); Assert.That(CombatSaveSystem.TryCapture(catalog,references,out var saved,out var error),Is.True,error); return JsonUtility.FromJson<CombatWorldSnapshot>(JsonUtility.ToJson(saved)); }
        private CombatRestoreResult Restore(CombatWorldSnapshot saved)
        { Assert.That(CombatSaveSystem.TryRestore(saved,catalog,references,out var result,out var error),Is.True,error); return result; }
        private CombatResources Resources(Health owner,out ResourceDefinition resource)
        {
            resource=Asset<ResourceDefinition>(); resource.DelayAfterSpend=2; resource.RegenerationPerSecond=5;
            var profile=Asset<ResourceProfile>(); profile.Resources=new[] { resource }; var resources=owner.gameObject.AddComponent<CombatResources>(); resources.Configure(profile); return resources;
        }
        private StatusController SourceStatus(out StatusDefinition status,out CombatStatDefinition stat,bool live=false)
        {
            stat=Asset<CombatStatDefinition>(); a.gameObject.AddComponent<CombatStats>().SetValue(stat,10);
            var effect=Asset<PeriodicDamageEffect>(); effect.Terms=new[] { new StatusDamageTerm(StatusDamageBasis.SourceStat,1,live ? StatusValueSampling.EveryTick : StatusValueSampling.OnApplication) { Stat=stat } };
            status=Asset<StatusDefinition>(); status.TickEffects=new StatusTickEffect[] { effect }; return b.gameObject.AddComponent<StatusController>();
        }
        [Test] public void JsonRestoresHealthShieldResourcesAndInvulnerabilityWithoutGameplayEvents()
        {
            var shield=Asset<TemporaryShieldLayer>(); var defense=Asset<DefenseProfile>(); defense.Layers=new[] { new DefenseSlot(shield) };
            var hp=Asset<HealthDefinition>(); hp.Defense=defense; b.ApplyDefinition(hp); b.ApplyDamage(new DamageInfo(70,a.gameObject)); b.RestoreShield(0,15); b.AdvanceDefense(1); b.GrantInvulnerability(3);
            var resources=Resources(b,out var mana); resources.TrySpend(new[] { new ResourceCost(mana,30) });
            Catalog(); Assert.That(CombatSaveSystem.TryCaptureJson(catalog,references,out var json,out var error),Is.True,error);
            b.ResetToFull(); resources.ResetResources(); var damages=0; var deaths=0; var revived=0; var resourcesChanged=0; var restored=0;
            b.Damaged+=_=>damages++; b.Died+=_=>deaths++; b.Revived+=_=>revived++; resources.Changed+=_=>resourcesChanged++; b.StateRestored+=_=>restored++;
            var random=new CountingRandom(); using(new CombatExecutionScope(random:random))
                Assert.That(CombatSaveSystem.TryRestoreJson(json,catalog,references,out _,out error),Is.True,error);
            resources.TryGet(mana,out var value);
            Assert.That(b.Current,Is.EqualTo(80)); Assert.That(b.ShieldCurrent,Is.EqualTo(15)); Assert.That(b.Defense.GetState(0).RechargeDelay,Is.EqualTo(4));
            Assert.That(b.InvulnerabilityRemaining,Is.EqualTo(3)); Assert.That(value.Current,Is.EqualTo(70)); Assert.That(value.RechargeDelay,Is.EqualTo(2));
            Assert.That(damages+deaths+revived+resourcesChanged+random.Calls,Is.Zero); Assert.That(restored,Is.EqualTo(1));
        }
        [Test] public void IndependentStacksKeepEachCapturedPowerTimerAndSourceAcrossJson()
        {
            var statuses=SourceStatus(out var status,out var stat); statuses.Apply(status,a.gameObject); statuses.Advance(.25f);
            a.GetComponent<CombatStats>().SetValue(stat,20); statuses.Apply(status,a.gameObject); statuses.Advance(.25f);
            var first=statuses.GetStack(status,0); var second=statuses.GetStack(status,1); var saved=Capture(); var json=JsonUtility.ToJson(saved);
            statuses.Clear(); a.GetComponent<CombatStats>().SetValue(stat,999); b.ApplyDamage(new DamageInfo(5)); var lifecycle=0; statuses.Lifecycle+=_=>lifecycle++;
            var random=new CountingRandom(); using(new CombatExecutionScope(random:random)) Restore(JsonUtility.FromJson<CombatWorldSnapshot>(json));
            Assert.That(random.Calls,Is.Zero); Assert.That(lifecycle,Is.Zero);
            Assert.That(statuses.GetStack(status,0).Remaining,Is.EqualTo(first.Remaining)); Assert.That(statuses.GetStack(status,1).NextTickIn,Is.EqualTo(second.NextTickIn));
            Assert.That(statuses.GetStack(status,0).Origin.Actor.Id,Is.EqualTo(a.Identity.Id));
            a.GetComponent<CombatStats>().SetValue(stat,1000); statuses.Advance(.5f); Assert.That(b.Current,Is.EqualTo(90)); statuses.Advance(.25f); Assert.That(b.Current,Is.EqualTo(70));
        }
        [Test] public void LiveSourceFormulaUsesRestoredLifeButNotReusedOriginalLife()
        {
            var statuses=SourceStatus(out var status,out var stat,true); statuses.Apply(status,a.gameObject); var saved=Capture(); Restore(saved);
            a.GetComponent<CombatStats>().SetValue(stat,20); statuses.Advance(1); Assert.That(b.Current,Is.EqualTo(80));
            a.ResetToFull(); a.GetComponent<CombatStats>().SetValue(stat,1000); statuses.Advance(1); Assert.That(b.Current,Is.EqualTo(80));
        }
        [Test] public void ExpiredStackBacklogSurvivesWithoutReplayingCompletedTicks()
        {
            var effect=Asset<PeriodicDamageEffect>(); effect.Terms=new[] { new StatusDamageTerm(StatusDamageBasis.Fixed,1) };
            var status=Asset<StatusDefinition>(); status.Duration=1; status.TickInterval=.1f; status.TickEffects=new StatusTickEffect[] { effect };
            var statuses=b.gameObject.AddComponent<StatusController>(); statuses.Apply(status,a.gameObject); statuses.Advance(1); Assert.That(b.Current,Is.EqualTo(92));
            var saved=Capture(); statuses.Advance(.001f); Assert.That(b.Current,Is.EqualTo(90)); Restore(saved); statuses.Advance(.001f); Assert.That(b.Current,Is.EqualTo(90));
        }
        [Test] public void UnknownDefinitionOrInvalidLateActorLeavesAllHealthUnchanged()
        {
            Resources(b,out _); a.ApplyDamage(new DamageInfo(10)); b.ApplyDamage(new DamageInfo(20)); var saved=Capture(); a.Heal(5); b.Heal(5);
            saved.Actors[1].Resources.Values[0].Current=float.NaN;
            Assert.That(CombatSaveSystem.TryRestore(saved,catalog,references,out _,out _),Is.False); Assert.That(a.Current,Is.EqualTo(95)); Assert.That(b.Current,Is.EqualTo(85));
            saved.Actors[1].Resources.Values[0].Current=50; saved.Actors[1].Resources.Values[0].Definition="missing";
            Assert.That(CombatSaveSystem.TryRestore(saved,catalog,references,out _,out _),Is.False); Assert.That(a.Current,Is.EqualTo(95)); Assert.That(b.Current,Is.EqualTo(85));
        }
        [Test] public void ModifierAndControlHandlesAreRemappedWithoutLosingOwnership()
        {
            var stat=Asset<CombatStatDefinition>(); var stats=a.gameObject.AddComponent<CombatStats>(); stats.SetValue(stat,10);
            var item=new object(); references.RegisterSource("item",item); stats.TryAdd(new StatModifier(stat,StatModifierOperation.Flat,5),item,out var oldModifier);
            var control=a.gameObject.AddComponent<CombatControl>(); control.TryRestrict(CombatRestriction.Move,out var oldControl); var saved=Capture();
            stats.ClearModifiers(); control.ResetRestrictions(); var result=Restore(saved);
            Assert.That(oldModifier.Remove(),Is.False); Assert.That(oldControl.Remove(),Is.False); Assert.That(control.Allows(CombatRestriction.Move),Is.False);
            Assert.That(result.Modifiers[0].SavedGroup,Is.EqualTo(oldModifier.SaveKey)); Assert.That(result.Controls[0].SavedKey,Is.EqualTo(oldControl.SaveKey));
            Assert.That(stats.RemoveSource(item),Is.EqualTo(1)); Assert.That(result.Controls[0].Handle.Remove(),Is.True); Assert.That(control.Allows(CombatRestriction.Move),Is.True);
        }
        [Test] public void ActiveCastResumesWithUnpaidCostAndRemappedTargetIdentity()
        {
            var resources=Resources(a,out var mana); var action=Asset<AbilityAttackAction>(); action.DirectToTarget=true; action.Attack=Asset<AttackDefinition>(); action.Attack.Damage=10;
            var definition=Asset<AbilityDefinition>(); definition.TargetMode=AbilityTargetMode.Unit; definition.CastTime=1.5f; definition.MaximumCharges=2; definition.Cooldown=3; definition.ChargeRecovery=4;
            definition.Costs=new[] { new ResourceCost(mana,20) }; definition.Actions=new CombatAbilityAction[] { action };
            var abilities=a.gameObject.AddComponent<CombatAbilities>(); abilities.Configure(new[] { definition }); abilities.TryActivate(definition,default,b); abilities.Advance(.5f);
            var saved=Capture(); abilities.Cancel(); resources.TrySpend(new[] { new ResourceCost(mana,30) }); b.ResetToFull(); Restore(saved);
            Assert.That(abilities.IsCasting,Is.True); Assert.That(abilities.CastRemaining,Is.EqualTo(1)); abilities.Advance(1); resources.TryGet(mana,out var value);
            Assert.That(b.Current,Is.EqualTo(90)); Assert.That(value.Current,Is.EqualTo(80)); Assert.That(abilities.GetAbility(0).Charges,Is.EqualTo(1)); Assert.That(abilities.GetAbility(0).Cooldown,Is.EqualTo(3));
        }
        [Test] public void TargetReactionCooldownIsRelativeAndReferencesRestoredVictim()
        {
            var clock=new ManualCombatClock(); using(new CombatExecutionScope(clock:clock))
            {
                a.ApplyDamage(new DamageInfo(50)); var heal=Asset<HealingDefinition>(); heal.Terms=new[] { new HealingTerm(HealingBasis.Fixed,5) };
                var action=Asset<ReactionHealingAction>(); action.Healing=heal; var rule=Asset<ReactionDefinition>(); rule.Trigger=ReactionTrigger.DamageDealt; rule.Target=ReactionTarget.Owner; rule.CooldownScope=ReactionCooldownScope.Target; rule.Cooldown=10; rule.Actions=new CombatReactionAction[] { action };
                a.gameObject.AddComponent<CombatReactions>().Configure(new[] { rule }); b.ApplyDamage(new DamageInfo(1,a.gameObject)); var saved=Capture(); clock.Advance(100); Restore(saved);
                b.ApplyDamage(new DamageInfo(1,a.gameObject)); Assert.That(a.Current,Is.EqualTo(55)); clock.Advance(10); b.ApplyDamage(new DamageInfo(1,a.gameObject)); Assert.That(a.Current,Is.EqualTo(60));
            }
        }
        [Test] public void BuildupRestoresValueWithoutThresholdActions()
        {
            var statuses=b.gameObject.AddComponent<StatusController>(); var status=Asset<StatusDefinition>(); var action=Asset<ReactionStatusAction>(); action.Status=status;
            var definition=Asset<BuildupDefinition>(); definition.Actions=new CombatReactionAction[] { action }; definition.Threshold=20; definition.DecayPerSecond=0;
            var meters=b.gameObject.AddComponent<CombatBuildup>(); meters.Configure(new[] { definition }); meters.Add(definition,15,a.gameObject); var saved=Capture(); meters.Add(definition,5,a.gameObject);
            Assert.That(statuses.GetStacks(status),Is.EqualTo(1)); Restore(saved); Assert.That(statuses.GetStacks(status),Is.Zero); Assert.That(meters.GetMeter(0).Value,Is.EqualTo(15));
            meters.Add(definition,5,a.gameObject); Assert.That(statuses.GetStacks(status),Is.EqualTo(1));
        }
        [Test] public void KillContributionsSurviveRestoreWithoutDuplicateRewards()
        {
            var c=Actor("c"); var profile=Asset<AttributionDefinition>(); profile.Killer=KillCreditMode.HighestDamage;
            var attribution=b.gameObject.AddComponent<CombatAttribution>(); attribution.Configure(profile); b.ApplyDamage(new DamageInfo(60,a.gameObject)); b.ApplyDamage(new DamageInfo(10,c.gameObject));
            var saved=Capture(); var rewards=0; KillCredit credit=default; attribution.Credited+=value=> { rewards++; credit=value; }; Restore(saved);
            Assert.That(rewards,Is.Zero); b.ApplyDamage(new DamageInfo(30,c.gameObject)); Assert.That(rewards,Is.EqualTo(1)); Assert.That(credit.Killer.Id,Is.EqualTo(a.Identity.Id));
        }
        [Test] public void SuccessfulRestoreCancelsFieldsWithoutExitDamageButFailedRestoreDoesNot()
        {
            var saved=Capture(); var areaObject=new GameObject("Field"); cleanup.Add(areaObject); var field=areaObject.AddComponent<CombatAreaTrigger>(); field.AutomaticAdvance=false;
            var recipe=Asset<AreaTriggerDefinition>(); recipe.Area=Asset<AreaDefinition>(); recipe.ExitAttack=Asset<AttackDefinition>(); recipe.ExitAttack.Damage=30;
            b.gameObject.AddComponent<SphereCollider>(); Physics.SyncTransforms(); field.Begin(recipe,a.gameObject);
            saved.Version=99; Assert.That(CombatSaveSystem.TryRestore(saved,catalog,references,out _,out _),Is.False); Assert.That(field.IsRunning,Is.True);
            saved.Version=1; Restore(saved); Assert.That(field.IsRunning,Is.False); Assert.That(b.Current,Is.EqualTo(100));
        }
        [Test] public void LoadDuringDamageAndReentrantLoadFromNotificationsAreRejected()
        {
            var saved=Capture(); var accepted=true; b.Damaged+=hit=>accepted=CombatSaveSystem.TryRestore(saved,catalog,references,out _,out _);
            b.ApplyDamage(new DamageInfo(10)); Assert.That(accepted,Is.False); Assert.That(b.Current,Is.EqualTo(90));
            var captures=0; a.StateRestored+=actor=>
            {
                if(CombatSaveSystem.TryCapture(catalog,references,out _,out _)) captures++;
                if(CombatSaveSystem.TryRestore(saved,catalog,references,out _,out _)) captures++;
            };
            Restore(saved); Assert.That(captures,Is.Zero); Assert.That(b.Current,Is.EqualTo(100));
            Assert.That(CombatSaveSystem.TryCapture(catalog,references,out _,out _),Is.True);
        }
        [Test] public void BodyPartVisibilityAndCoreLinkRecoverWithoutBreakEvent()
        {
            var collider=b.gameObject.AddComponent<SphereCollider>(); var definition=Asset<BodyPartDefinition>(); definition.Health=Asset<HealthDefinition>(); definition.Health.MaximumHealth=20; definition.OnBreak=BodyPartBreakBehavior.DisableObject;
            var part=b.gameObject.AddComponent<CombatBodyPart>(); part.Configure(definition,a); part.ApplyDamage(new DamageInfo(5)); var saved=Capture(); part.ApplyDamage(new DamageInfo(30));
            Assert.That(b.gameObject.activeSelf,Is.False); var breaks=0; part.Broken+=_=>breaks++; Restore(saved);
            Assert.That(b.gameObject.activeSelf,Is.True); Assert.That(collider.enabled,Is.True); Assert.That(part.IsBroken,Is.False); Assert.That(part.Core,Is.EqualTo(a)); Assert.That(b.Current,Is.EqualTo(15)); Assert.That(breaks,Is.Zero);
        }
        [Test] public void CatalogRejectsAmbiguousIdsAndMissingOwnershipFailsCapture()
        {
            var stat=Asset<CombatStatDefinition>(); Assert.That(catalog.Configure(new[] { new CombatCatalogEntry("same",stat),new CombatCatalogEntry("same",Asset<CombatStatDefinition>()) }),Is.False);
            var stats=a.gameObject.AddComponent<CombatStats>(); stats.SetValue(stat,10); stats.TryAdd(new StatModifier(stat,StatModifierOperation.Flat,5),new object(),out _); Catalog();
            Assert.That(CombatSaveSystem.TryCapture(catalog,references,out _,out var error),Is.False); Assert.That(error,Does.Contain("not registered"));
        }
        [Test] public void SharedFormulaKeepsCriticalTypeCapAndLiveSourceAfterJson()
        {
            var stat=Asset<CombatStatDefinition>(); var stats=a.gameObject.AddComponent<CombatStats>(); stats.SetValue(stat,10);
            var fire=Asset<DamageTypeDefinition>(); var formula=Asset<DamageFormulaDefinition>();
            formula.Terms=new[] { new DamageFormulaTerm(fire,StatusDamageBasis.Fixed,5),
                new DamageFormulaTerm(fire,StatusDamageBasis.SourceStat,1) { Stat=stat,Sampling=StatusValueSampling.EveryTick,MaximumDamage=12 } };
            var effect=Asset<PeriodicDamageEffect>(); effect.Formula=formula; effect.CriticalChance=1; effect.CriticalMultiplier=2;
            var status=Asset<StatusDefinition>(); status.TickEffects=new StatusTickEffect[] { effect }; var statuses=b.gameObject.AddComponent<StatusController>(); statuses.Apply(status,a.gameObject);
            var saved=Capture(); var random=new CountingRandom(); using(new CombatExecutionScope(random:random)) Restore(saved); Assert.That(random.Calls,Is.Zero);
            stats.SetValue(stat,20); DamageResult tick=default; b.Damaged+=result=>tick=result; statuses.Advance(1);
            Assert.That(tick.Damage.IsCritical,Is.True); Assert.That(tick.AppliedAmount,Is.EqualTo(22)); Assert.That(tick.Damage.Parts[0].Type,Is.EqualTo(fire));
        }
        [Test] public void DownedStateRestoresWithoutDeathOrReviveCallbacks()
        {
            var profile=Asset<HealthDefinition>(); profile.LifeRules=Asset<LifeRulesDefinition>(); profile.LifeRules.AllowDowned=true; b.ApplyDefinition(profile);
            b.ApplyDamage(new DamageInfo(1000)); var saved=Capture(); b.Revive(); var callbacks=0; b.Died+=actor=>callbacks++; b.Revived+=actor=>callbacks++;
            Restore(saved); Assert.That(b.LifeState,Is.EqualTo(CombatLifeState.Downed)); Assert.That(b.Current,Is.Zero); Assert.That(callbacks,Is.Zero);
        }
    }
}
