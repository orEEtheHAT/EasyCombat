using System;
using System.Collections.Generic;
using EasyResources;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
using WalletResource=EasyResources.ResourceDefinition;
using WalletCost=EasyResources.ResourceCost;

namespace EasyCombat.Integrations.Tests
{
    public sealed class PartiallyFailedCostAction : CombatAbilityAction
    {
        public override bool IsValid=>true;
        public override bool CanExecute(in AbilityContext context)=>true;
        public override bool Execute(in AbilityContext context) { context.Target.ApplyDamage(new DamageInfo(5)); return false; }
    }

    public sealed class InvalidMappedDefense : DefenseLayerDefinition
    {
        public override DamageAmounts Reduce(in DefenseContext context,DamageAmounts input,ref DefenseLayerState state)
        { var invalid=new DamageAmounts(); invalid.Add(null,-1); return invalid; }
    }

    public sealed class EasyResourcesCostTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly List<ScriptableObject> combatDefinitions=new List<ScriptableObject>();
        private readonly List<ResourceStore> stores=new List<ResourceStore>();
        private Health owner,target;
        private ResourceDefinition mana,ammo;
        private WalletResource energy,cartridges;
        private ResourceStore store;
        private CombatResourceMap map;
        private EasyResourcesCostProvider provider;
        private T Asset<T>() where T:ScriptableObject
        { var a=ScriptableObject.CreateInstance<T>(); a.name=typeof(T).Name+cleanup.Count; cleanup.Add(a); combatDefinitions.Add(a); return a; }
        private Health Actor(string name,Vector3 at)
        { var go=new GameObject(name); cleanup.Add(go); go.transform.position=at; return go.AddComponent<Health>(); }
        private ResourceStore Wallet(long amount=10000)
        { var wallet=new ResourceStore(new WalletCost(new ResourceAmount(energy,amount),new ResourceAmount(cartridges,10))); stores.Add(wallet); return wallet; }
        private ResourceCost[] Costs(float amount=20)=>new[] { new ResourceCost(mana,amount) };
        private CombatAbilities Ability(out AbilityDefinition ability,float cost=20)
        {
            var attack=Asset<AttackDefinition>(); attack.Damage=10;
            var action=Asset<AbilityAttackAction>(); action.Attack=attack; action.DirectToTarget=true;
            ability=Asset<AbilityDefinition>(); ability.TargetMode=AbilityTargetMode.Unit; ability.Costs=Costs(cost); ability.Actions=new CombatAbilityAction[] { action };
            var abilities=owner.gameObject.AddComponent<CombatAbilities>(); abilities.AutomaticAdvance=false;
            Assert.That(abilities.Configure(new[] { ability }),Is.True); return abilities;
        }
        [SetUp] public void Setup()
        {
            owner=Actor("Mapped payer",new Vector3(0,4600,0)); target=Actor("Mapped target",new Vector3(0,4600,5));
            mana=Asset<ResourceDefinition>(); ammo=Asset<ResourceDefinition>();
            energy=WalletResource.Create("Energy",decimalPlaces:2); cartridges=WalletResource.Create("Cartridges"); cleanup.Add(energy); cleanup.Add(cartridges);
            map=Asset<CombatResourceMap>(); map.Entries=new[] { new CombatResourceMap.Entry(mana,energy),new CombatResourceMap.Entry(ammo,cartridges) };
            store=Wallet(); provider=owner.gameObject.AddComponent<EasyResourcesCostProvider>(); provider.Bind(store,map);
        }
        [TearDown] public void Cleanup()
        {
            foreach(var wallet in stores) wallet.Dispose(); stores.Clear();
            for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]);
            cleanup.Clear(); combatDefinitions.Clear();
        }

        [Test] public void DecimalMappingAndDuplicateAliasesAreExact()
        {
            map.Entries=new[] { new CombatResourceMap.Entry(mana,energy),new CombatResourceMap.Entry(ammo,energy) };
            Assert.That(map.TryConvert(new[] { new ResourceCost(mana,.1f),new ResourceCost(mana,1.25f) },new[] { new ResourceCost(ammo,.15f) },out var cost,out var error),Is.True,error);
            Assert.That(cost.GetAmount(energy),Is.EqualTo(150));
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.MaxValue)] [TestCase(.001f)]
        public void InvalidFractionOrAmountRejectsBeforeAnyBalanceChange(float amount)
        {
            var changes=0; store.Changed+=()=>changes++;
            Assert.That(provider.TrySpend(Costs(amount),out var payment),Is.False); Assert.That(payment,Is.Null);
            Assert.That(store.GetTotal(energy),Is.EqualTo(10000)); Assert.That(store.ActiveReservationCount,Is.Zero); Assert.That(changes,Is.Zero);
            Assert.That(provider.LastError,Is.Not.Null.And.Not.Empty);
        }
        [Test] public void DuplicateRowsCannotOverflowTheIntegerLedger()
        {
            map.Entries=new[] { new CombatResourceMap.Entry(mana,cartridges) };
            Assert.That(map.TryConvert(new[] { new ResourceCost(mana,5e18f),new ResourceCost(mana,5e18f) },default,out _,out var error),Is.False);
            Assert.That(error,Does.Contain("range"));
        }
        [Test] public void UnknownOrAmbiguousMappingsRejectInsteadOfUsingLegacyFunds()
        {
            var profile=Asset<ResourceProfile>(); profile.Resources=new[] { mana }; owner.gameObject.AddComponent<CombatResources>().Configure(profile);
            map.Entries=Array.Empty<CombatResourceMap.Entry>(); Assert.That(provider.CanSpend(Costs()),Is.False);
            map.Entries=new[] { new CombatResourceMap.Entry(mana,energy),new CombatResourceMap.Entry(mana,cartridges) };
            Assert.That(provider.TrySpend(Costs(),out _),Is.False); Assert.That(store.GetTotal(energy),Is.EqualTo(10000));
        }
        [Test] public void MultiResourceInsufficiencyHasNoPartialReservationOrNotification()
        {
            var changes=0; store.Changed+=()=>changes++;
            var costs=new[] { new ResourceCost(mana,20),new ResourceCost(ammo,11) };
            Assert.That(provider.TrySpend(costs,out _),Is.False);
            Assert.That(store.GetAvailable(energy),Is.EqualTo(10000)); Assert.That(store.GetAvailable(cartridges),Is.EqualTo(10));
            Assert.That(store.ActiveReservationCount,Is.Zero); Assert.That(changes,Is.Zero);
        }
        [Test] public void FailedAdmissionRefundsAllResourcesWithoutIntermediateEvents()
        {
            var changes=0; store.Changed+=()=>changes++; var revision=store.Revision;
            Assert.That(provider.TrySpend(new[] { new ResourceCost(mana,20),new ResourceCost(ammo,3) },out var payment),Is.True);
            Assert.That(store.GetAvailable(energy),Is.EqualTo(8000)); Assert.That(store.GetAvailable(cartridges),Is.EqualTo(7)); Assert.That(changes,Is.Zero);
            payment.Complete(false); payment.Complete(true); payment.Complete(false);
            Assert.That(store.GetAvailable(energy),Is.EqualTo(10000)); Assert.That(store.GetTotal(cartridges),Is.EqualTo(10));
            Assert.That(store.ActiveReservationCount,Is.Zero); Assert.That(changes,Is.Zero); Assert.That(store.Revision,Is.EqualTo(revision));
        }
        [Test] public void NestedPaymentsPublishOnceAndCancellingParentDoesNotRefundCommittedChild()
        {
            var changes=0; store.Changed+=()=>changes++;
            Assert.That(provider.TrySpend(Costs(20),out var parent),Is.True);
            Assert.That(provider.TrySpend(Costs(30),out var child),Is.True);
            child.Complete(true); Assert.That(changes,Is.Zero);
            parent.Complete(false); Assert.That(changes,Is.EqualTo(1)); Assert.That(store.GetTotal(energy),Is.EqualTo(7000));
            Assert.That(store.GetReserved(energy),Is.Zero);
        }
        [Test] public void CompletionNotificationsRejectResourceReentryAndCannotChargeTwice()
        {
            var changes=0; var reentered=true;
            store.Changed+=()=> { changes++; reentered=provider.TrySpend(Costs(1),out _); };
            Assert.That(provider.TrySpend(Costs(),out var payment),Is.True); payment.Complete(true); payment.Complete(true);
            Assert.That(reentered,Is.False); Assert.That(changes,Is.EqualTo(1)); Assert.That(store.GetTotal(energy),Is.EqualTo(8000));
        }
        [Test] public void CompletionSurvivesProviderDestructionAndNeverUsesReboundWallet()
        {
            Assert.That(provider.TrySpend(Costs(),out var payment),Is.True);
            var replacement=Wallet(); provider.Bind(replacement,map); Object.DestroyImmediate(provider); payment.Complete(true); payment.Complete(true);
            Assert.That(store.GetTotal(energy),Is.EqualTo(8000)); Assert.That(replacement.GetTotal(energy),Is.EqualTo(10000));
        }
        [Test] public void AuthoritativeRestoreInvalidatesOldPaymentWithoutSpendingNewLife()
        {
            var saved=store.CaptureState(); Assert.That(provider.TrySpend(Costs(),out var payment),Is.True);
            Assert.Throws<InvalidOperationException>(()=>store.CaptureState(true));
            store.RestoreState(saved,new[] { energy,cartridges },new ResourceRestoreOptions(replaceActiveReservations:true));
            var revision=store.Revision; payment.Complete(true); payment.Complete(false);
            Assert.That(store.GetTotal(energy),Is.EqualTo(10000)); Assert.That(store.ActiveReservationCount,Is.Zero); Assert.That(store.Revision,Is.EqualTo(revision));
        }
        [Test] public void DisposedStoreMakesOldCompletionHarmless()
        {
            Assert.That(provider.TrySpend(Costs(),out var payment),Is.True); store.Dispose();
            Assert.DoesNotThrow(()=>payment.Complete(true)); Assert.DoesNotThrow(()=>payment.Complete(false));
        }
        [Test] public void AbilityCancellationAndDeliveryRejectionDoNotCharge()
        {
            var abilities=Ability(out var definition); definition.CastTime=1;
            Assert.That(abilities.TryActivate(definition,default,target),Is.True); Assert.That(abilities.Cancel(),Is.True);
            definition.CastTime=0; var action=(AbilityAttackAction)definition.Actions[0]; action.DirectToTarget=false; action.Attack.Delivery=DamageDelivery.Projectile;
            Assert.That(abilities.TryActivate(definition,default,target,out var reason),Is.False); Assert.That(reason,Is.EqualTo(AbilityRejection.Delivery));
            Assert.That(store.GetTotal(energy),Is.EqualTo(10000)); Assert.That(store.ActiveReservationCount,Is.Zero);
        }
        [Test] public void IrreversibleExecutionFailureKeepsCostAndPublishesAfterFailedOutcome()
        {
            var abilities=Ability(out var definition); definition.Actions=new CombatAbilityAction[] { Asset<PartiallyFailedCostAction>() };
            var failed=false; var observed=false;
            abilities.Changed+=value=> { if(value.Kind==AbilityEventKind.Failed) failed=true; };
            store.Changed+=()=>observed=failed && target.Current==95;
            Assert.That(abilities.TryActivate(definition,default,target),Is.True);
            Assert.That(observed,Is.True); Assert.That(store.GetTotal(energy),Is.EqualTo(8000));
        }
        private CombatBeam Beam(out BeamDefinition recipe,float pulseCost=20)
        {
            target.gameObject.AddComponent<SphereCollider>().radius=.5f;
            recipe=Asset<BeamDefinition>(); recipe.Attack=Asset<AttackDefinition>(); recipe.Attack.Damage=10; recipe.Duration=.4f; recipe.Interval=.1f;
            recipe.PulseCosts=Costs(pulseCost); recipe.PulseImmediately=false;
            var beam=owner.gameObject.AddComponent<CombatBeam>(); beam.AutomaticAdvance=false; Physics.SyncTransforms(); return beam;
        }
        [Test] public void BeamStopsBeforeAnUnpaidPulse()
        {
            var beam=Beam(out var recipe,40); Assert.That(beam.Begin(recipe),Is.True); beam.Advance(.4f);
            Assert.That(store.GetTotal(energy),Is.EqualTo(2000)); Assert.That(target.Current,Is.EqualTo(80)); Assert.That(beam.IsRunning,Is.False);
        }
        [Test] public void BeamPaymentObserverCannotAdmitAnotherAbilityOnTheSameWallet()
        {
            var beam=Beam(out var recipe); var abilities=Ability(out var ability);
            var admitted=true; var started=0;
            abilities.Changed+=value=> { if(value.Kind==AbilityEventKind.Started) started++; };
            store.Changed+=()=>admitted=abilities.TryActivate(ability,default,target);
            Assert.That(beam.Begin(recipe),Is.True); beam.Advance(.1f);
            Assert.That(admitted,Is.False); Assert.That(started,Is.Zero); Assert.That(target.Current,Is.EqualTo(90));
        }
        [Test] public void AbilityAndImmediateBeamShareBudgetAndNotifyOnlyAfterBothComplete()
        {
            Beam(out var recipe,60); recipe.PulseImmediately=true;
            var abilities=Ability(out var definition,50); var action=Asset<AbilityBeamAction>(); action.Beam=recipe; definition.Actions=new CombatAbilityAction[] { action };
            Assert.That(abilities.TryActivate(definition,default,target,out var reason),Is.False); Assert.That(reason,Is.EqualTo(AbilityRejection.Resources));
            Assert.That(store.GetTotal(energy),Is.EqualTo(10000)); recipe.PulseCosts=Costs(20);
            var committed=false; var changes=0;
            abilities.Changed+=value=> { if(value.Kind==AbilityEventKind.Committed) committed=true; };
            store.Changed+=()=> { changes++; Assert.That(committed,Is.True); Assert.That(target.Current,Is.EqualTo(90)); Assert.That(store.GetTotal(energy),Is.EqualTo(3000)); };
            Assert.That(abilities.TryActivate(definition,default,target),Is.True); Assert.That(changes,Is.EqualTo(1));
        }
        private void Defense(bool invalid=false)
        {
            target.gameObject.AddComponent<EasyResourcesCostProvider>().Bind(store,map);
            var block=Asset<BlockLayer>(); block.Arc=360; block.Reduction=.5f; block.Costs=Costs(10);
            var hp=Asset<HealthDefinition>(); hp.Defense=Asset<DefenseProfile>();
            hp.Defense.Layers=invalid ? new[] { new DefenseSlot(block),new DefenseSlot(Asset<InvalidMappedDefense>()) } : new[] { new DefenseSlot(block) };
            target.ApplyDefinition(hp);
        }
        [Test] public void NestedPaidDefenseSharesWalletAndObservesFinalAbilityOutcome()
        {
            Defense(); var abilities=Ability(out var definition); var committed=false; var changes=0;
            abilities.Changed+=value=> { if(value.Kind==AbilityEventKind.Committed) committed=true; };
            store.Changed+=()=> { changes++; Assert.That(committed,Is.True); Assert.That(target.Current,Is.EqualTo(95)); };
            Assert.That(abilities.TryActivate(definition,default,target),Is.True);
            Assert.That(changes,Is.EqualTo(1)); Assert.That(store.GetTotal(energy),Is.EqualTo(7000));
        }
        [Test] public void InvalidLateDefenseRollsBackBeforePayment()
        {
            Defense(true); var changes=0; store.Changed+=()=>changes++;
            Assert.That(target.ApplyDamage(new DamageInfo(40)).Rejection,Is.EqualTo(DamageRejection.InvalidDefense));
            Assert.That(target.Current,Is.EqualTo(100)); Assert.That(store.GetTotal(energy),Is.EqualTo(10000)); Assert.That(changes,Is.Zero);
        }
        [Test] public void CommonSnapshotRestoresActiveCastAndChargesOnlyOnResumedRelease()
        {
            var abilities=Ability(out var definition); definition.CastTime=1.5f;
            var references=new CombatReferenceRegistry(); references.RegisterActor("owner",owner); references.RegisterActor("target",target);
            var catalog=Asset<CombatDefinitionCatalog>(); var entries=new List<CombatCatalogEntry>();
            foreach(var asset in combatDefinitions) if(asset!=catalog && !(asset is CombatResourceMap)) entries.Add(new CombatCatalogEntry("asset-"+entries.Count,asset));
            Assert.That(catalog.Configure(entries.ToArray(),"resources-cost-test"),Is.True);
            Assert.That(abilities.TryActivate(definition,default,target),Is.True); abilities.Advance(.5f);
            Assert.That(CombatSaveSystem.TryCapture(catalog,references,out var combat,out var error),Is.True,error); var wallet=store.CaptureState();
            abilities.Advance(1); target.ApplyDamage(new DamageInfo(1000)); Assert.That(store.GetTotal(energy),Is.EqualTo(8000));
            var deathRewards=0; target.Died+=_=>deathRewards++;
            store.RestoreState(wallet,new[] { energy,cartridges });
            Assert.That(CombatSaveSystem.TryRestore(combat,catalog,references,out _,out error),Is.True,error);
            Assert.That(target.IsAlive,Is.True); Assert.That(deathRewards,Is.Zero); Assert.That(abilities.IsCasting,Is.True); Assert.That(store.GetTotal(energy),Is.EqualTo(10000));
            abilities.Advance(1); Assert.That(target.Current,Is.EqualTo(90)); Assert.That(store.GetTotal(energy),Is.EqualTo(8000));
            abilities.Advance(1); Assert.That(store.GetTotal(energy),Is.EqualTo(8000)); Assert.That(store.ActiveReservationCount,Is.Zero);
            references.Clear();
        }
    }
}
