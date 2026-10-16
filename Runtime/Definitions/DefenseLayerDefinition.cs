using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum DefenseHitKinds { All, Direct, Periodic }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DefenseLayerState
    {
        public float Current, Maximum, RechargeDelay;
        public bool Enabled;
    }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DefenseContext
    {
        public readonly Health Target;
        public readonly DamageInfo Damage;
        public DefenseContext(Health target, in DamageInfo damage) { Target = target; Damage = damage; }
    }
    /// <summary>Stateless damage reducer. Never mutate actors/SOs or emit events here; only modify the supplied value state.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public abstract class DefenseLayerDefinition : ScriptableObject
    {
        public string DisplayName;
        public DefenseHitKinds HitKinds;
        public bool FilterTypes;
        public DamageTypeDefinition[] Types = Array.Empty<DamageTypeDefinition>();
        public virtual bool IsShield => false;
        public virtual bool IsValid => HitKinds >= DefenseHitKinds.All && HitKinds <= DefenseHitKinds.Periodic;
        public virtual DefenseLayerState CreateState() => new DefenseLayerState { Enabled = true };
        public virtual bool NeedsAdvance(in DefenseLayerState state) => false;
        public virtual void Advance(ref DefenseLayerState state, float deltaTime) { }
        public bool Accepts(in DamageInfo damage) => HitKinds == DefenseHitKinds.All
            || (damage.IsPeriodic ? HitKinds == DefenseHitKinds.Periodic : HitKinds == DefenseHitKinds.Direct);
        public bool AcceptsType(DamageTypeDefinition type) => !FilterTypes || Types != null && Array.IndexOf(Types, type) >= 0;
        public abstract DamageAmounts Reduce(in DefenseContext context, DamageAmounts incoming, ref DefenseLayerState state);
        protected float MatchingTotal(DamageAmounts incoming)
        { var sum = 0f; for (var i = 0; i < incoming.Count; i++) if (AcceptsType(incoming[i].Type)) sum += incoming[i].Amount; return sum; }
        protected DamageAmounts ScaleMatching(DamageAmounts incoming, float scale)
        {
            var result = new DamageAmounts();
            for (var i = 0; i < incoming.Count; i++)
            { var part = incoming[i]; result.Add(part.Type, part.Amount * (AcceptsType(part.Type) ? scale : 1)); }
            return result;
        }
    }
}
