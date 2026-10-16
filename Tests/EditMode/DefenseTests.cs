using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    internal sealed class InvalidDefenseTestLayer : DefenseLayerDefinition
    {
        public override DamageAmounts Reduce(in DefenseContext context, DamageAmounts incoming, ref DefenseLayerState state) => incoming.Scale(float.NaN);
    }
    public sealed class DefenseTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private Health target;
        private GameObject source;
        private HealthDefinition profile;
        private DefenseProfile defense;
        private HitRulesDefinition rules;
        private DamageTypeDefinition fire, poison;
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name) { var go = new GameObject(name); cleanup.Add(go); return go; }
        [SetUp] public void Setup()
        {
            target = Actor("Target").AddComponent<Health>(); source = Actor("Source"); source.AddComponent<Health>();
            target.gameObject.AddComponent<CombatTeam>().Team = 1; source.AddComponent<CombatTeam>().Team = 2;
            rules = Asset<HitRulesDefinition>(); defense = Asset<DefenseProfile>(); profile = Asset<HealthDefinition>();
            profile.HitRules = rules; profile.Defense = defense; fire = Asset<DamageTypeDefinition>(); poison = Asset<DamageTypeDefinition>();
        }
        [TearDown] public void Teardown()
        { for (var i = cleanup.Count - 1; i >= 0; i--) if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private void Configure(params DefenseSlot[] layers) { defense.Layers = layers; target.ApplyDefinition(profile); }
        private ShieldLayer Shield(float capacity = 50) { var s = Asset<ShieldLayer>(); s.Capacity = capacity; return s; }
        private DamageInfo Hit(float amount = 20, bool periodic = false) => new DamageInfo(amount, source, direction: Vector3.back, type: fire, isPeriodic: periodic);
        [TestCase("self", DamageRejection.Self)] [TestCase("ally", DamageRejection.Ally)]
        [TestCase("enemy", DamageRejection.Enemy)] [TestCase("neutral", DamageRejection.Neutral)] [TestCase("environment", DamageRejection.Environment)]
        public void RelationshipRulesRejectWholeHitBeforeShieldAndStatuses(string relation, DamageRejection expected)
        {
            GameObject actor = source;
            switch (relation)
            {
                case "self": actor = target.gameObject; rules.AllowSelf = false; break;
                case "ally": source.GetComponent<CombatTeam>().Team = 1; rules.AllowAllies = false; break;
                case "enemy": rules.AllowEnemies = false; break;
                case "neutral": Object.DestroyImmediate(source.GetComponent<CombatTeam>()); rules.AllowNeutralActors = false; break;
                case "environment": actor = null; rules.AllowEnvironment = false; break;
            }
            Configure(new DefenseSlot(Shield())); var status = Asset<StatusDefinition>(); var controller = target.gameObject.AddComponent<StatusController>();
            var result = target.ApplyDamage(new DamageInfo(20, actor, onHitStatuses: new[] { new StatusApplication(status) }, statusPolicy: StatusApplyPolicy.OnContact));
            Assert.That(result.Rejection, Is.EqualTo(expected)); Assert.That(target.Current, Is.EqualTo(100));
            Assert.That(target.ShieldCurrent, Is.EqualTo(50)); Assert.That(controller.Count, Is.Zero);
        }
        [Test] public void SourceTeamAndIdentitySurviveDestructionAndTeamChange()
        {
            source.GetComponent<CombatTeam>().Team = 1; rules.AllowAllies = false; Configure(); var hit = Hit();
            source.GetComponent<CombatTeam>().Team = 2; Object.DestroyImmediate(source);
            Assert.That(target.ApplyDamage(hit).Rejection, Is.EqualTo(DamageRejection.Ally));
        }
        [Test] public void TargetTeamIsReadAtImpact()
        {
            rules.AllowAllies = false; Configure(); var hit = Hit(); target.GetComponent<CombatTeam>().Team = 2;
            Assert.That(target.ApplyDamage(hit).Rejection, Is.EqualTo(DamageRejection.Ally));
        }
        [Test] public void ShieldBreakPassesRemainderWithoutOverkillOrFalseDeath()
        {
            Configure(new DefenseSlot(Shield(30))); var deaths = 0; target.Died += _ => deaths++;
            var result = target.ApplyDamage(Hit(50));
            Assert.That(target.ShieldCurrent, Is.Zero); Assert.That(target.Current, Is.EqualTo(80));
            Assert.That(result.ShieldAbsorbed, Is.EqualTo(30).Within(.0001)); Assert.That(result.HealthDamage, Is.EqualTo(20).Within(.0001));
            Assert.That(result.Overkill, Is.Zero); Assert.That(deaths, Is.Zero); Assert.That(result.Defenses.Count, Is.EqualTo(1));
        }
        [Test] public void ShieldOnlyHitHasSeparateSuccessAndNoHealthDamageEvent()
        {
            Configure(new DefenseSlot(Shield())); var damaged = 0; var changed = 0;
            target.Damaged += _ => damaged++; target.DefenseChanged += _ => changed++;
            var result = target.ApplyDamage(Hit());
            Assert.That(result.Applied, Is.False); Assert.That(result.HitApplied, Is.True); Assert.That(result.Overkill, Is.Zero);
            Assert.That(damaged, Is.Zero); Assert.That(changed, Is.EqualTo(1)); Assert.That(target.Current, Is.EqualTo(100));
        }
        [TestCase(DefenseStage.BeforeResistance, 5, 0)] [TestCase(DefenseStage.AfterResistance, 0, 10)]
        public void ShieldPlacementChangesWhatResistanceProtects(DefenseStage stage, float hpLoss, float shieldLeft)
        {
            var resistance = Asset<ResistanceProfile>(); resistance.Configure(new DamageResistance(fire, .5f)); profile.Resistances = resistance;
            Configure(new DefenseSlot(Shield(30), stage)); var result = target.ApplyDamage(Hit(40));
            Assert.That(result.AppliedAmount, Is.EqualTo(hpLoss).Within(.0001)); Assert.That(target.ShieldCurrent, Is.EqualTo(shieldLeft).Within(.0001));
            Assert.That(result.ShieldAbsorbed + result.ResistanceDelta + result.HealthDamage, Is.EqualTo(40).Within(.0001));
        }
        [Test] public void TwoShieldsConsumeInOrderWithIndependentTypeFilters()
        {
            var first = Shield(10); first.FilterTypes = true; first.Types = new[] { fire }; var second = Shield(15);
            Configure(new DefenseSlot(first), new DefenseSlot(second));
            var parts = new DamageAmounts(); parts.Add(fire, 20); parts.Add(poison, 20);
            var result = target.ApplyDamage(new DamageInfo(parts, source));
            Assert.That(result.AppliedAmount, Is.EqualTo(15).Within(.0001)); Assert.That(result.ShieldAbsorbed, Is.EqualTo(25).Within(.0001));
            Assert.That(result.Breakdown[0].ShieldAbsorbed, Is.EqualTo(15).Within(.0001)); Assert.That(result.Breakdown[1].ShieldAbsorbed, Is.EqualTo(10).Within(.0001));
            Assert.That(target.ShieldCurrent, Is.Zero);
        }
        [Test] public void ArmorIsOncePerPacketAndRespectsTypeFilter()
        {
            var armor = Asset<FlatArmorLayer>(); armor.Reduction = 6; armor.FilterTypes = true; armor.Types = new[] { fire };
            Configure(new DefenseSlot(armor)); var parts = new DamageAmounts(); parts.Add(fire, 10); parts.Add(fire, 10); parts.Add(poison, 10);
            var result = target.ApplyDamage(new DamageInfo(parts, source));
            Assert.That(result.AppliedAmount, Is.EqualTo(24).Within(.0001)); Assert.That(result.Breakdown[2].Applied, Is.EqualTo(10).Within(.0001));
            Assert.That(result.ArmorPrevented, Is.EqualTo(6).Within(.0001));
        }
        [Test] public void LayerOrderIsExplicitAndMatters()
        {
            var flat = Asset<FlatArmorLayer>(); flat.Reduction = 10; var percent = Asset<ReductionLayer>(); percent.Reduction = .5f;
            Configure(new DefenseSlot(flat), new DefenseSlot(percent)); Assert.That(target.ApplyDamage(Hit(30)).AppliedAmount, Is.EqualTo(10).Within(.0001));
            Configure(new DefenseSlot(percent), new DefenseSlot(flat)); Assert.That(target.ApplyDamage(Hit(30)).AppliedAmount, Is.EqualTo(5).Within(.0001));
        }
        [Test] public void FailedLaterLayerRollsBackPreviouslySpentShield()
        {
            Configure(new DefenseSlot(Shield(10)), new DefenseSlot(Asset<InvalidDefenseTestLayer>()));
            Assert.That(target.ApplyDamage(Hit()).Rejection, Is.EqualTo(DamageRejection.InvalidDefense));
            Assert.That(target.ShieldCurrent, Is.EqualTo(10)); Assert.That(target.Current, Is.EqualTo(100));
        }
        [Test] public void InvalidResistanceResultDoesNotCommitPreResistanceShield()
        {
            var resist = Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(fire, -1)); profile.Resistances = resist;
            Configure(new DefenseSlot(Shield(1), DefenseStage.BeforeResistance));
            Assert.That(target.ApplyDamage(Hit(float.MaxValue)).Rejection, Is.EqualTo(DamageRejection.InvalidAmount));
            Assert.That(target.ShieldCurrent, Is.EqualTo(1));
        }
        [TestCase(StatusApplyPolicy.AfterDamage, 0)] [TestCase(StatusApplyPolicy.OnContact, 1)] [TestCase(StatusApplyPolicy.AfterDamageOrShield, 1)]
        public void StatusPolicyDistinguishesShieldAndHealthLoss(StatusApplyPolicy policy, int expected)
        {
            Configure(new DefenseSlot(Shield())); var status = Asset<StatusDefinition>(); target.gameObject.AddComponent<StatusController>();
            var result = target.ApplyDamage(new DamageInfo(20, source, onHitStatuses: new[] { new StatusApplication(status) }, statusPolicy: policy));
            Assert.That(result.StatusesApplied, Is.EqualTo(expected));
        }
        [Test] public void CooldownStartsOnShieldDamageAndBypassesPeriodicByDefault()
        {
            rules.InvulnerabilityAfterHit = 1; Configure(new DefenseSlot(Shield()));
            Assert.That(target.ApplyDamage(Hit()).HitApplied, Is.True);
            Assert.That(target.ApplyDamage(Hit()).Rejection, Is.EqualTo(DamageRejection.Invulnerable));
            Assert.That(target.ApplyDamage(Hit(1, true)).HitApplied, Is.True);
            target.AdvanceDefense(.5f); Assert.That(target.ApplyDamage(Hit()).Rejection, Is.EqualTo(DamageRejection.Invulnerable));
            target.AdvanceDefense(.5f); Assert.That(target.ApplyDamage(Hit()).HitApplied, Is.True);
        }
        [Test] public void ManualInvulnerabilityAndDodgeCanPreventPeriodicDamage()
        {
            Configure(); target.GrantInvulnerability(1); Assert.That(target.ApplyDamage(Hit(1, true)).Rejection, Is.EqualTo(DamageRejection.Invulnerable));
            target.AdvanceDefense(1); rules.DodgeChance = 1;
            Assert.That(target.ApplyDamage(Hit()).Rejection, Is.EqualTo(DamageRejection.Dodged));
            Assert.That(target.ApplyDamage(Hit(1, true)).Applied, Is.True); rules.DodgePeriodic = true;
            Assert.That(target.ApplyDamage(Hit(1, true)).Rejection, Is.EqualTo(DamageRejection.Dodged));
        }
        [Test] public void FrontalBlockDoesNotBlockRearOrDirectionlessHit()
        {
            var block = Asset<BlockLayer>(); Configure(new DefenseSlot(block));
            Assert.That(target.ApplyDamage(Hit()).Rejection, Is.EqualTo(DamageRejection.Blocked));
            Assert.That(target.ApplyDamage(new DamageInfo(20, source, direction: Vector3.forward)).AppliedAmount, Is.EqualTo(20));
            Assert.That(target.ApplyDamage(new DamageInfo(20, source)).AppliedAmount, Is.EqualTo(20));
            block.Chance = 0; Assert.That(target.ApplyDamage(Hit()).AppliedAmount, Is.EqualTo(20));
        }
        [Test] public void RechargeUsesOnlyTimeBeyondDelayAndCanPausePerLayer()
        {
            var shield = Shield(); shield.RechargePerSecond = 10; shield.DelayAfterAbsorption = 2;
            Configure(new DefenseSlot(shield)); target.ApplyDamage(Hit(30)); target.AdvanceDefense(1); Assert.That(target.ShieldCurrent, Is.EqualTo(20));
            target.AdvanceDefense(1.5f); Assert.That(target.ShieldCurrent, Is.EqualTo(25));
            target.SetDefenseLayerEnabled(0, false); target.AdvanceDefense(10); Assert.That(target.Defense.GetState(0).Current, Is.EqualTo(25));
            target.SetDefenseLayerEnabled(0, true); target.AdvanceDefense(10); Assert.That(target.ShieldCurrent, Is.EqualTo(50));
        }
        [Test] public void UnmatchedDamageDoesNotRestartRechargeDelay()
        {
            var shield = Shield(); shield.FilterTypes = true; shield.Types = new[] { fire }; shield.RechargePerSecond = 10; shield.DelayAfterAbsorption = 2;
            Configure(new DefenseSlot(shield)); target.ApplyDamage(Hit(20)); target.AdvanceDefense(1);
            target.ApplyDamage(new DamageInfo(1, source, type: poison)); target.AdvanceDefense(1.5f);
            Assert.That(target.ShieldCurrent, Is.EqualTo(35));
        }
        [Test] public void ResetRestoresInitialShieldAndClearsTemporaryImmunityWithoutMutatingSharedSO()
        {
            var shield = Shield(); shield.InitialFraction = .5f; Configure(new DefenseSlot(shield));
            var other = Actor("Other").AddComponent<Health>(); other.ApplyDefinition(profile);
            target.ApplyDamage(Hit(20)); Assert.That(other.ShieldCurrent, Is.EqualTo(25)); target.GrantInvulnerability(10); target.ResetToFull();
            Assert.That(target.ShieldCurrent, Is.EqualTo(25)); Assert.That(target.InvulnerabilityRemaining, Is.Zero); Assert.That(shield.Capacity, Is.EqualTo(50));
            Assert.That(target.RestoreShield(0, 99), Is.EqualTo(25)); Assert.That(target.ShieldCurrent, Is.EqualTo(50));
        }
        [Test] public void PartialAbsorptionAndTypeImmunityLeaveHPIndependent()
        {
            var shield = Shield(); shield.Absorption = .5f; Configure(new DefenseSlot(shield));
            var result = target.ApplyDamage(Hit(20)); Assert.That(result.AppliedAmount, Is.EqualTo(10)); Assert.That(target.ShieldCurrent, Is.EqualTo(40));
        }
        [Test] public void PercentHealthFormulaIgnoresShieldCapacityAndCapsBeforeDefenses()
        {
            Configure(new DefenseSlot(Shield(500)));
            var formula = Asset<DamageFormulaDefinition>(); formula.Terms = new[] { new DamageFormulaTerm(fire, StatusDamageBasis.TargetMaximumHealth, .5f) { MaximumDamage = 20 } };
            var attack = Asset<AttackDefinition>(); attack.Formula = formula;
            var result = target.ApplyDamage(attack.RollDamage(source, default, Vector3.forward, 0, 1));
            Assert.That(result.Damage.Amount, Is.EqualTo(20)); Assert.That(target.ShieldCurrent, Is.EqualTo(480));
        }
        [Test] public void WarmRulesLayersAndRechargeAllocateNoManagedMemory()
        {
            var armor = Asset<FlatArmorLayer>(); var shield = Shield(1000); shield.DelayAfterAbsorption = 0; shield.RechargePerSecond = 100;
            Configure(new DefenseSlot(armor), new DefenseSlot(shield)); var hit = Hit();
            for (var i = 0; i < 32; i++) { target.ApplyDamage(hit); target.AdvanceDefense(1); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++) { target.ApplyDamage(hit); target.AdvanceDefense(1); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
    }
}
