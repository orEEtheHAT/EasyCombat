using UnityEngine;

namespace EasyCombat
{
    /// <summary>Only used while capturing a new application; never stored on the shared SO.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct StatusCaptureContext
    {
        public readonly Health Target;
        public readonly GameObject Source;
        public readonly StatusDefinition Definition;
        public readonly DamageResult Hit;
        public readonly bool HasHit;
        public StatusCaptureContext(Health target, GameObject source, StatusDefinition definition, DamageResult hit = default, bool hasHit = false)
        { Target = target; Source = source; Definition = definition; Hit = hit; HasHit = hasHit; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct StatusTickContext
    {
        public readonly StatusController Controller;
        public readonly Health Target;
        public readonly GameObject Source;
        public readonly StatusDefinition Definition;
        public readonly int Stacks;
        public readonly DamageOrigin Origin;
        public readonly long StackId;
        public readonly float CapturedValue, Duration, Interval;
        public readonly bool HasCapturedValue;
        public readonly double PlannedTickCount;
        public readonly DamageFormulaSnapshot Formula;
        public StatusTickContext(StatusController controller, Health target, GameObject source, StatusDefinition definition, int stacks,
            DamageOrigin origin = default, long stackId = 0, float capturedValue = 0, bool hasCapturedValue = false, float duration = 0, float interval = 0, double plannedTickCount = 0,
            DamageFormulaSnapshot formula = default)
        {
            Controller = controller; Target = target; Source = source; Definition = definition; Stacks = stacks; Origin = origin; StackId = stackId;
            CapturedValue = capturedValue; HasCapturedValue = hasCapturedValue;
            Duration = duration > 0 ? duration : definition != null ? definition.Duration : 0;
            Interval = interval > 0 ? interval : definition != null ? definition.TickInterval : 0;
            PlannedTickCount = plannedTickCount > 0 ? plannedTickCount : PeriodicDamageEffect.PlannedTicks(Duration, Interval);
            Formula = formula;
        }
    }

    /// <summary>Stateless reusable behaviour. Timers and stack counts belong to StatusController.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public abstract class StatusTickEffect : ScriptableObject
    {
        public virtual bool IsValid => true;
        /// <summary>Capture one immutable scalar for this effect/application. False rejects the whole application atomically.</summary>
        public virtual bool TryCapture(in StatusCaptureContext context, out float value) { value = 0; return IsValid; }
        public virtual bool TryCaptureState(in StatusCaptureContext context, out float value, out DamageFormulaSnapshot formula)
        { formula = default; return TryCapture(in context, out value); }
        public abstract void Execute(in StatusTickContext context);
    }
}
