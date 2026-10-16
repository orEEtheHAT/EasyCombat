using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EasyCombat.Editor
{
    [Serializable] public sealed class CombatSimulationReport
    {
        public bool Success,Truncated,Killed;
        public string Error;
        public int Shots,ResolvedHits,RejectedHits,Steps,OverdueStatusRecords;
        public double Seconds,TimeToKill,HealthDamage,ShieldAbsorbed,ArmorPrevented,ResistanceDelta,LifePrevented,Redirected,Overkill;
        public float SourceHealth,TargetHealth,TargetShield;
        public readonly List<CombatRecord> Records=new List<CombatRecord>();
        public readonly List<CombatSimulationPoint> Timeline=new List<CombatSimulationPoint>();
    }
    public readonly struct CombatSimulationPoint
    {
        public readonly float Time,Health,Shield;
        public CombatSimulationPoint(float time,float health,float shield) { Time=time; Health=health; Shield=shield; }
    }
    public static class CombatBattleSimulation
    {
        public const int MaximumSteps=100000,MaximumRecords=2048,MaximumPoints=2048;
        public static CombatSimulationReport Run(CombatScenarioDefinition scenario)
        {
            var report=new CombatSimulationReport();
            if(EditorApplication.isPlayingOrWillChangePlaymode) { report.Error="Exit Play mode before running a calculator scenario."; return report; }
            if(scenario==null) { report.Error="Select a scenario."; return report; }
            var graph=CombatDependencyAnalyzer.Analyze(new[] { scenario });
            if(graph.Cycles>0 || graph.Truncated) { report.Error="Scenario dependency graph contains cycles or exceeds the analysis budget."; return report; }
            if(!scenario.IsValid) { report.Error="Scenario requires valid attack, health and timing settings."; return report; }
            if(!scenario.AllowCustomBehaviors && !KnownBehaviors(scenario)) { report.Error="Custom behavior detected. Enable it only if it obeys the isolated/local-target contract."; return report; }
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var physics=scene.GetPhysicsScene();
                if(!physics.IsValid() || physics.Equals(Physics.defaultPhysicsScene)) throw new InvalidOperationException("An isolated physics scene is required.");
                var clock=new ManualCombatClock();
                using(new CombatExecutionScope(new SeededCombatRandom(unchecked((uint)scenario.Seed)),clock,true,physics))
                {
                    var source=Actor("Scenario Source",scene,scenario.AttackFromBehind ? Vector3.back*5 : Vector3.forward*5,scenario.SourceHealth,scenario.SourceStats,scenario.SourceResources,0);
                    var target=Actor("Scenario Target",scene,Vector3.zero,scenario.TargetHealth,scenario.TargetStats,scenario.TargetResources,1);
                    var sourceStatuses=source.gameObject.AddComponent<StatusController>(); var targetStatuses=target.gameObject.AddComponent<StatusController>();
                    if(scenario.SourceOutgoing!=null) source.gameObject.AddComponent<CombatDamageModifiers>().Outgoing=scenario.SourceOutgoing;
                    if(scenario.TargetIncoming!=null) target.gameObject.AddComponent<CombatDamageModifiers>().Incoming=scenario.TargetIncoming;
                    if(scenario.SourceReactions.Length>0) source.gameObject.AddComponent<CombatReactions>().Configure(scenario.SourceReactions);
                    if(scenario.TargetReactions.Length>0) target.gameObject.AddComponent<CombatReactions>().Configure(scenario.TargetReactions);
                    target.Resolved+=record=>
                    {
                        if(record.Target.Id!=target.Identity.Id) return; var result=record.Result;
                        report.ResolvedHits++; if(result.Rejection!=DamageRejection.None) report.RejectedHits++;
                        report.HealthDamage+=result.AppliedAmount; report.ShieldAbsorbed+=result.ShieldAbsorbed; report.ArmorPrevented+=result.ArmorPrevented;
                        report.ResistanceDelta+=result.ResistanceDelta; report.LifePrevented+=result.LifePrevented; report.Redirected+=result.Redirected; report.Overkill+=result.Overkill;
                        if(result.Killed && !report.Killed) { report.Killed=true; report.TimeToKill=clock.Now; }
                        if(report.Records.Count<MaximumRecords) report.Records.Add(record);
                    };
                    foreach(var status in scenario.SourceStatuses) if(!sourceStatuses.Apply(status,source.gameObject)) throw new InvalidOperationException("Could not apply source status: "+status.Label);
                    foreach(var status in scenario.TargetStatuses) if(!targetStatuses.Apply(status,source.gameObject)) throw new InvalidOperationException("Could not apply target status: "+status.Label);
                    Physics.SyncTransforms();
                    var sourceResources=source.GetComponent<CombatResources>(); var targetResources=target.GetComponent<CombatResources>();
                    var end=Math.Max(scenario.ObserveSeconds,(scenario.Shots-1)*(double)scenario.ShotInterval); var next=0;
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    report.Timeline.Add(new CombatSimulationPoint(0,target.Current,target.ShieldCurrent));
                    while(true)
                    {
                        if(timer.ElapsedMilliseconds>2500 || report.ResolvedHits>=100000) { report.Truncated=true; break; }
                        while(next<scenario.Shots && next*(double)scenario.ShotInterval<=clock.Now+1e-7)
                        {
                            next++;
                            if(CombatControl.IsAllowed(source.gameObject,CombatRestriction.Attack))
                            {
                                var multiplier=sourceStatuses.GetStatMultiplier(scenario.Attack.DamageModifierStat);
                                var request=scenario.Attack.RollRandom(source.gameObject,target.transform.position,(target.transform.position-source.transform.position).normalized,multiplier).Scale(scenario.HurtboxMultiplier);
                                if(!request.IsValid) throw new InvalidOperationException("Attack could not prepare damage; check source stats/formulas.");
                                target.ApplyDamage(request); report.Shots++;
                            }
                            if(scenario.StopOnTargetDeath && !target.IsAlive) break;
                        }
                        if(clock.Now>=end-1e-7 || scenario.StopOnTargetDeath && !target.IsAlive) break;
                        if(report.Steps++>=MaximumSteps) { report.Truncated=true; break; }
                        var dt=Math.Min(scenario.StepSeconds,end-clock.Now);
                        if(next<scenario.Shots) dt=Math.Min(dt,Math.Max(0,next*(double)scenario.ShotInterval-clock.Now));
                        if(dt<=0) break; clock.Advance(dt);
                        source.AdvanceDefense((float)dt); target.AdvanceDefense((float)dt);
                        sourceResources?.Advance((float)dt); targetResources?.Advance((float)dt);
                        sourceStatuses.Advance((float)dt); targetStatuses.Advance((float)dt);
                        if(report.Timeline.Count<MaximumPoints) report.Timeline.Add(new CombatSimulationPoint((float)clock.Now,target.Current,target.ShieldCurrent));
                    }
                    report.Timeline.Add(new CombatSimulationPoint((float)clock.Now,target.Current,target.ShieldCurrent));
                    report.Seconds=clock.Now; report.SourceHealth=source.Current; report.TargetHealth=target.Current; report.TargetShield=target.ShieldCurrent; report.Success=true;
                    report.OverdueStatusRecords=sourceStatuses.OverdueTickRecords+targetStatuses.OverdueTickRecords;
                }
            }
            catch(Exception exception) { report.Error=exception.Message; }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return report;
        }
        private static Health Actor(string name,Scene scene,Vector3 position,HealthDefinition profile,CombatStatsDefinition stats,ResourceProfile resources,int team)
        {
            var actor=new GameObject(name) { hideFlags=HideFlags.HideAndDontSave }; SceneManager.MoveGameObjectToScene(actor,scene); actor.transform.position=position;
            actor.AddComponent<SphereCollider>().radius=.5f; actor.AddComponent<CombatTeam>().Team=team; var health=actor.AddComponent<Health>();
            if(profile!=null) health.ApplyDefinition(profile);
            if(stats!=null) actor.AddComponent<CombatStats>().Profile=stats;
            if(resources!=null && !actor.AddComponent<CombatResources>().Configure(resources)) throw new InvalidOperationException("Resource capacities require source stats.");
            return health;
        }
        private static bool KnownBehaviors(CombatScenarioDefinition scenario)
        {
            var pending=new Stack<UnityEngine.Object>(); var visited=new HashSet<UnityEngine.Object>(); pending.Push(scenario);
            while(pending.Count>0)
            {
                var value=pending.Pop(); if(value==null || !visited.Add(value)) continue;
                if(value is StatusTickEffect && !(value is PeriodicDamageEffect) && !(value is PeriodicHealingEffect)) return false;
                if(value is DefenseLayerDefinition && value.GetType().Assembly!=typeof(Health).Assembly) return false;
                if(value is CombatReactionAction && value.GetType().Assembly!=typeof(Health).Assembly) return false;
                using(var serialized=new SerializedObject(value))
                {
                    var property=serialized.GetIterator(); while(property.Next(true))
                        if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue is ScriptableObject reference) pending.Push(reference);
                }
            }
            return true;
        }
    }
}
