using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class HealingTests
    {
        private GameObject actor;
        private Health health;
        private HealingDefinition healing;
        private HealthDefinition profile;
        private CombatStatDefinition multiplier;
        [SetUp] public void Setup()
        {
            actor = new GameObject("Healer"); health = actor.AddComponent<Health>();
            healing = ScriptableObject.CreateInstance<HealingDefinition>(); profile = ScriptableObject.CreateInstance<HealthDefinition>();
            multiplier = ScriptableObject.CreateInstance<CombatStatDefinition>(); health.ApplyDefinition(profile);
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(actor); Object.DestroyImmediate(healing); Object.DestroyImmediate(profile); Object.DestroyImmediate(multiplier); }
        [Test] public void MixedFormulaUsesPreHealingHealthAndExplicitCritical()
        {
            health.ApplyDamage(new DamageInfo(80));
            healing.Terms = new[] { new HealingTerm(HealingBasis.Fixed,5),new HealingTerm(HealingBasis.TargetMissingHealth,.25f) };
            healing.CriticalChance = 1; healing.CriticalMultiplier = 2;
            Assert.That(healing.TryPrepare(actor,health,0,0,out var info),Is.True);
            Assert.That(health.ApplyHealing(info).AppliedAmount,Is.EqualTo(50)); Assert.That(health.Current,Is.EqualTo(70));
        }
        [Test] public void IncomingMultiplierAndCapApplyAfterCritical()
        {
            health.ApplyDamage(new DamageInfo(90));
            var stats = actor.AddComponent<CombatStats>(); stats.SetValue(multiplier,2); profile.ReceivedHealingMultiplierStat = multiplier;
            healing.CriticalChance = 1; healing.MaximumHealing = 30;
            healing.TryPrepare(actor,health,0,0,out var info);
            Assert.That(health.ApplyHealing(info).AppliedAmount,Is.EqualTo(30));
        }
        [Test] public void HealingRestrictionAndDeathCannotBeBypassedByLegacyHeal()
        {
            health.ApplyDamage(new DamageInfo(50)); profile.PreventHealing = true;
            Assert.That(health.Heal(30),Is.Zero); profile.PreventHealing = false;
            health.ApplyDamage(new DamageInfo(100));
            Assert.That(health.ApplyHealing(new HealingInfo(100)).Rejection,Is.EqualTo(HealingRejection.Dead));
        }
        [Test] public void MissingStatAndNonFiniteInputLeaveHealthUntouched()
        {
            health.ApplyDamage(new DamageInfo(50)); profile.ReceivedHealingMultiplierStat = multiplier;
            Assert.That(health.ApplyHealing(new HealingInfo(10)).Rejection,Is.EqualTo(HealingRejection.Invalid));
            Assert.That(health.ApplyHealing(new HealingInfo(float.NaN)).Rejection,Is.EqualTo(HealingRejection.Invalid));
            Assert.That(health.Current,Is.EqualTo(50));
        }
        [Test] public void LifestealCanChooseActualHealthLossOrAlsoShieldAbsorption()
        {
            var hit = new DamageResult(new DamageInfo(1000),25,false,shieldAbsorbed:50);
            healing.Terms = new[] { new HealingTerm(HealingBasis.TriggeringHealthDamage,.2f) };
            healing.TryPrepare(actor,health,0,1,out var hp,hit); Assert.That(hp.Amount,Is.EqualTo(5));
            healing.Terms[0].Basis = HealingBasis.TriggeringHitIncludingShield;
            healing.TryPrepare(actor,health,0,1,out var includingShield,hit); Assert.That(includingShield.Amount,Is.EqualTo(15));
        }
        [Test] public void OverhealRestoresExistingShieldAndReportsDiscardedRemainder()
        {
            var shield = ScriptableObject.CreateInstance<ShieldLayer>(); var defense = ScriptableObject.CreateInstance<DefenseProfile>();
            try
            {
                shield.Capacity = 20; shield.InitialFraction = 0; defense.Layers = new[] { new DefenseSlot(shield) }; profile.Defense = defense; health.ApplyDefinition(profile);
                health.ApplyDamage(new DamageInfo(10));
                var result = health.ApplyHealing(new HealingInfo(50,overheal:OverhealPolicy.RestoreShield));
                Assert.That(result.AppliedAmount,Is.EqualTo(10)); Assert.That(result.ShieldRestored,Is.EqualTo(20)); Assert.That(result.Discarded,Is.EqualTo(20));
                Assert.That(health.ShieldCurrent,Is.EqualTo(20));
            }
            finally { Object.DestroyImmediate(defense); Object.DestroyImmediate(shield); }
        }
        [Test] public void HealingObserversCannotRecursivelyHeal()
        {
            health.ApplyDamage(new DamageInfo(50)); var reason = HealingRejection.None;
            health.Healed += _ => reason = health.ApplyHealing(new HealingInfo(1)).Rejection;
            health.Heal(10); Assert.That(reason,Is.EqualTo(HealingRejection.Reentrant)); Assert.That(health.Current,Is.EqualTo(60));
        }
    }
}
