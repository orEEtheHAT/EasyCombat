using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AdvancedDefenseTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health health;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private void Equip(params DefenseLayerDefinition[] layers)
        {
            var defense=Asset<DefenseProfile>(); defense.Layers=new DefenseSlot[layers.Length];
            for(var i=0;i<layers.Length;i++) defense.Layers[i]=new DefenseSlot(layers[i]);
            var hp=Asset<HealthDefinition>(); hp.Defense=defense; health.ApplyDefinition(hp);
        }
        [SetUp] public void Setup() { var actor=new GameObject("Defense"); cleanup.Add(actor); health=actor.AddComponent<Health>(); }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void TemporaryShieldDecaysExpiresAndCanBeReapplied()
        {
            var shield=Asset<TemporaryShieldLayer>(); shield.Capacity=50; shield.Duration=3; shield.DecayPerSecond=5; Equip(shield);
            health.AdvanceDefense(1); Assert.That(health.ShieldCurrent,Is.EqualTo(45));
            Assert.That(health.ApplyDamage(new DamageInfo(20)).ShieldAbsorbed,Is.EqualTo(20));
            health.AdvanceDefense(2); Assert.That(health.ShieldMaximum,Is.Zero);
            Assert.That(health.ResetDefenseLayer(0),Is.True); Assert.That(health.ShieldCurrent,Is.EqualTo(50));
            Assert.That(health.Defense.GetState(0).RechargeDelay,Is.EqualTo(3));
        }
        [Test] public void ChargesConsumeOncePerPacketAndRegenerateAcrossLongStep()
        {
            var charges=Asset<ChargeProtectionLayer>(); charges.Charges=2; charges.RechargeInterval=2; Equip(charges);
            var parts=new DamageAmounts(); parts.Add(null,5); parts.Add(null,5);
            Assert.That(health.ApplyDamage(new DamageInfo(parts)).AppliedAmount,Is.Zero); Assert.That(health.Defense.GetState(0).Current,Is.EqualTo(1));
            health.ApplyDamage(new DamageInfo(10)); Assert.That(health.ApplyDamage(new DamageInfo(10)).AppliedAmount,Is.EqualTo(10));
            health.AdvanceDefense(4); Assert.That(health.Defense.GetState(0).Current,Is.EqualTo(2));
        }
        [Test] public void DamageCapIsSharedAcrossMatchingChannels()
        {
            var cap=Asset<DamageCapLayer>(); cap.MaximumDamage=20; cap.MaximumHealthFraction=.1f; Equip(cap);
            var parts=new DamageAmounts(); parts.Add(null,50); parts.Add(null,50);
            var result=health.ApplyDamage(new DamageInfo(parts)); Assert.That(result.AppliedAmount,Is.EqualTo(10)); Assert.That(result.ArmorPrevented,Is.EqualTo(90));
        }
        [Test] public void ShieldCostsChangeEnergyDepletionWithoutChangingDamageAccounting()
        {
            var fire=Asset<DamageTypeDefinition>(); var shield=Asset<ShieldLayer>(); shield.Capacity=15;
            shield.DamageCosts=new[] { new ShieldDamageCost(fire,2) }; Equip(shield);
            var parts=new DamageAmounts(); parts.Add(fire,10); parts.Add(null,10);
            var result=health.ApplyDamage(new DamageInfo(parts));
            Assert.That(result.ShieldAbsorbed,Is.EqualTo(10).Within(.0001)); Assert.That(result.AppliedAmount,Is.EqualTo(10).Within(.0001));
            Assert.That(health.ShieldCurrent,Is.Zero);
        }
        [Test] public void DisabledTemporaryShieldPausesItsLifetime()
        {
            var shield=Asset<TemporaryShieldLayer>(); shield.Duration=2; Equip(shield);
            health.SetDefenseLayerEnabled(0,false); health.AdvanceDefense(10); health.SetDefenseLayerEnabled(0,true);
            Assert.That(health.Defense.GetState(0).RechargeDelay,Is.EqualTo(2));
        }
        [Test] public void InvalidShieldCostRejectsDamageWithoutSpendingEnergy()
        {
            var shield=Asset<ShieldLayer>(); Equip(shield); shield.DamageCosts=new[] { new ShieldDamageCost(null,0) };
            Assert.That(health.ApplyDamage(new DamageInfo(20)).Rejection,Is.EqualTo(DamageRejection.InvalidDefense));
            Assert.That(health.ShieldCurrent,Is.EqualTo(50));
        }
    }
}
