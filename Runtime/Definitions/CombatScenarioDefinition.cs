using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Tools/Battle Scenario",fileName="BattleScenario")]
    public sealed class CombatScenarioDefinition:ScriptableObject
    {
        public AttackDefinition Attack;
        public HealthDefinition SourceHealth,TargetHealth;
        public CombatStatsDefinition SourceStats,TargetStats;
        public ResourceProfile SourceResources,TargetResources;
        public DamageModifierProfile SourceOutgoing,TargetIncoming;
        public StatusDefinition[] SourceStatuses=Array.Empty<StatusDefinition>(),TargetStatuses=Array.Empty<StatusDefinition>();
        public ReactionDefinition[] SourceReactions=Array.Empty<ReactionDefinition>(),TargetReactions=Array.Empty<ReactionDefinition>();
        [Range(1,1000)] public int Shots=1;
        [Min(0),Tooltip("Explicit spacing for this test, replacing weapon RPM.")] public float ShotInterval=.25f;
        [Range(0,3600)] public float ObserveSeconds=5;
        [Range(.001f,1)] public float StepSeconds=.02f;
        [Min(.001f)] public float HurtboxMultiplier=1;
        public bool AttackFromBehind;
        public int Seed=12345;
        public bool StopOnTargetDeath=true;
        [Tooltip("Custom extensions must obey the stateless/local-target contract and use EasyCombat queries to remain isolated.")] public bool AllowCustomBehaviors;
        public bool IsValid=>Attack!=null && Attack.IsValid && TargetHealth!=null && TargetHealth.IsValid
            && (SourceHealth==null || SourceHealth.IsValid) && (SourceStats==null || SourceStats.IsValid) && (TargetStats==null || TargetStats.IsValid)
            && (SourceResources==null || SourceResources.IsValid) && (TargetResources==null || TargetResources.IsValid)
            && (SourceOutgoing==null || SourceOutgoing.IsValid) && (TargetIncoming==null || TargetIncoming.IsValid)
            && Shots>=1 && Shots<=1000 && CombatMath.Finite(ShotInterval) && ShotInterval>=0 && ShotInterval<=60
            && CombatMath.Finite(ObserveSeconds) && ObserveSeconds>=0 && ObserveSeconds<=3600 && CombatMath.Finite(StepSeconds) && StepSeconds>=.001f && StepSeconds<=1
            && CombatMath.Positive(HurtboxMultiplier) && ValidStatuses(SourceStatuses) && ValidStatuses(TargetStatuses) && ValidReactions(SourceReactions) && ValidReactions(TargetReactions);
        private static bool ValidStatuses(StatusDefinition[] values)
        { if(values==null || values.Length>32) return false; foreach(var value in values) if(value==null || !value.IsValid) return false; return true; }
        private static bool ValidReactions(ReactionDefinition[] values)
        { if(values==null || values.Length>32) return false; foreach(var value in values) if(value==null || !value.IsValid) return false; return true; }
    }
}
