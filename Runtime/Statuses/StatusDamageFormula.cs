using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum StatusDamageBasis { Fixed, TriggeringHit, TargetCurrentHealth, TargetMaximumHealth, TargetMissingHealth, SourceStat }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum HitDamageStage { Incoming, AfterResistance, HealthLost }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum StatusValueSampling { OnApplication, EveryTick }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum StatusDamageUnit { PerTick, PerSecond, OverDuration }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct StatusDamageTerm
    {
        public StatusDamageBasis Basis;
        [Min(0), Tooltip("Fixed: damage units. Other bases: fraction (0.2 = 20%).")]
        public float Value;
        public HitDamageStage HitStage;
        public bool FilterHitType;
        public DamageTypeDefinition HitType;
        public CombatStatDefinition Stat;
        public StatusValueSampling Sampling;

        public StatusDamageTerm(StatusDamageBasis basis, float value, StatusValueSampling sampling = StatusValueSampling.OnApplication)
        { Basis = basis; Value = value; Sampling = sampling; HitStage = HitDamageStage.HealthLost; FilterHitType = false; HitType = null; Stat = null; }
        public bool IsValid => CombatMath.Finite(Value) && Value >= 0
            && Basis >= StatusDamageBasis.Fixed && Basis <= StatusDamageBasis.SourceStat
            && (Basis != StatusDamageBasis.TriggeringHit || HitStage >= HitDamageStage.Incoming && HitStage <= HitDamageStage.HealthLost)
            && (Basis != StatusDamageBasis.SourceStat || Stat != null)
            && ((Basis != StatusDamageBasis.SourceStat && Basis != StatusDamageBasis.TargetMaximumHealth)
                || Sampling == StatusValueSampling.OnApplication || Sampling == StatusValueSampling.EveryTick);
        public bool IsLive => Basis == StatusDamageBasis.TargetCurrentHealth || Basis == StatusDamageBasis.TargetMissingHealth
            || ((Basis == StatusDamageBasis.TargetMaximumHealth || Basis == StatusDamageBasis.SourceStat) && Sampling == StatusValueSampling.EveryTick);

        internal bool TryEvaluate(in StatusCaptureContext context, out float value) =>
            DamageFormulaMath.TryValue(this, context.Target != null ? context.Target.Current : 0,
                context.Target != null ? context.Target.Maximum : 0, context.Target != null,
                context.Source, context.Hit, context.HasHit, out value);
    }
}
