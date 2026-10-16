using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class ObserverIsolationTests
    {
        [Test] public void BrokenHealthObserversCannotCancelDamageDeathOrCleanup()
        {
            var actor = new GameObject("Observer test");
            try
            {
                var health = actor.AddComponent<Health>();
                var damaged = 0; var died = 0;
                health.Changed += _ => throw new InvalidOperationException("Health observer failure");
                health.Damaged += _ => damaged++; health.Died += _ => died++;
                LogAssert.Expect(LogType.Exception,"InvalidOperationException: Health observer failure");
                var result = health.ApplyDamage(new DamageInfo(1000));
                Assert.That(result.Killed,Is.True); Assert.That(damaged,Is.EqualTo(1)); Assert.That(died,Is.EqualTo(1));
                health.ApplyDamage(new DamageInfo(1000)); Assert.That(died,Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
