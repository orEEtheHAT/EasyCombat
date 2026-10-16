using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class AbilitySchedulerTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private GameObject source;
        private Health target;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        [SetUp] public void Setup()
        {
            source=new GameObject("Caster"); cleanup.Add(source); source.transform.position=new Vector3(0,4200,0); source.AddComponent<Health>();
            var go=new GameObject("Target"); cleanup.Add(go); go.transform.position=source.transform.position+Vector3.forward; go.AddComponent<SphereCollider>().radius=.2f; target=go.AddComponent<Health>();
            Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.Destroy(cleanup[i]); cleanup.Clear(); yield return null; }
        [UnityTest] public IEnumerator AbilityWindupRunsWithoutPerActorUpdate()
        {
            var action=Asset<AbilityAttackAction>(); action.Attack=Asset<AttackDefinition>(); action.Attack.Damage=10; action.DirectToTarget=true;
            var ability=Asset<AbilityDefinition>(); ability.TargetMode=AbilityTargetMode.Unit; ability.CastTime=.03f; ability.Actions=new CombatAbilityAction[] { action };
            var controller=source.AddComponent<CombatAbilities>(); controller.Configure(new[] { ability }); controller.TryActivate(ability,default,target);
            yield return new WaitForSeconds(.15f); Assert.That(target.Current,Is.EqualTo(90)); Assert.That(controller.IsCasting,Is.False);
        }
        [UnityTest] public IEnumerator BeamSchedulerRunsAndUnregistersCompletedBeam()
        {
            var recipe=Asset<BeamDefinition>(); recipe.Attack=Asset<AttackDefinition>(); recipe.Attack.Damage=1; recipe.Duration=.09f; recipe.Interval=.03f; recipe.PulseImmediately=false;
            var beam=source.AddComponent<CombatBeam>(); beam.Begin(recipe); yield return new WaitForSeconds(.2f);
            Assert.That(target.Current,Is.EqualTo(97)); Assert.That(beam.IsRunning,Is.False);
        }
        [UnityTest] public IEnumerator MeleeAndSensorCanShareDeliveryScheduler()
        {
            var meleeRecipe=Asset<MeleeDefinition>(); meleeRecipe.Attack=Asset<AttackDefinition>(); meleeRecipe.Attack.Damage=3;
            meleeRecipe.Duration=.09f; meleeRecipe.Radius=1.1f; meleeRecipe.Windows=new[] { new MeleeWindow(0,.09f) };
            var melee=source.AddComponent<CombatMelee>(); melee.Begin(meleeRecipe);
            var sensorRecipe=Asset<AreaTriggerDefinition>(); sensorRecipe.Area=Asset<AreaDefinition>(); sensorRecipe.Duration=.09f; sensorRecipe.EmitExitOnStop=false;
            var sensor=source.AddComponent<CombatAreaTrigger>(); sensor.Begin(sensorRecipe,source);
            Assert.That(sensor.OccupantCount,Is.EqualTo(1)); yield return new WaitForSeconds(.2f);
            Assert.That(target.Current,Is.EqualTo(97)); Assert.That(melee.IsRunning,Is.False); Assert.That(sensor.IsRunning,Is.False);
        }
    }
}
