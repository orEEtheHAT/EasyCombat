using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class TestCostProvider : CombatCostProvider
    {
        public ResourceDefinition Resource;
        public float Balance=100;
        public bool Publishing;
        public Action Completed;
        public override bool CanSpend(ReadOnlySpan<ResourceCost> first,ReadOnlySpan<ResourceCost> additional=default)
        {
            if(Publishing) return false;
            var total=0f;
            foreach(var cost in first) { if(!cost.IsValid || cost.Resource!=Resource) return false; total+=cost.Amount; }
            foreach(var cost in additional) { if(!cost.IsValid || cost.Resource!=Resource) return false; total+=cost.Amount; }
            return !float.IsInfinity(total) && total<=Balance;
        }
        public override bool TrySpend(ReadOnlySpan<ResourceCost> costs,out CombatCostTransaction transaction)
        {
            transaction=null; if(!CanSpend(costs)) return false;
            var amount=0f; foreach(var cost in costs) amount+=cost.Amount;
            Balance-=amount; transaction=new Payment(this,amount); return true;
        }
        private sealed class Payment : CombatCostTransaction
        {
            private TestCostProvider owner;
            private readonly float amount;
            public Payment(TestCostProvider provider,float value) { owner=provider; amount=value; }
            public override void Complete(bool committed)
            {
                var current=owner; owner=null; if(current==null) return;
                if(!committed) { current.Balance+=amount; return; }
                current.Publishing=true;
                try { current.Completed?.Invoke(); } finally { current.Publishing=false; }
            }
        }
    }
    public sealed class CostProviderTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health owner,target;
        private CombatResources legacy;
        private TestCostProvider provider;
        private CombatAbilities abilities;
        private ResourceDefinition mana;
        private AbilityDefinition ability;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private Health Actor(string name) { var a=new GameObject(name); cleanup.Add(a); return a.AddComponent<Health>(); }
        [SetUp] public void Setup()
        {
            owner=Actor("Payer"); target=Actor("Target"); mana=Asset<ResourceDefinition>();
            var profile=Asset<ResourceProfile>(); profile.Resources=new[] { mana };
            legacy=owner.gameObject.AddComponent<CombatResources>(); legacy.Configure(profile);
            provider=owner.gameObject.AddComponent<TestCostProvider>(); provider.Resource=mana;
            var action=Asset<AbilityAttackAction>(); action.Attack=Asset<AttackDefinition>(); action.Attack.Damage=10; action.DirectToTarget=true;
            ability=Asset<AbilityDefinition>(); ability.TargetMode=AbilityTargetMode.Unit;
            ability.Costs=new[] { new ResourceCost(mana,20) }; ability.Actions=new CombatAbilityAction[] { action };
            abilities=owner.gameObject.AddComponent<CombatAbilities>(); abilities.AutomaticAdvance=false; abilities.Configure(new[] { ability });
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void OptionalProviderIsTheOnlyBalanceAndPublishesAfterDamage()
        {
            var observed=0f; var reentry=true;
            provider.Completed=()=> { observed=target.Current; reentry=abilities.TryActivate(ability,default,target); };
            Assert.That(abilities.TryActivate(ability,default,target),Is.True);
            Assert.That(provider.Balance,Is.EqualTo(80)); Assert.That(observed,Is.EqualTo(90)); Assert.That(reentry,Is.False);
            legacy.TryGet(mana,out var value); Assert.That(value.Current,Is.EqualTo(100));
        }
        [Test] public void DisabledProviderDoesNotFallbackToLegacyBalance()
        {
            provider.enabled=false;
            Assert.That(abilities.TryActivate(ability,default,target,out var reason),Is.False);
            Assert.That(reason,Is.EqualTo(AbilityRejection.Resources)); Assert.That(target.Current,Is.EqualTo(100));
            Assert.That(provider.Balance,Is.EqualTo(100));
        }
        [Test] public void CancellationAndReleaseRecheckPreserveProviderBalance()
        {
            ability.CastTime=1;
            Assert.That(abilities.TryActivate(ability,default,target),Is.True); Assert.That(abilities.Cancel(),Is.True);
            Assert.That(provider.Balance,Is.EqualTo(100));
            Assert.That(abilities.TryActivate(ability,default,target),Is.True); provider.Balance=10; abilities.Advance(1);
            Assert.That(provider.Balance,Is.EqualTo(10)); Assert.That(target.Current,Is.EqualTo(100));
        }
        [Test] public void PaidDefenseUsesProviderAndPublishesCommittedHealth()
        {
            var block=Asset<BlockLayer>(); block.Arc=360; block.Reduction=.5f; block.Costs=new[] { new ResourceCost(mana,10) };
            var health=Asset<HealthDefinition>(); health.Defense=Asset<DefenseProfile>(); health.Defense.Layers=new[] { new DefenseSlot(block) };
            owner.ApplyDefinition(health);
            var observed=0f; provider.Completed=()=>observed=owner.Current;
            Assert.That(owner.ApplyDamage(new DamageInfo(40)).AppliedAmount,Is.EqualTo(20));
            Assert.That(provider.Balance,Is.EqualTo(90)); Assert.That(observed,Is.EqualTo(80));
        }
    }
}
