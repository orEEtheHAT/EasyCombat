using System;
using UnityEngine;

namespace EasyCombat
{
    public interface ICombatRandom { float NextUnit(); }
    public interface ICombatClock { double Now { get; } }
    /// <summary>Portable xorshift32 sequence. Zero is remapped to a nonzero seed.</summary>
    public sealed class SeededCombatRandom : ICombatRandom
    {
        private uint state;
        public SeededCombatRandom(uint seed) { state = seed == 0 ? 0x9e3779b9u : seed; }
        public float NextUnit()
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state >> 8) * (1f / 16777216f);
        }
    }
    public sealed class ManualCombatClock : ICombatClock
    {
        public double Now { get; private set; }
        public ManualCombatClock(double start = 0)
        { if (double.IsNaN(start) || double.IsInfinity(start) || start < 0) throw new ArgumentOutOfRangeException(nameof(start)); Now = start; }
        public void Advance(double delta)
        {
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0 || double.IsInfinity(Now+delta)) throw new ArgumentOutOfRangeException(nameof(delta));
            Now += delta;
        }
    }
    public static class CombatRandom
    {
        public static float Value
        {
            get
            {
                var source = CombatExecutionScope.Current?.Random;
                var value = source != null ? source.NextUnit() : UnityEngine.Random.value;
                if (!CombatMath.Finite(value) || value < 0 || value > 1) throw new InvalidOperationException("Combat RNG must return a finite sample in [0,1].");
                return value;
            }
        }
    }
    public static class CombatClock
    {
        public static double Now => CombatExecutionScope.Current?.Clock?.Now ?? Time.timeAsDouble;
    }
    /// <summary>Main-thread simulation scope. Dispose in reverse order; does not mutate UnityEngine.Random.state.</summary>
    public sealed class CombatExecutionScope : IDisposable
    {
        internal static CombatExecutionScope Current { get; private set; }
        internal readonly ICombatRandom Random;
        internal readonly ICombatClock Clock;
        private readonly CombatExecutionScope parent;
        private bool disposed;
        public CombatExecutionScope(ICombatRandom random = null, ICombatClock clock = null)
        { parent = Current; Random = random ?? parent?.Random; Clock = clock ?? parent?.Clock; Current = this; }
        public void Dispose()
        {
            if (disposed) return;
            if (!ReferenceEquals(Current,this)) throw new InvalidOperationException("Dispose combat scopes in reverse creation order.");
            Current = parent; disposed = true;
        }
    }
}
