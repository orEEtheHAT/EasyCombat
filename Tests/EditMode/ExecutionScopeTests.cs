using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ExecutionScopeTests
    {
        [Test] public void SeededRollsRepeatWithoutTouchingUnityRandom()
        {
            UnityEngine.Random.InitState(87); var state = UnityEngine.Random.state;
            var expectedUnity = UnityEngine.Random.value; UnityEngine.Random.state = state;
            float a,b;
            using (new CombatExecutionScope(new SeededCombatRandom(42))) a = CombatRandom.Value;
            using (new CombatExecutionScope(new SeededCombatRandom(42))) b = CombatRandom.Value;
            Assert.That(a,Is.EqualTo(b)); Assert.That(UnityEngine.Random.value,Is.EqualTo(expectedUnity));
        }
        [Test] public void NestedScopesRestoreAndRejectOutOfOrderDisposal()
        {
            var first = new CombatExecutionScope(clock:new ManualCombatClock(10));
            var second = new CombatExecutionScope(clock:new ManualCombatClock(20));
            try { Assert.Throws<InvalidOperationException>(() => first.Dispose()); Assert.That(CombatClock.Now,Is.EqualTo(20)); }
            finally { second.Dispose(); first.Dispose(); }
        }
        [Test] public void EmitterCooldownUsesExplicitClock()
        {
            var actor = new GameObject("Clock"); var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            try
            {
                var emitter = actor.AddComponent<DamageEmitter>(); emitter.Attack = attack; attack.Cooldown = 2;
                var clock = new ManualCombatClock();
                using (new CombatExecutionScope(new SeededCombatRandom(1),clock))
                {
                    Assert.That(emitter.TryFire(Vector3.zero,Vector3.up),Is.True);
                    Assert.That(emitter.TryFire(Vector3.zero,Vector3.up),Is.False);
                    clock.Advance(2); Assert.That(emitter.TryFire(Vector3.zero,Vector3.up),Is.True);
                }
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(attack); }
        }
        [Test] public void AllAttackRandomValuesUseTheInjectedSequence()
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            try
            {
                attack.MinimumDamage = 5; attack.MaximumDamage = 20; attack.CriticalChance = .5f;
                float a,b;
                using (new CombatExecutionScope(new SeededCombatRandom(73))) a = attack.RollRandom(null,default,Vector3.up).Amount;
                using (new CombatExecutionScope(new SeededCombatRandom(73))) b = attack.RollRandom(null,default,Vector3.up).Amount;
                Assert.That(a,Is.EqualTo(b));
            }
            finally { Object.DestroyImmediate(attack); }
        }
    }
}
