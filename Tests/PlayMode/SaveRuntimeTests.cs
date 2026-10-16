using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class SaveRuntimeTests
    {
        [UnityTest] public IEnumerator RestoredStatusReregistersAndTicksOnlyItsRemainingSchedule()
        {
            var source=new GameObject("SaveSource"); var target=new GameObject("SaveTarget");
            var a=source.AddComponent<Health>(); var b=target.AddComponent<Health>(); var controller=target.AddComponent<StatusController>();
            var effect=ScriptableObject.CreateInstance<PeriodicDamageEffect>(); effect.DamagePerStack=1;
            var status=ScriptableObject.CreateInstance<StatusDefinition>(); status.Duration=.1f; status.TickInterval=.05f; status.TickEffects=new StatusTickEffect[] { effect };
            var catalog=ScriptableObject.CreateInstance<CombatDefinitionCatalog>(); catalog.Configure(new[] { new CombatCatalogEntry("status",status),new CombatCatalogEntry("effect",effect) });
            var registry=new CombatReferenceRegistry(); registry.RegisterActor("source",a); registry.RegisterActor("target",b);
            try
            {
                controller.Apply(status,source); Assert.That(CombatSaveSystem.TryCaptureJson(catalog,registry,out var json,out var error),Is.True,error); controller.Clear();
                Assert.That(CombatSaveSystem.TryRestoreJson(json,catalog,registry,out _,out error),Is.True,error);
                yield return new WaitForSeconds(.2f); Assert.That(b.Current,Is.EqualTo(98)); Assert.That(controller.Count,Is.Zero);
            }
            finally { Object.Destroy(source); Object.Destroy(target); Object.Destroy(effect); Object.Destroy(status); Object.Destroy(catalog); }
            yield return null;
        }
    }
}
