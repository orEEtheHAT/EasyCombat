using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class TimedDeliveryTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private readonly Vector3 center=new Vector3(0,3200,0);
        private GameObject source;
        private Health target;
        private T Asset<T>() where T:ScriptableObject { var a=ScriptableObject.CreateInstance<T>(); cleanup.Add(a); return a; }
        private GameObject Actor(string name,Vector3 position)
        { var go=new GameObject(name); cleanup.Add(go); go.transform.position=position; return go; }
        [SetUp] public void Setup()
        {
            source=Actor("Source",center+Vector3.back*4); source.AddComponent<Health>();
            var go=Actor("Target",center); go.AddComponent<SphereCollider>().radius=.25f; target=go.AddComponent<Health>();
        }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        private CombatMelee Melee(out MeleeDefinition recipe,out Transform tip)
        {
            recipe=Asset<MeleeDefinition>(); recipe.Attack=Asset<AttackDefinition>(); recipe.Attack.Damage=10; recipe.Duration=1; recipe.Windows=new[] { new MeleeWindow(0,1) };
            tip=Actor("Tip",center+Vector3.left*3).transform;
            var melee=source.AddComponent<CombatMelee>(); melee.AutomaticAdvance=false; melee.SetAnchors(tip); return melee;
        }
        [Test] public void FastMeleeSweepsAcrossTargetWithoutEndpointOverlap()
        {
            var melee=Melee(out var recipe,out var tip); Physics.SyncTransforms(); melee.Begin(recipe); tip.position=center+Vector3.right*3; Physics.SyncTransforms(); melee.Advance(1);
            Assert.That(target.Current,Is.EqualTo(90)); Assert.That(melee.IsRunning,Is.False);
        }
        [Test] public void DamageWindowClipsMotionToActiveTime()
        {
            var melee=Melee(out var recipe,out var tip); recipe.Windows=new[] { new MeleeWindow(.75f,1) };
            Physics.SyncTransforms(); melee.Begin(recipe); tip.position=center+Vector3.right*3; Physics.SyncTransforms(); melee.Advance(1); Assert.That(target.Current,Is.EqualTo(100));
        }
        [TestCase(MeleeRepeatPolicy.OncePerSwing,90)] [TestCase(MeleeRepeatPolicy.OncePerWindow,80)]
        public void RepeatPolicyDistinguishesSwingAndWindows(MeleeRepeatPolicy policy,float expected)
        {
            var melee=Melee(out var recipe,out var tip); recipe.Repeat=policy; recipe.Windows=new[] { new MeleeWindow(0,.3f),new MeleeWindow(.6f,.9f) };
            tip.position=center; Physics.SyncTransforms(); melee.Begin(recipe); melee.Advance(1); Assert.That(target.Current,Is.EqualTo(expected));
        }
        [Test] public void SeveralWeaponSamplesDoNotMultiplyDamage()
        {
            var melee=Melee(out var recipe,out var tip); tip.position=center; var second=Actor("SecondTip",center).transform; melee.SetAnchors(tip,second);
            Physics.SyncTransforms(); melee.Begin(recipe); melee.Advance(.2f); melee.Advance(.2f); Assert.That(target.Current,Is.EqualTo(90));
        }
        [Test] public void IntervalMeleeCanHitAgainAfterCooldown()
        {
            var melee=Melee(out var recipe,out var tip); recipe.Repeat=MeleeRepeatPolicy.Interval; recipe.RepeatInterval=.3f; tip.position=center; Physics.SyncTransforms();
            melee.Begin(recipe); melee.Advance(.1f); melee.Advance(.1f); Assert.That(target.Current,Is.EqualTo(90)); melee.Advance(.3f); Assert.That(target.Current,Is.EqualTo(80));
        }
        private CombatBeam Beam(out BeamDefinition recipe)
        {
            recipe=Asset<BeamDefinition>(); recipe.Attack=Asset<AttackDefinition>(); recipe.Attack.Damage=20; recipe.Duration=1; recipe.Interval=.25f; recipe.PulseImmediately=false;
            var beam=source.AddComponent<CombatBeam>(); beam.AutomaticAdvance=false; return beam;
        }
        [Test] public void BeamPerSecondDamageHonorsPulsesAndStopsAtDuration()
        {
            var beam=Beam(out var recipe); recipe.DamagePerSecond=true; Physics.SyncTransforms(); Assert.That(beam.Begin(recipe),Is.True); beam.Advance(1);
            Assert.That(target.Current,Is.EqualTo(80)); Assert.That(beam.IsRunning,Is.False);
        }
        [Test] public void BeamStopsBeforeUnpaidPulse()
        {
            var beam=Beam(out var recipe); var mana=Asset<ResourceDefinition>(); mana.Maximum=25;
            var profile=Asset<ResourceProfile>(); profile.Resources=new[] { mana }; var resources=source.AddComponent<CombatResources>(); resources.Configure(profile);
            recipe.PulseCosts=new[] { new ResourceCost(mana,10) }; Physics.SyncTransforms(); beam.Begin(recipe); beam.Advance(1);
            resources.TryGet(mana,out var view); Assert.That(view.Current,Is.EqualTo(5)); Assert.That(target.Current,Is.EqualTo(60)); Assert.That(beam.IsRunning,Is.False);
        }
        [Test] public void BeamStopsOnSourcePoolReuse()
        {
            var beam=Beam(out var recipe); Physics.SyncTransforms(); beam.Begin(recipe); source.GetComponent<Health>().ResetToFull(); beam.Advance(1);
            Assert.That(target.Current,Is.EqualTo(100)); Assert.That(beam.IsRunning,Is.False);
        }
        [Test] public void BeamCatchupBudgetPreservesRemainingPulses()
        {
            var beam=Beam(out var recipe); recipe.Interval=.1f; recipe.Attack.Damage=1; Physics.SyncTransforms(); beam.Begin(recipe); beam.Advance(1);
            Assert.That(target.Current,Is.EqualTo(92)); Assert.That(beam.IsRunning,Is.True); beam.Advance(0);
            Assert.That(target.Current,Is.EqualTo(90)); Assert.That(beam.IsRunning,Is.False);
        }
        [Test] public void AbilityAndImmediateBeamCostsAreValidatedTogether()
        {
            Beam(out var recipe); recipe.PulseImmediately=true;
            var mana=Asset<ResourceDefinition>(); var profile=Asset<ResourceProfile>(); profile.Resources=new[] { mana }; var resources=source.AddComponent<CombatResources>(); resources.Configure(profile);
            recipe.PulseCosts=new[] { new ResourceCost(mana,90) }; var step=Asset<AbilityBeamAction>(); step.Beam=recipe;
            var ability=Asset<AbilityDefinition>(); ability.TargetMode=AbilityTargetMode.Self; ability.Costs=new[] { new ResourceCost(mana,20) }; ability.Actions=new CombatAbilityAction[] { step };
            var controller=source.AddComponent<CombatAbilities>(); controller.Configure(new[] { ability });
            Assert.That(controller.TryActivate(ability,default,null,out var reason),Is.False); Assert.That(reason,Is.EqualTo(AbilityRejection.Resources));
            resources.TryGet(mana,out var view); Assert.That(view.Current,Is.EqualTo(100));
        }
    }
}
