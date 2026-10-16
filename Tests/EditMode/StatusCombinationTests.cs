using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class StatusCombinationTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private Health health;
        private StatusController controller;
        private StatusDefinition input,output;
        private StatusCombinationDefinition recipe;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        [SetUp] public void Setup()
        {
            var actor=new GameObject("Combination"); cleanup.Add(actor); health=actor.AddComponent<Health>(); controller=actor.AddComponent<StatusController>();
            input=Asset<StatusDefinition>(); output=Asset<StatusDefinition>(); recipe=Asset<StatusCombinationDefinition>(); recipe.Consume=input; recipe.Apply=output; recipe.Stacks=2;
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void ExchangeFreesSlotAndObserversSeeCommittedResult()
        {
            typeof(StatusController).GetField("maximumActiveStatuses",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(controller,1);
            controller.Apply(input); controller.Apply(input); var sawResult=false;
            controller.Lifecycle+=change=> { if(change.RemovalReason==StatusRemovalReason.Consumed) sawResult=controller.GetStacks(output)==1; };
            Assert.That(controller.TryCombine(recipe),Is.True); Assert.That(controller.GetStacks(input),Is.Zero); Assert.That(controller.GetStacks(output),Is.EqualTo(1)); Assert.That(sawResult,Is.True);
        }
        [Test] public void RejectedOutputRestoresExactStackIdsTimersAndPower()
        {
            output.Stacking=StatusStacking.IgnoreWhileActive;
            var effect=Asset<PeriodicDamageEffect>(); effect.DamagePerStack=5; input.TickEffects=new StatusTickEffect[] { effect };
            controller.Apply(input); effect.DamagePerStack=10; controller.Apply(input); controller.Apply(output); controller.Advance(.25f);
            var a=controller.GetStack(input,0); var b=controller.GetStack(input,1);
            Assert.That(controller.TryCombine(recipe),Is.False);
            Assert.That(controller.GetStack(input,0).Id,Is.EqualTo(a.Id)); Assert.That(controller.GetStack(input,1).Id,Is.EqualTo(b.Id));
            Assert.That(controller.GetStack(input,0).NextTickIn,Is.EqualTo(a.NextTickIn)); Assert.That(controller.GetStack(input,1).Remaining,Is.EqualTo(b.Remaining));
            controller.TryGetStackDamage(input,0,0,out var first); controller.TryGetStackDamage(input,1,0,out var second);
            Assert.That(first,Is.EqualTo(5)); Assert.That(second,Is.EqualTo(10));
        }
        [Test] public void StatusImmunityRejectsCombinationWithoutConsumingInput()
        {
            var hp=Asset<HealthDefinition>(); hp.StatusImmunities=new[] { output }; health.ApplyDefinition(hp);
            controller.Apply(input); controller.Apply(input);
            Assert.That(controller.TryCombine(recipe),Is.False); Assert.That(controller.GetStacks(input),Is.EqualTo(2));
        }
        [Test] public void SharedStackConsumptionRollsBackAndThenSucceeds()
        {
            input.Stacking=StatusStacking.StackAndRefresh; output.Stacking=StatusStacking.IgnoreWhileActive;
            controller.Apply(input); controller.Apply(input); controller.Apply(input); controller.Apply(output);
            Assert.That(controller.TryCombine(recipe),Is.False); Assert.That(controller.GetStacks(input),Is.EqualTo(3));
            controller.Remove(output); Assert.That(controller.TryCombine(recipe),Is.True); Assert.That(controller.GetStacks(input),Is.EqualTo(1));
        }
        [Test] public void MissingSourceStatRejectsPreparationWithoutConsumption()
        {
            var effect=Asset<PeriodicDamageEffect>(); var key=Asset<CombatStatDefinition>();
            effect.Terms=new[] { new StatusDamageTerm(StatusDamageBasis.SourceStat,1) { Stat=key } }; output.TickEffects=new StatusTickEffect[] { effect };
            controller.Apply(input); controller.Apply(input);
            Assert.That(controller.TryCombine(recipe),Is.False); Assert.That(controller.GetStacks(input),Is.EqualTo(2));
        }
    }
}
