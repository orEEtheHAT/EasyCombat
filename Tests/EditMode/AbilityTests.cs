using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AbilityTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health owner,target;
        private CombatAbilities abilities;
        private CombatResources resources;
        private ResourceDefinition mana;
        private AbilityDefinition ability;
        private AbilityAttackAction action;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private Health Actor(string name) { var go=new GameObject(name); cleanup.Add(go); return go.AddComponent<Health>(); }
        [SetUp] public void Setup()
        {
            owner=Actor("Caster"); target=Actor("Victim"); target.transform.position=Vector3.forward*5;
            mana=Asset<ResourceDefinition>(); var profile=Asset<ResourceProfile>(); profile.Resources=new[] { mana }; resources=owner.gameObject.AddComponent<CombatResources>(); resources.Configure(profile);
            action=Asset<AbilityAttackAction>(); action.Attack=Asset<AttackDefinition>(); action.Attack.Damage=10; action.DirectToTarget=true;
            ability=Asset<AbilityDefinition>(); ability.TargetMode=AbilityTargetMode.Unit; ability.Costs=new[] { new ResourceCost(mana,20) }; ability.Actions=new CombatAbilityAction[] { action };
            abilities=owner.gameObject.AddComponent<CombatAbilities>(); Assert.That(abilities.Configure(new[] { ability }),Is.True);
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private float Mana { get { resources.TryGet(mana,out var view); return view.Current; } }
        [Test] public void CastCommitsCostChargeAndDamageOnlyOnRelease()
        {
            ability.CastTime=1; Assert.That(abilities.TryActivate(ability,target.transform.position,target),Is.True);
            Assert.That(Mana,Is.EqualTo(100)); Assert.That(abilities.GetAbility(0).Charges,Is.EqualTo(1)); abilities.Advance(.5f); Assert.That(target.Current,Is.EqualTo(100));
            abilities.Advance(.5f); Assert.That(Mana,Is.EqualTo(80)); Assert.That(target.Current,Is.EqualTo(90)); Assert.That(abilities.GetAbility(0).Charges,Is.Zero);
            abilities.Advance(1); Assert.That(abilities.GetAbility(0).Charges,Is.EqualTo(1));
        }
        [Test] public void CancellationAndTargetReuseDoNotSpendResources()
        {
            ability.CastTime=1; abilities.TryActivate(ability,default,target); Assert.That(abilities.Cancel(),Is.True); abilities.Advance(2);
            Assert.That(target.Current,Is.EqualTo(100)); Assert.That(Mana,Is.EqualTo(100));
            abilities.TryActivate(ability,default,target); target.ResetToFull(); abilities.Advance(1); Assert.That(target.Current,Is.EqualTo(100)); Assert.That(Mana,Is.EqualTo(100));
        }
        [Test] public void FrozenAimCannotBypassUnitRangeValidation()
        {
            ability.FollowTargetDuringCast=false; ability.Range=10; target.transform.position=Vector3.forward*20;
            Assert.That(abilities.TryActivate(ability,Vector3.zero,target,out var reason),Is.False); Assert.That(reason,Is.EqualTo(AbilityRejection.Range));
            target.transform.position=Vector3.forward*5; ability.CastTime=1; abilities.TryActivate(ability,Vector3.zero,target);
            target.transform.position=Vector3.forward*20; abilities.Advance(1); Assert.That(Mana,Is.EqualTo(100));
        }
        [Test] public void LargeStepAppliesRemainderToNewCooldownAndRecharge()
        {
            ability.CastTime=1; ability.Cooldown=4; ability.ChargeRecovery=5; abilities.TryActivate(ability,default,target); abilities.Advance(3);
            Assert.That(abilities.GetAbility(0).Cooldown,Is.EqualTo(2)); Assert.That(abilities.GetAbility(0).RechargeRemaining,Is.EqualTo(3));
        }
        [Test] public void ChargesRecoverSequentiallyAndRemainActorOwned()
        {
            ability.MaximumCharges=3; ability.Cooldown=0; ability.ChargeRecovery=2; abilities.Configure(new[] { ability });
            abilities.TryActivate(ability,default,target); abilities.TryActivate(ability,default,target); abilities.TryActivate(ability,default,target);
            Assert.That(abilities.TryActivate(ability,default,target,out var reason),Is.False); Assert.That(reason,Is.EqualTo(AbilityRejection.Charges));
            abilities.Advance(5); Assert.That(abilities.GetAbility(0).Charges,Is.EqualTo(2)); Assert.That(abilities.GetAbility(0).RechargeRemaining,Is.EqualTo(1));
            var other=Actor("Other").gameObject.AddComponent<CombatAbilities>(); other.Configure(new[] { ability }); Assert.That(other.GetAbility(0).Charges,Is.EqualTo(3));
        }
        [Test] public void ResourceNotificationObservesCompletedDamageAndCannotReenter()
        {
            var seen=0f; var reentered=true; resources.Changed+=_=> { seen=target.Current; reentered=abilities.TryActivate(ability,default,target); };
            abilities.TryActivate(ability,default,target); Assert.That(seen,Is.EqualTo(90)); Assert.That(reentered,Is.False); Assert.That(Mana,Is.EqualTo(80));
        }
        [Test] public void DuplicateCostsAreAtomicAndRecheckedAtRelease()
        {
            ability.Costs=new[] { new ResourceCost(mana,30),new ResourceCost(mana,30) }; ability.CastTime=1;
            abilities.TryActivate(ability,default,target); resources.TrySpend(new[] { new ResourceCost(mana,50) }); abilities.Advance(1);
            Assert.That(Mana,Is.EqualTo(50)); Assert.That(target.Current,Is.EqualTo(100)); Assert.That(abilities.GetAbility(0).Charges,Is.EqualTo(1));
        }
        [Test] public void CastRestrictionInterruptsWindup()
        {
            ability.CastTime=1; abilities.TryActivate(ability,default,target); var control=owner.gameObject.AddComponent<CombatControl>(); control.TryRestrict(CombatRestriction.Cast,out var handle);
            abilities.Advance(.2f); Assert.That(abilities.IsCasting,Is.False); Assert.That(Mana,Is.EqualTo(100)); handle.Remove();
        }
        [Test] public void ResetInStartedCallbackCancelsWithoutCost()
        {
            abilities.Changed+=e=> { if(e.Kind==AbilityEventKind.Started) owner.ResetToFull(); };
            abilities.TryActivate(ability,default,target); Assert.That(Mana,Is.EqualTo(100)); Assert.That(abilities.IsCasting,Is.False); Assert.That(target.Current,Is.EqualTo(100));
        }
        [Test] public void MultipleActionsCannotAffectNewPooledTargetLife()
        {
            ability.Actions=new CombatAbilityAction[] { action,action }; target.Damaged+=_=>target.ResetToFull();
            var hits=0; target.Resolved+=_=>hits++;
            abilities.TryActivate(ability,default,target); Assert.That(hits,Is.EqualTo(1)); Assert.That(target.Current,Is.EqualTo(100));
        }
        [Test] public void AttackKeepsAbilityAncestryAndWeaponTags()
        {
            var tag=Asset<CombatTagDefinition>(); action.Attack.Tags=new[] { tag }; DamageOrigin hit=default; long cast=0;
            target.Damaged+=result=>hit=result.Damage.Origin; abilities.Changed+=e=> { if(e.Kind==AbilityEventKind.Started) cast=e.CastId; };
            abilities.TryActivate(ability,default,target); Assert.That(hit.Action.RootId,Is.EqualTo(cast)); Assert.That(hit.Action.ParentId,Is.EqualTo(cast)); Assert.That(hit.Action.Tags.Contains(tag),Is.True);
        }
        [Test] public void MissingProjectilePoolRejectsBeforePayment()
        {
            action.DirectToTarget=false; action.Attack.Delivery=DamageDelivery.Projectile;
            Assert.That(abilities.TryActivate(ability,default,target,out var reason),Is.False); Assert.That(reason,Is.EqualTo(AbilityRejection.Delivery)); Assert.That(Mana,Is.EqualTo(100));
        }
        [Test] public void UnitTeamFilterIsCheckedBeforeCost()
        {
            ability.UnitTargets=AreaTargetFilter.Enemies; owner.gameObject.AddComponent<CombatTeam>().Team=1; target.gameObject.AddComponent<CombatTeam>().Team=1;
            Assert.That(abilities.TryActivate(ability,default,target),Is.False); target.GetComponent<CombatTeam>().Team=2;
            Assert.That(abilities.TryActivate(ability,default,target),Is.True); Assert.That(Mana,Is.EqualTo(80));
        }
        [Test] public void ReusableStatusEffectTargetsSelfWithoutTriggerDamage()
        {
            var statuses=owner.gameObject.AddComponent<StatusController>(); var status=Asset<StatusDefinition>(); var effect=Asset<ReactionStatusAction>(); effect.Status=status;
            var step=Asset<AbilityEffectAction>(); step.Effect=effect; step.AffectCaster=true; ability.Actions=new CombatAbilityAction[] { step }; ability.TargetMode=AbilityTargetMode.Self;
            Assert.That(abilities.TryActivate(ability,default),Is.True); Assert.That(statuses.GetStacks(status),Is.EqualTo(1));
        }
    }
}
