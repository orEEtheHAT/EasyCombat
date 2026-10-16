using System;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public interface IDamageable
    {
        DamageResult ApplyDamage(DamageInfo damage);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum DamageRejection { None, InvalidAmount, Dead, Disabled, NoReceiver, Immune, NoEffect, Self, Ally, Enemy, Neutral, Environment, Invulnerable, Dodged, Blocked, InvalidDefense, Reentrant }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum HealthChangeReason { Damage, Healing, Reset, Restore, MaximumChanged, Revive }

    /// <summary>Immutable description shared by rays, projectiles, melee and environmental damage.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly GameObject Source;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly DamageTypeDefinition Type;
        public readonly bool IsCritical;
        public readonly bool IsPeriodic;
        public readonly StatusApplication[] OnHitStatuses;
        public readonly DamageAmounts Parts;
        public readonly DamageOrigin Origin;
        public readonly DamageProtection Protection;
        public readonly StatusApplyPolicy StatusPolicy;
        public readonly DamageFormulaSnapshot Formula;
        public bool HasFormula => Formula.Count > 0;
        public bool IsValid => Protection.IsValid && (HasFormula ? Formula.IsValid : Parts.IsValid && (Amount > 0 || (Amount == 0 && StatusPolicy == StatusApplyPolicy.OnContact
            && OnHitStatuses != null && OnHitStatuses.Length > 0)));

        public DamageInfo(float amount, GameObject source = null, Vector3 point = default, Vector3 direction = default,
            DamageTypeDefinition type = null, bool isCritical = false, bool isPeriodic = false, StatusApplication[] onHitStatuses = null,
            DamageOrigin origin = default, StatusApplyPolicy statusPolicy = StatusApplyPolicy.AfterDamage, DamageProtection protection = default)
        {
            Protection = protection; Formula = default;
            Amount = amount;
            Source = source;
            Point = point;
            Direction = direction;
            Type = type;
            IsCritical = isCritical;
            IsPeriodic = isPeriodic;
            OnHitStatuses = onHitStatuses;
            var parts = new DamageAmounts(); parts.Add(type, amount); Parts = parts;
            Origin = origin.CastId == 0 ? DamageOrigin.Capture(source) : origin;
            StatusPolicy = statusPolicy;
        }

        public DamageInfo(DamageAmounts parts, GameObject source = null, Vector3 point = default, Vector3 direction = default,
            bool isCritical = false, bool isPeriodic = false, StatusApplication[] onHitStatuses = null,
            DamageOrigin origin = default, StatusApplyPolicy statusPolicy = StatusApplyPolicy.AfterDamage, DamageProtection protection = default)
        {
            Protection = protection; Formula = default;
            Parts = parts; Amount = parts.Total; Type = parts.Count == 1 ? parts[0].Type : null;
            Source = source; Point = point; Direction = direction; IsCritical = isCritical; IsPeriodic = isPeriodic;
            OnHitStatuses = onHitStatuses; Origin = origin.CastId == 0 ? DamageOrigin.Capture(source) : origin; StatusPolicy = statusPolicy;
        }
        public DamageInfo(DamageFormulaSnapshot formula, GameObject source = null, Vector3 point = default, Vector3 direction = default,
            bool isPeriodic = false, StatusApplication[] onHitStatuses = null, DamageOrigin origin = default, StatusApplyPolicy statusPolicy = StatusApplyPolicy.AfterDamage, DamageProtection protection = default)
        {
            Protection = protection; Formula = formula; Parts = default; Amount = 0; Type = null;
            Source = source; Point = point; Direction = direction; IsCritical = formula.IsCritical; IsPeriodic = isPeriodic;
            OnHitStatuses = onHitStatuses; Origin = origin.CastId == 0 ? DamageOrigin.Capture(source) : origin; StatusPolicy = statusPolicy;
        }
        public bool TryResolve(float currentHealth, float maximumHealth, out DamageInfo resolved)
        {
            resolved = this;
            if (!Protection.IsValid) return false;
            if (!HasFormula) return IsValid;
            if (!Formula.TryResolve(currentHealth, maximumHealth, out var parts)) return false;
            resolved = new DamageInfo(parts, Source, Point, Direction, IsCritical, IsPeriodic, OnHitStatuses, Origin, StatusPolicy, Protection);
            return true;
        }
        public DamageInfo Scale(float multiplier) => HasFormula
            ? new DamageInfo(Formula.Scale(multiplier), Source, Point, Direction, IsPeriodic, OnHitStatuses, Origin, StatusPolicy, Protection)
            : new DamageInfo(Parts.Scale(multiplier), Source, Point, Direction, IsCritical, IsPeriodic, OnHitStatuses, Origin, StatusPolicy, Protection);
        public DamageInfo WithProtection(DamageProtection protection) => HasFormula
            ? new DamageInfo(Formula,Source,Point,Direction,IsPeriodic,OnHitStatuses,Origin,StatusPolicy,protection)
            : new DamageInfo(Parts,Source,Point,Direction,IsCritical,IsPeriodic,OnHitStatuses,Origin,StatusPolicy,protection);
        public DamageInfo Adjust(DamageModifierProfile profile)
        {
            if (profile == null) return this;
            if (!profile.IsValid) return default;
            if (HasFormula)
            {
                var adjusted = Formula.Adjust(profile,IsPeriodic,Origin.Action.Tags);
                return adjusted.IsValid ? new DamageInfo(adjusted,Source,Point,Direction,IsPeriodic,OnHitStatuses,Origin,StatusPolicy,Protection) : default;
            }
            var parts = new DamageAmounts();
            for (var i = 0; i < Parts.Count; i++)
            {
                var part = Parts[i];
                if (!profile.TryAdjust(part.Type,IsPeriodic,Origin.Action.Tags,out var type,out var multiplier)) return default;
                parts.Add(type,part.Amount*multiplier);
            }
            return new DamageInfo(parts,Source,Point,Direction,IsCritical,IsPeriodic,OnHitStatuses,Origin,StatusPolicy,Protection);
        }
        public DamageInfo WithAmount(float amount) => HasFormula ? throw new InvalidOperationException("Resolve target-dependent damage before replacing its amount.") : Amount > 0 ? Scale(amount / Amount)
            : new DamageInfo(amount, Source, Point, Direction, Type, IsCritical, IsPeriodic, OnHitStatuses, Origin, StatusPolicy, Protection);
        public DamageInfo At(Vector3 point, Vector3 direction) => HasFormula
            ? new DamageInfo(Formula, Source, point, direction, IsPeriodic, OnHitStatuses, Origin, StatusPolicy, Protection)
            : new DamageInfo(Parts, Source, point, direction, IsCritical, IsPeriodic, OnHitStatuses, Origin, StatusPolicy, Protection);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DamageResult
    {
        public readonly DamageInfo Damage;
        public readonly float AppliedAmount;
        public readonly bool Killed;
        public readonly bool Downed;
        public readonly DamageRejection Rejection;
        public readonly DamageBreakdown Breakdown;
        public readonly float EffectiveAmount;
        public readonly int StatusesApplied;
        public readonly float HealthDamage, ShieldAbsorbed, ArmorPrevented, ResistanceDelta;
        public readonly float LifePrevented;
        public readonly float Redirected;
        public readonly DefenseBreakdown Defenses;
        public float Overkill => Mathf.Max(0, HealthDamage - AppliedAmount - LifePrevented);
        public bool Applied => Rejection == DamageRejection.None && AppliedAmount > 0f;
        public bool HitApplied => Applied || Rejection == DamageRejection.None && ShieldAbsorbed > 0;

        public DamageResult(DamageInfo damage, float appliedAmount, bool killed, DamageRejection rejection = DamageRejection.None,
            DamageBreakdown breakdown = default, float effectiveAmount = 0, int statusesApplied = 0,
            float? healthDamage = null, float shieldAbsorbed = 0, float armorPrevented = 0, float? resistanceDelta = null, DefenseBreakdown defenses = default, bool downed = false, float lifePrevented = 0, float redirected = 0)
        {
            Damage = damage;
            AppliedAmount = appliedAmount;
            Killed = killed;
            Downed = downed;
            LifePrevented = lifePrevented;
            Redirected = redirected;
            Rejection = rejection;
            Breakdown = breakdown; EffectiveAmount = effectiveAmount; StatusesApplied = statusesApplied;
            HealthDamage = healthDamage ?? effectiveAmount; ShieldAbsorbed = shieldAbsorbed; ArmorPrevented = armorPrevented;
            ResistanceDelta = resistanceDelta ?? damage.Amount - effectiveAmount; Defenses = defenses;
        }

        public static DamageResult Rejected(DamageInfo damage, DamageRejection reason) =>
            new DamageResult(damage, 0f, false, reason);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct HealthChange
    {
        public readonly float Previous;
        public readonly float Current;
        public readonly float Maximum;
        public readonly HealthChangeReason Reason;
        public HealthChange(float previous, float current, float maximum, HealthChangeReason reason)
        {
            Previous = previous;
            Current = current;
            Maximum = maximum;
            Reason = reason;
        }
    }

    /// <summary>Plain data only. The saving system decides where and when to persist it.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct HealthSnapshot
    {
        public int Version;
        public float Maximum;
        public float Current;
        public CombatLifeState LifeState;
        public HealthSnapshot(float maximum, float current, CombatLifeState? lifeState = null)
        {
            Version = 2;
            Maximum = maximum;
            Current = current;
            LifeState = lifeState ?? (current>0 ? CombatLifeState.Alive : CombatLifeState.Dead);
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DamageImpact
    {
        public readonly Collider Collider;
        public readonly Vector3 Point;
        public readonly Vector3 Normal;
        public readonly DamageResult Result;
        public DamageImpact(Collider collider, Vector3 point, Vector3 normal, DamageResult result)
        {
            Collider = collider;
            Point = point;
            Normal = normal;
            Result = result;
        }
    }

    internal static class CombatMath
    {
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Positive(float value) => Finite(value) && value > 0f;
        public static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
