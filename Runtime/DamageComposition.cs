using System;
using UnityEngine;

namespace EasyCombat
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DamageChannel
    {
        public DamageTypeDefinition Type;
        [Min(0)] public float Minimum;
        [Min(0)] public float Maximum;
        public DamageChannel(DamageTypeDefinition type, float minimum, float maximum)
        { Type = type; Minimum = minimum; Maximum = maximum; }
        public bool IsValid => CombatMath.Finite(Minimum) && CombatMath.Finite(Maximum) && Minimum >= 0 && Maximum >= Minimum;
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DamagePart
    {
        public readonly DamageTypeDefinition Type;
        public readonly float Amount;
        public DamagePart(DamageTypeDefinition type, float amount) { Type = type; Amount = amount; }
    }

    /// <summary>Value snapshot: no rented array lifetime or per-shot allocation. Maximum eight channels per hit.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DamageAmounts
    {
        public const int Capacity = 8;
        private DamagePart a, b, c, d, e, f, g, h;
        public int Count { get; private set; }
        public DamagePart this[int index] => index >= 0 && index < Count ? index switch
        { 0 => a, 1 => b, 2 => c, 3 => d, 4 => e, 5 => f, 6 => g, _ => h } : throw new ArgumentOutOfRangeException(nameof(index));
        public void Add(DamageTypeDefinition type, float amount)
        {
            var part = new DamagePart(type, amount);
            switch (Count)
            {
                case 0: a = part; break; case 1: b = part; break; case 2: c = part; break; case 3: d = part; break;
                case 4: e = part; break; case 5: f = part; break; case 6: g = part; break; case 7: h = part; break;
                default: throw new InvalidOperationException("A hit supports at most eight damage channels.");
            }
            Count++;
        }
        public float Total { get { var total = 0f; for (var i = 0; i < Count; i++) total += this[i].Amount; return total; } }
        public bool IsValid
        {
            get
            {
                for (var i = 0; i < Count; i++) if (!CombatMath.Finite(this[i].Amount) || this[i].Amount < 0) return false;
                return CombatMath.Finite(Total);
            }
        }
        public DamageAmounts Scale(float multiplier)
        {
            var result = new DamageAmounts();
            for (var i = 0; i < Count; i++) result.Add(this[i].Type, this[i].Amount * multiplier);
            return result;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct DamagePartResult
    {
        public readonly DamageTypeDefinition Type;
        public readonly float Incoming, Resistance, Effective, Applied;
        public readonly float HealthDamage, ShieldAbsorbed, ArmorPrevented, ResistanceDelta;
        public float Overkill => Mathf.Max(0, HealthDamage - Applied);
        public DamagePartResult(DamageTypeDefinition type, float incoming, float resistance, float effective, float applied,
            float? healthDamage = null, float shieldAbsorbed = 0, float armorPrevented = 0, float? resistanceDelta = null)
        { Type = type; Incoming = incoming; Resistance = resistance; Effective = effective; Applied = applied;
          HealthDamage = healthDamage ?? effective; ShieldAbsorbed = shieldAbsorbed; ArmorPrevented = armorPrevented; ResistanceDelta = resistanceDelta ?? incoming - effective; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public struct DamageBreakdown
    {
        private DamagePartResult a, b, c, d, e, f, g, h;
        public int Count { get; private set; }
        public DamagePartResult this[int index] => index >= 0 && index < Count ? index switch
        { 0 => a, 1 => b, 2 => c, 3 => d, 4 => e, 5 => f, 6 => g, _ => h } : throw new ArgumentOutOfRangeException(nameof(index));
        internal void Add(DamagePartResult part)
        {
            switch (Count)
            {
                case 0: a = part; break; case 1: b = part; break; case 2: c = part; break; case 3: d = part; break;
                case 4: e = part; break; case 5: f = part; break; case 6: g = part; break; case 7: h = part; break;
                default: throw new InvalidOperationException("Damage breakdown capacity exceeded.");
            }
            Count++;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public enum StatusApplyPolicy { AfterDamage, OnContact, AfterDamageOrShield }
}
