using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class InvalidPaidDefense : DefenseLayerDefinition
    {
        public override DamageAmounts Reduce(in DefenseContext context,DamageAmounts input,ref DefenseLayerState state)
        { var invalid=new DamageAmounts(); invalid.Add(null,-1); return invalid; }
    }
    public sealed class BlockResourceTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health health;
        private CombatResources resources;
        private ResourceDefinition stamina;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private BlockLayer Block(float reduction=1)
        { var block=Asset<BlockLayer>(); block.Arc=360; block.Reduction=reduction; block.Costs=new[] { new ResourceCost(stamina,10) }; return block; }
        private void Equip(params DefenseLayerDefinition[] layers)
        {
            var profile=Asset<HealthDefinition>(); profile.Defense=Asset<DefenseProfile>(); profile.Defense.Layers=new DefenseSlot[layers.Length];
            for(var i=0;i<layers.Length;i++) profile.Defense.Layers[i]=new DefenseSlot(layers[i]); health.ApplyDefinition(profile);
        }
        [SetUp] public void Setup()
        {
            var actor=new GameObject("Paid block"); cleanup.Add(actor); health=actor.AddComponent<Health>(); resources=actor.AddComponent<CombatResources>();
            stamina=Asset<ResourceDefinition>(); stamina.Maximum=15; var profile=Asset<ResourceProfile>(); profile.Resources=new[] { stamina }; resources.Configure(profile);
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private float Stamina { get { resources.TryGet(stamina,out var state); return state.Current; } }
        [Test] public void SuccessfulBlockPaysOnceAndInsufficientFundsLetHitThrough()
        {
            Equip(Block()); Assert.That(health.ApplyDamage(new DamageInfo(20)).AppliedAmount,Is.Zero); Assert.That(Stamina,Is.EqualTo(5));
            Assert.That(health.ApplyDamage(new DamageInfo(20)).AppliedAmount,Is.EqualTo(20)); Assert.That(Stamina,Is.EqualTo(5));
        }
        [Test] public void MultipleLayersShareTheSameResourceBudget()
        {
            Equip(Block(.5f),Block()); var result=health.ApplyDamage(new DamageInfo(40));
            Assert.That(result.AppliedAmount,Is.EqualTo(20)); Assert.That(result.ArmorPrevented,Is.EqualTo(20)); Assert.That(Stamina,Is.EqualTo(5));
        }
        [Test] public void LaterInvalidLayerRollsBackAllEarlierCosts()
        {
            Equip(Block(.5f),Asset<InvalidPaidDefense>());
            Assert.That(health.ApplyDamage(new DamageInfo(40)).Rejection,Is.EqualTo(DamageRejection.InvalidDefense)); Assert.That(Stamina,Is.EqualTo(15)); Assert.That(health.Current,Is.EqualTo(100));
        }
        [Test] public void ResourceObserversSeeCommittedHealthAndCannotReenterSpending()
        {
            Equip(Block(.5f)); var observed=0f; var reentered=true;
            resources.Changed+=_=> { observed=health.Current; reentered=resources.TrySpend(new[] { new ResourceCost(stamina,1) }); };
            health.ApplyDamage(new DamageInfo(40)); Assert.That(observed,Is.EqualTo(80)); Assert.That(reentered,Is.False);
        }
        [Test] public void MissedArcAndZeroReductionDoNotSpend()
        {
            var block=Block(); block.Arc=90; Equip(block);
            health.ApplyDamage(new DamageInfo(10,direction:Vector3.forward)); Assert.That(Stamina,Is.EqualTo(15));
            block.Arc=360; block.Reduction=0; health.ApplyDamage(new DamageInfo(10)); Assert.That(Stamina,Is.EqualTo(15));
        }
    }
}
