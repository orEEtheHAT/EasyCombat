using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class StatModifierTests
    {
        private GameObject actor;
        private CombatStats stats;
        private CombatStatDefinition key;
        private StatModifierDefinition set;
        [SetUp] public void Setup()
        {
            actor = new GameObject("Stats"); stats = actor.AddComponent<CombatStats>();
            key = ScriptableObject.CreateInstance<CombatStatDefinition>(); stats.SetValue(key,100);
            set = ScriptableObject.CreateInstance<StatModifierDefinition>();
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(actor); Object.DestroyImmediate(key); Object.DestroyImmediate(set); }
        private float Value() { Assert.That(stats.TryGetValue(key,out var value),Is.True); return value; }
        private StatModifier M(StatModifierOperation operation,float value) => new StatModifier(key,operation,value);
        [Test] public void OrderIndependentStagesAndExplanation()
        {
            stats.TryAdd(M(StatModifierOperation.Multiply,2),null,out _);
            stats.TryAdd(M(StatModifierOperation.AdditivePercent,.25f),null,out _);
            stats.TryAdd(M(StatModifierOperation.Flat,20),null,out _);
            stats.TryAdd(M(StatModifierOperation.AdditivePercent,.25f),null,out _);
            Assert.That(Value(),Is.EqualTo(360));
            Assert.That(stats.TryExplain(key,out var e),Is.True); Assert.That(e.Base,Is.EqualTo(100)); Assert.That(e.Flat,Is.EqualTo(20));
        }
        [Test] public void HandlesOwnAnApplicationAndNeverCrossActors()
        {
            stats.TryAdd(M(StatModifierOperation.Flat,10),null,out var a);
            stats.TryAdd(M(StatModifierOperation.Flat,20),null,out var b);
            var other = new GameObject();
            try { Assert.That(other.AddComponent<CombatStats>().Remove(a),Is.False); }
            finally { Object.DestroyImmediate(other); }
            Assert.That(a.Remove(),Is.True); Assert.That(a.Remove(),Is.False); Assert.That(b.IsActive,Is.True);
            Assert.That(Value(),Is.EqualTo(120));
            stats.ClearModifiers(); stats.TryAdd(M(StatModifierOperation.Flat,4),null,out _);
            Assert.That(b.Remove(),Is.False); Assert.That(Value(),Is.EqualTo(104));
        }
        [Test] public void SetsAreCopiedAndRemovedAtomically()
        {
            set.Modifiers = new[] { M(StatModifierOperation.Flat,20),M(StatModifierOperation.Multiply,2) };
            Assert.That(stats.TryAdd(set,this,out var handle),Is.True);
            set.Modifiers[0].Value = 999; Assert.That(Value(),Is.EqualTo(240));
            Assert.That(handle.Remove(),Is.True); Assert.That(Value(),Is.EqualTo(100));
        }
        [Test] public void InvalidOrOverBudgetSetCannotPartiallyApply()
        {
            stats.MaximumModifiers = 1;
            set.Modifiers = new[] { M(StatModifierOperation.Flat,20),M(StatModifierOperation.Multiply,2) };
            Assert.That(stats.TryAdd(set,this,out _),Is.False); Assert.That(stats.ModifierCount,Is.Zero);
            stats.MaximumModifiers = 3; set.Modifiers[1].Value = float.NaN;
            Assert.That(stats.TryAdd(set,this,out _),Is.False); Assert.That(Value(),Is.EqualTo(100));
        }
        [Test] public void SourceRemovalUsesReferenceIdentityAndNullIsNotWildcard()
        {
            var source = new object(); stats.TryAdd(M(StatModifierOperation.Flat,20),source,out _);
            stats.TryAdd(M(StatModifierOperation.Flat,3),null,out _);
            Assert.That(stats.RemoveSource(null),Is.Zero); Assert.That(stats.RemoveSource(new object()),Is.Zero);
            Assert.That(stats.RemoveSource(source),Is.EqualTo(1)); Assert.That(Value(),Is.EqualTo(103));
        }
        [Test] public void DebuffsClampAtZeroAndOverflowFailsExplicitly()
        {
            stats.TryAdd(M(StatModifierOperation.AdditivePercent,-2),null,out var debuff); Assert.That(Value(),Is.Zero);
            debuff.Remove(); stats.TryAdd(M(StatModifierOperation.Multiply,float.MaxValue),null,out _);
            Assert.That(stats.TryGetValue(key,out _),Is.False);
        }
        [Test] public void MissingBaseDoesNotBecomeAnImplicitStat()
        {
            var missing = ScriptableObject.CreateInstance<CombatStatDefinition>();
            try { stats.TryAdd(new StatModifier(missing,StatModifierOperation.Flat,10),null,out _); Assert.That(stats.TryGetValue(missing,out _),Is.False); }
            finally { Object.DestroyImmediate(missing); }
        }
        [Test] public void QueriesAllocateNothingAfterWarmup()
        {
            stats.TryAdd(M(StatModifierOperation.Flat,10),null,out _);
            for (var i=0;i<32;i++) stats.TryGetValue(key,out _);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i=0;i<10000;i++) stats.TryGetValue(key,out _);
            var allocated = GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.That(allocated,Is.Zero);
        }
    }
}
