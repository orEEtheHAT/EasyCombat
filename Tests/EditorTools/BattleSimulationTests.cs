using System.Collections.Generic;
using EasyCombat.Editor;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat.Tests
{
    public sealed class BattleSimulationTests
    {
        private readonly List<Object> cleanup=new List<Object>();
        private T Asset<T>() where T:ScriptableObject { var value=ScriptableObject.CreateInstance<T>(); cleanup.Add(value); return value; }
        private CombatScenarioDefinition Scenario()
        { var s=Asset<CombatScenarioDefinition>(); s.Attack=Asset<AttackDefinition>(); s.Attack.Damage=10; s.TargetHealth=Asset<HealthDefinition>(); s.ObserveSeconds=0; return s; }
        [TearDown] public void Cleanup() { for(var i=cleanup.Count-1;i>=0;i--) if(cleanup[i]!=null) Object.DestroyImmediate(cleanup[i]); cleanup.Clear(); }
        [Test] public void CalculatorUsesRealDefenseAndStatusPipelineWithoutMutatingDefinitions()
        {
            var s=Scenario(); var armor=Asset<FlatArmorLayer>(); armor.Reduction=3; var defense=Asset<DefenseProfile>(); defense.Layers=new[] { new DefenseSlot(armor) }; s.TargetHealth.Defense=defense;
            var effect=Asset<PeriodicDamageEffect>(); effect.DamagePerStack=5; var status=Asset<StatusDefinition>(); status.TickEffects=new StatusTickEffect[] { effect }; status.Duration=1;
            s.Attack.OnHitStatuses=new[] { new StatusApplication(status) }; s.ObserveSeconds=1;
            var report=CombatBattleSimulation.Run(s); Assert.That(report.Success,Is.True,report.Error); Assert.That(report.HealthDamage,Is.EqualTo(9).Within(.001)); Assert.That(s.TargetHealth.MaximumHealth,Is.EqualTo(100)); Assert.That(s.Attack.Damage,Is.EqualTo(10));
        }
        [Test] public void GeometryReactionsStayInPreviewWorldAndGlobalJournalIsQuiet()
        {
            var real=new GameObject("Real scene target"); cleanup.Add(real); real.AddComponent<SphereCollider>(); var health=real.AddComponent<Health>();
            var s=Scenario(); var action=Asset<ReactionDamageAction>(); action.FixedDamage=7; action.Area=Asset<AreaDefinition>(); action.Area.Radius=10;
            var rule=Asset<ReactionDefinition>(); rule.Trigger=ReactionTrigger.DamageTaken; rule.Target=ReactionTarget.Owner; rule.Actions=new CombatReactionAction[] { action }; s.TargetReactions=new[] { rule };
            var global=0; System.Action<CombatRecord> listener=record=>global++; CombatEvents.DamageResolved+=listener;
            try { var report=CombatBattleSimulation.Run(s); Assert.That(report.Success,Is.True,report.Error); Assert.That(report.SourceHealth,Is.EqualTo(93)); Assert.That(health.Current,Is.EqualTo(100)); Assert.That(global,Is.Zero); }
            finally { CombatEvents.DamageResolved-=listener; }
        }
        [Test] public void SameSeedProducesSameDamageSequence()
        {
            var s=Scenario(); s.Attack.MinimumDamage=1; s.Attack.MaximumDamage=4; s.Attack.CriticalChance=.4f; s.Shots=10; s.ShotInterval=.1f; s.ObserveSeconds=1;
            var first=CombatBattleSimulation.Run(s); var second=CombatBattleSimulation.Run(s); Assert.That(first.Success && second.Success,Is.True);
            Assert.That(first.HealthDamage,Is.EqualTo(second.HealthDamage)); Assert.That(first.Records.Count,Is.EqualTo(second.Records.Count));
            for(var i=0;i<first.Records.Count;i++) Assert.That(first.Records[i].Result.AppliedAmount,Is.EqualTo(second.Records[i].Result.AppliedAmount));
        }
    }
}
