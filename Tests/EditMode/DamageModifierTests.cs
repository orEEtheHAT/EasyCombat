using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class DamageModifierTests
    {
        private readonly List<Object> cleanup = new List<Object>();
        private GameObject source;
        private Health target;
        private AttackDefinition attack;
        private DamageTypeDefinition fire,physical;
        private T Asset<T>() where T : ScriptableObject { var a = ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name) { var a = new GameObject(name); cleanup.Add(a); return a; }
        [SetUp] public void Setup()
        {
            source = Actor("Source"); target = Actor("Target").AddComponent<Health>();
            fire = Asset<DamageTypeDefinition>(); physical = Asset<DamageTypeDefinition>(); attack = Asset<AttackDefinition>(); attack.Damage = 20; attack.DamageType = physical;
        }
        [TearDown] public void Cleanup() { for (var i=cleanup.Count-1;i>=0;i--) if (cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private DamageInfo Shot() => attack.RollDamage(source,default,Vector3.forward,0,1);
        private void Resist(float amount)
        {
            var profile = Asset<HealthDefinition>(); var resist = Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(physical,amount)); profile.Resistances = resist; target.ApplyDefinition(profile);
        }
        [Test] public void PenetrationUsesPercentThenFlatWithoutCreatingVulnerability()
        {
            Resist(.8f); attack.Protection = Asset<DamageProtectionPolicy>(); attack.Protection.PercentResistancePenetration = .5f; attack.Protection.FlatResistancePenetration = .1f;
            Assert.That(target.ApplyDamage(Shot()).AppliedAmount,Is.EqualTo(14).Within(.0001));
            attack.Protection.FlatResistancePenetration = .9f; Assert.That(target.ApplyDamage(Shot()).AppliedAmount,Is.EqualTo(20));
        }
        [Test] public void PenetrationPreservesNegativeResistanceButBypassSkipsIt()
        {
            Resist(-.5f); attack.Protection = Asset<DamageProtectionPolicy>(); attack.Protection.PercentResistancePenetration = 1;
            Assert.That(target.ApplyDamage(Shot()).AppliedAmount,Is.EqualTo(30));
            attack.Protection.BypassResistance = true; Assert.That(target.ApplyDamage(Shot()).AppliedAmount,Is.EqualTo(20));
        }
        [Test] public void ShieldAndArmorBypassAreIndependentAndNeverBypassInvulnerability()
        {
            var armor = Asset<FlatArmorLayer>(); armor.Reduction = 5; var shield = Asset<ShieldLayer>(); shield.Capacity = 40;
            var defense = Asset<DefenseProfile>(); defense.Layers = new[] { new DefenseSlot(armor),new DefenseSlot(shield) };
            var profile = Asset<HealthDefinition>(); profile.Defense = defense; target.ApplyDefinition(profile);
            attack.Protection = Asset<DamageProtectionPolicy>(); attack.Protection.BypassShields = true;
            var shot = Shot(); attack.Protection.BypassShields = false; // Captured shot retains its policy.
            Assert.That(target.ApplyDamage(shot.Scale(2).At(default,Vector3.forward)).AppliedAmount,Is.EqualTo(35)); Assert.That(target.ShieldCurrent,Is.EqualTo(40));
            attack.Protection.BypassArmor = true; Assert.That(target.ApplyDamage(Shot()).ShieldAbsorbed,Is.EqualTo(20));
            target.Invulnerable = true; attack.Protection.BypassResistance = attack.Protection.BypassShields = true;
            Assert.That(target.ApplyDamage(Shot()).Rejection,Is.EqualTo(DamageRejection.Invulnerable));
        }
        [Test] public void OutgoingRulesCaptureAtEmissionAndIncomingRulesUseCurrentTarget()
        {
            var outgoing = Asset<DamageModifierProfile>(); outgoing.Rules = new[] { new DamageModifierRule(2,physical) };
            source.AddComponent<CombatDamageModifiers>().Outgoing = outgoing;
            var shot = Shot(); outgoing.Rules[0].Multiplier = 99;
            var incoming = Asset<DamageModifierProfile>(); incoming.Rules = new[] { new DamageModifierRule(.5f) };
            target.gameObject.AddComponent<CombatDamageModifiers>().Incoming = incoming;
            Assert.That(target.ApplyDamage(shot).AppliedAmount,Is.EqualTo(20));
        }
        [Test] public void TypedConversionUsesDestinationResistanceAndTagConditions()
        {
            var tag = Asset<CombatTagDefinition>(); attack.Tags = new[] { tag };
            var mods = Asset<DamageModifierProfile>(); mods.Rules = new[] { new DamageModifierRule(1,physical) { RequiredTag = tag,ConvertType = true,DestinationType = fire } };
            source.AddComponent<CombatDamageModifiers>().Outgoing = mods;
            var resist = Asset<ResistanceProfile>(); resist.Configure(new DamageResistance(fire,.5f)); var hp = Asset<HealthDefinition>(); hp.Resistances = resist; target.ApplyDefinition(hp);
            var result = target.ApplyDamage(Shot()); Assert.That(result.AppliedAmount,Is.EqualTo(10)); Assert.That(result.Damage.Type,Is.EqualTo(fire));
            attack.Tags = null; Assert.That(target.ApplyDamage(Shot()).AppliedAmount,Is.EqualTo(20));
        }
        [Test] public void PercentageHardCapRemainsAfterOutgoingAndIncomingModifiers()
        {
            var formula = Asset<DamageFormulaDefinition>(); formula.Terms = new[] { new DamageFormulaTerm(fire,StatusDamageBasis.TargetMaximumHealth,.1f) { MaximumDamage = 15 } }; attack.Formula = formula;
            var mods = Asset<DamageModifierProfile>(); mods.Rules = new[] { new DamageModifierRule(3) };
            source.AddComponent<CombatDamageModifiers>().Outgoing = mods; target.gameObject.AddComponent<CombatDamageModifiers>().Incoming = mods;
            Assert.That(target.ApplyDamage(Shot()).AppliedAmount,Is.EqualTo(15));
        }
        [Test] public void IncomingPeriodicFilterAndZeroMultiplierWork()
        {
            var mods = Asset<DamageModifierProfile>(); mods.Rules = new[] { new DamageModifierRule(0) { Kind = DamageModifierKind.Periodic } };
            target.gameObject.AddComponent<CombatDamageModifiers>().Incoming = mods;
            Assert.That(target.ApplyDamage(new DamageInfo(20,source,isPeriodic:true)).AppliedAmount,Is.Zero);
            Assert.That(target.ApplyDamage(new DamageInfo(20,source)).AppliedAmount,Is.EqualTo(20));
        }
        [Test] public void InvalidModifierCannotPartiallyConsumeShields()
        {
            var shield = Asset<ShieldLayer>(); var defense = Asset<DefenseProfile>(); defense.Layers = new[] { new DefenseSlot(shield) };
            var hp = Asset<HealthDefinition>(); hp.Defense = defense; target.ApplyDefinition(hp);
            var mods = Asset<DamageModifierProfile>(); mods.Rules = new[] { new DamageModifierRule(float.NaN) };
            target.gameObject.AddComponent<CombatDamageModifiers>().Incoming = mods;
            Assert.That(target.ApplyDamage(Shot()).Rejection,Is.EqualTo(DamageRejection.InvalidAmount)); Assert.That(target.ShieldCurrent,Is.EqualTo(50));
        }
        [TestCase(false)] [TestCase(true)] public void IndependentPeriodicStacksCaptureTheirOwnOutgoingAdjustment(bool sharedFormula)
        {
            var modifier = Asset<DamageModifierProfile>(); modifier.Rules = new[] { new DamageModifierRule(2,physical) { ConvertType = true,DestinationType = fire } };
            source.AddComponent<CombatDamageModifiers>().Outgoing = modifier;
            var effect = Asset<PeriodicDamageEffect>(); effect.DamageType = physical; effect.DamagePerStack = 5;
            if (sharedFormula) { effect.Formula = Asset<DamageFormulaDefinition>(); effect.Formula.Terms = new[] { new DamageFormulaTerm(physical,StatusDamageBasis.Fixed,5) }; }
            var status = Asset<StatusDefinition>(); status.TickEffects = new StatusTickEffect[] { effect };
            var controller = target.gameObject.AddComponent<StatusController>();
            Assert.That(controller.Apply(status,source),Is.True); modifier.Rules[0].Multiplier = 3;
            Assert.That(controller.Apply(status,source),Is.True); modifier.Rules[0].Multiplier = 99;
            Assert.That(controller.TryGetStackDamage(status,0,0,out var first),Is.True); Assert.That(first,Is.EqualTo(10));
            Assert.That(controller.TryGetStackDamage(status,1,0,out var second),Is.True); Assert.That(second,Is.EqualTo(15));
            DamageTypeDefinition received = null; target.Damaged += result => received = result.Damage.Type;
            controller.Advance(1);
            Assert.That(target.Current,Is.EqualTo(75)); Assert.That(received,Is.EqualTo(fire));
        }
    }
}
