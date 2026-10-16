using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct ActiveStatusView
    {
        public readonly StatusDefinition Definition;
        public readonly int Stacks;
        public readonly float Remaining;
        public readonly float NextStackExpiresIn;
        public readonly bool Independent;
        public readonly int PendingStacks;
        public ActiveStatusView(StatusDefinition definition, int stacks, float remaining,
            float nextStackExpiresIn = 0, bool independent = false, int pendingStacks = 0)
        { Definition = definition; Stacks = stacks; Remaining = remaining; NextStackExpiresIn = nextStackExpiresIn; Independent = independent; PendingStacks = pendingStacks; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public readonly struct StatusStackView
    {
        public readonly long Id;
        public readonly float Remaining, NextTickIn;
        public readonly GameObject Source;
        public readonly DamageOrigin Origin;
        public StatusStackView(long id, float remaining, float nextTickIn, GameObject source, DamageOrigin origin)
        { Id = id; Remaining = remaining; NextTickIn = nextTickIn; Source = source; Origin = origin; }
    }

    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    [AddComponentMenu("EasyCombat/Status Controller")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class StatusController : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumActiveStatuses = 32;
        [SerializeField, Range(1, 32)] private int maximumCatchUpTicksPerStatus = 8;
        [SerializeField, Min(1), Tooltip("Total independent stack records on this actor, including expired stacks waiting for overdue ticks. New applications are rejected when this budget is full.")]
        private int maximumIndependentStackRecords = 512;
        private const double TickEpsilon = 0.0000001d;
        private struct CapturedEffect
        {
            public StatusTickEffect Effect;
            public float Value;
            public DamageFormulaSnapshot Formula;
        }
        private sealed class ApplicationData
        {
            public readonly List<CapturedEffect> Effects = new List<CapturedEffect>(2);
            public float Duration, Interval;
            public double PlannedTicks;
            public bool HasBudget;
        }
        private struct StackState
        {
            public ApplicationData Data;
            public long Id;
            public GameObject Source;
            public DamageOrigin Origin;
            public double ExpiresAt, NextTick, Interval;
        }
        private struct Entry
        {
            public ApplicationData Data;
            public StatusDefinition Definition;
            public GameObject Source;
            public DamageOrigin Origin;
            public int Stacks;
            public double ExpiresAt, NextTick, Interval;
            public List<StackState> IndependentStacks;
        }
        private struct Pending
        {
            public ApplicationData Data;
            public StatusDefinition Definition;
            public GameObject Source;
            public DamageOrigin Origin;
            public bool Remove;
        }
        private readonly List<Entry> entries = new List<Entry>(8);
        private readonly Stack<List<StackState>> stackLists = new Stack<List<StackState>>(8);
        private readonly Stack<ApplicationData> applicationPool = new Stack<ApplicationData>(8);
        private readonly Pending[] pending = new Pending[64];
        private readonly Dictionary<CombatStatDefinition, float> statMultipliers = new Dictionary<CombatStatDefinition, float>(8);
        private readonly Dictionary<DamageTypeDefinition, float> resistanceModifiers = new Dictionary<DamageTypeDefinition, float>(8);
        private Health health;
        private double clock;
        private int pendingCount;
        private int independentRecordCount;
        private long nextStackId;
        private bool advancing, clearRequested, modifiersDirty = true, bound;
        internal int SchedulerIndex = -1;
        internal bool NeedsTick => this != null && isActiveAndEnabled && entries.Count > 0;
        public int Count => entries.Count;
        public int IndependentRecordCount => independentRecordCount;
        public Health Health { get { if (!bound) BindHealth(); return health; } }
        public event Action Changed;

        private void Awake() => BindHealth();
        private void BindHealth()
        {
            if (bound) return;
            if (health == null) health = GetComponent<Health>();
            health.AttachStatuses(this);
            health.Changed += OnHealthChanged;
            health.Died += OnDeath;
            bound = true;
        }
        private void OnEnable()
        {
            BindHealth();
        }
        private void OnDisable()
        {
            if (health != null) { health.Changed -= OnHealthChanged; health.Died -= OnDeath; }
            bound = false;
            Clear();
            StatusScheduler.Unregister(this);
        }
        private void OnDestroy()
        {
            if (health != null) health.DetachStatuses(this);
            StatusScheduler.Unregister(this);
        }
        private void OnHealthChanged(HealthChange change)
        {
            if (change.Reason == HealthChangeReason.Reset || change.Reason == HealthChangeReason.Restore) Clear();
        }
        private void OnDeath(Health _) => Clear();
        public ActiveStatusView GetStatus(int index)
        {
            var entry = entries[index];
            if (entry.IndependentStacks == null)
            {
                var remaining = (float)Math.Max(0d, entry.ExpiresAt - clock);
                return new ActiveStatusView(entry.Definition, entry.Stacks, remaining, remaining, false, remaining == 0 ? 1 : 0);
            }
            var first = double.PositiveInfinity;
            var last = clock;
            var live = 0;
            foreach (var stack in entry.IndependentStacks)
            {
                if (stack.ExpiresAt <= clock) continue;
                live++; first = Math.Min(first, stack.ExpiresAt); last = Math.Max(last, stack.ExpiresAt);
            }
            return new ActiveStatusView(entry.Definition, live, (float)(last - clock), live > 0 ? (float)(first - clock) : 0,
                true, entry.IndependentStacks.Count - live);
        }
        public int GetStacks(StatusDefinition definition)
        {
            var index = Find(definition);
            return index >= 0 ? ActiveStacks(entries[index]) : 0;
        }
        /// <summary>Read one live independent stack. Shared modes expose their common state at index zero.</summary>
        public StatusStackView GetStack(StatusDefinition definition, int stackIndex)
        {
            var index = Find(definition);
            if (index < 0 || stackIndex < 0) throw new ArgumentOutOfRangeException(nameof(stackIndex));
            var entry = entries[index];
            if (entry.IndependentStacks == null)
            {
                if (stackIndex != 0) throw new ArgumentOutOfRangeException(nameof(stackIndex));
                return new StatusStackView(0, (float)Math.Max(0, entry.ExpiresAt - clock),
                    (float)Math.Max(0, entry.NextTick - clock), entry.Source, entry.Origin);
            }
            foreach (var stack in entry.IndependentStacks)
            {
                if (stack.ExpiresAt <= clock) continue;
                if (stackIndex-- == 0) return new StatusStackView(stack.Id, (float)(stack.ExpiresAt - clock),
                    (float)Math.Max(0, stack.NextTick - clock), stack.Source, stack.Origin);
            }
            throw new ArgumentOutOfRangeException(nameof(stackIndex));
        }
        private int ActiveStacks(Entry entry)
        {
            if (entry.IndependentStacks == null) return entry.Stacks;
            var count = 0;
            foreach (var stack in entry.IndependentStacks) if (stack.ExpiresAt > clock) count++;
            return count;
        }
        private int Find(StatusDefinition definition)
        {
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].Definition == definition) return i;
            return -1;
        }

        public bool Apply(StatusDefinition definition, GameObject source = null, DamageOrigin origin = default)
            => CaptureAndApply(definition, source, origin, default, false);

        /// <summary>Use a resolved hit for hit-dependent formulas. Health supplies this automatically for OnHitStatuses.</summary>
        public bool ApplyFromHit(StatusDefinition definition, in DamageResult hit)
            => CaptureAndApply(definition, hit.Damage.Source, hit.Damage.Origin, hit, true);

        private bool CaptureAndApply(StatusDefinition definition, GameObject source, DamageOrigin origin, DamageResult hit, bool hasHit)
        {
            if (definition == null || !definition.IsValid || !isActiveAndEnabled || !Health.IsAlive || Health.IsImmuneToStatus(definition)) return false;
            if (origin.CastId == 0) origin = DamageOrigin.Capture(source);
            origin = origin.WithStatus(definition);
            var data = applicationPool.Count > 0 ? applicationPool.Pop() : new ApplicationData();
            data.Duration = definition.Duration; data.Interval = definition.TickInterval;
            data.PlannedTicks = PeriodicDamageEffect.PlannedTicks(data.Duration, data.Interval);
            data.HasBudget = false;
            var context = new StatusCaptureContext(Health, source, definition, hit, hasHit);
            try
            {
                if (definition.TickEffects != null)
                    foreach (var effect in definition.TickEffects)
                    {
                        if (effect == null) { data.Effects.Add(default); continue; }
                        if (!effect.TryCaptureState(in context, out var value, out var formula) || !CombatMath.Finite(value)) { ReleaseData(data); return false; }
                        data.Effects.Add(new CapturedEffect { Effect = effect, Value = value, Formula = formula });
                        data.HasBudget |= effect is PeriodicDamageEffect periodic && periodic.Unit == StatusDamageUnit.OverDuration;
                    }
            }
            catch { ReleaseData(data); throw; }
            return ApplyPrepared(definition, source, origin, data);
        }

        private bool ApplyPrepared(StatusDefinition definition, GameObject source, DamageOrigin origin, ApplicationData data)
        {
            var accepted = false;
            try { accepted = ApplyCaptured(definition, source, origin, data); }
            finally { if (!accepted) ReleaseData(data); }
            if (accepted && !advancing)
            {
                modifiersDirty = true;
                StatusScheduler.Register(this);
                Changed?.Invoke();
            }
            return accepted;
        }

        private bool ApplyCaptured(StatusDefinition definition, GameObject source, DamageOrigin origin, ApplicationData data)
        {
            if (definition == null || !isActiveAndEnabled || !Health.IsAlive || Health.IsImmuneToStatus(definition)) return false;
            if (advancing) return Enqueue(definition, source, false, origin, data);
            var index = Find(definition);
            if (index >= 0)
            {
                var entry = entries[index];
                if (entry.IndependentStacks != null)
                {
                    if (!ApplyIndependent(entry.IndependentStacks, definition, source, origin, data)) return false;
                }
                else
                {
                    if (definition.Stacking == StatusStacking.IgnoreWhileActive) return false;
                    data.Interval = (float)entry.Interval;
                    data.PlannedTicks = Math.Max(0, Math.Floor((clock + data.Duration - entry.NextTick + TickEpsilon) / entry.Interval) + 1);
                    if (data.HasBudget && data.PlannedTicks < 1) return false;
                    if (definition.Stacking == StatusStacking.StackAndRefresh)
                        entry.Stacks = Mathf.Min(entry.Stacks + 1, definition.MaximumStacks);
                    entry.ExpiresAt = clock + data.Duration;
                    ReleaseData(entry.Data); entry.Data = data;
                    entry.Source = source; // Shared modes attribute the whole group to the last application.
                    entry.Origin = origin;
                    // Refresh does not reset the next tick: rapid hits cannot starve periodic damage.
                    entries[index] = entry;
                }
            }
            else
            {
                if (entries.Count >= maximumActiveStatuses) return false;
                if (definition.Stacking == StatusStacking.Independent)
                {
                    if (independentRecordCount >= maximumIndependentStackRecords) return false;
                    var list = stackLists.Count > 0 ? stackLists.Pop() : new List<StackState>(Math.Min(definition.MaximumStacks, 8));
                    list.Add(NewStack(source, origin, data)); independentRecordCount++;
                    entries.Add(new Entry { Definition = definition, IndependentStacks = list });
                }
                else
                    entries.Add(new Entry
                    {
                        Definition = definition, Source = source, Origin = origin, Stacks = 1, Data = data,
                        ExpiresAt = clock + data.Duration, Interval = data.Interval,
                        NextTick = data.Effects.Count > 0 ? clock + data.Interval : double.PositiveInfinity
                    });
            }
            return true;
        }

        private StackState NewStack(GameObject source, DamageOrigin origin, ApplicationData data) => new StackState
        {
            Id = ++nextStackId, Source = source, Origin = origin, Data = data, ExpiresAt = clock + data.Duration,
            Interval = data.Interval, NextTick = data.Effects.Count > 0 ? clock + data.Interval : double.PositiveInfinity
        };
        private bool ApplyIndependent(List<StackState> stacks, StatusDefinition definition, GameObject source, DamageOrigin origin, ApplicationData data)
        {
            var live = 0;
            var replacement = -1;
            for (var i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if (stack.ExpiresAt <= clock) continue;
                live++;
                if (replacement < 0 || stack.ExpiresAt < stacks[replacement].ExpiresAt
                    || (stack.ExpiresAt == stacks[replacement].ExpiresAt && stack.Id < stacks[replacement].Id)) replacement = i;
            }
            if (live >= definition.MaximumStacks)
            {
                if (definition.StackOverflow == StatusStackOverflow.IgnoreNew) return false;
                var old = stacks[replacement];
                if (old.NextTick > Math.Min(clock, old.ExpiresAt) + TickEpsilon)
                {
                    ReleaseData(old.Data);
                    stacks[replacement] = NewStack(source, origin, data);
                    return true;
                }
                // Preserve already-due ticks of the replaced stack, but stop its future lifetime.
                if (independentRecordCount >= maximumIndependentStackRecords) return false;
                old.ExpiresAt = Math.Min(clock, old.ExpiresAt); stacks[replacement] = old;
            }
            else if (independentRecordCount >= maximumIndependentStackRecords) return false;
            stacks.Add(NewStack(source, origin, data)); independentRecordCount++;
            return true;
        }

        internal int ApplyOnHit(StatusApplication[] applications, in DamageResult hit)
        {
            if (applications == null) return 0;
            var applied = 0;
            for (var i = 0; i < applications.Length; i++)
            {
                var application = applications[i];
                if (application.Chance >= 1f || (application.Chance > 0f && CombatRandom.Value < application.Chance))
                    if (ApplyFromHit(application.Status, in hit)) applied++;
            }
            return applied;
        }

        public bool Remove(StatusDefinition definition)
        {
            if (advancing) return Enqueue(definition, null, true);
            var index = Find(definition);
            if (index < 0) return false;
            RemoveAt(index);
            ReconcileScheduler();
            Changed?.Invoke();
            return true;
        }
        private bool Enqueue(StatusDefinition definition, GameObject source, bool remove, DamageOrigin origin = default, ApplicationData data = null)
        {
            if (pendingCount == pending.Length || clearRequested) return false;
            pending[pendingCount++] = new Pending { Definition = definition, Source = source, Origin = origin, Remove = remove, Data = data };
            return true;
        }
        private void RemoveAt(int index)
        {
            ReleaseStacks(entries[index]);
            entries[index] = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            modifiersDirty = true;
        }
        private void ReleaseStacks(Entry entry)
        {
            if (entry.IndependentStacks == null) { ReleaseData(entry.Data); return; }
            independentRecordCount -= entry.IndependentStacks.Count;
            foreach (var stack in entry.IndependentStacks) ReleaseData(stack.Data);
            entry.IndependentStacks.Clear();
            stackLists.Push(entry.IndependentStacks);
        }
        private void ReleaseData(ApplicationData data)
        {
            if (data == null) return;
            data.Effects.Clear();
            applicationPool.Push(data);
        }
        public void Clear()
        {
            if (advancing) { clearRequested = true; return; }
            var hadEntries = entries.Count > 0;
            for (var i = 0; i < entries.Count; i++) ReleaseStacks(entries[i]);
            entries.Clear();
            for (var i = 0; i < pendingCount; i++) ReleaseData(pending[i].Data);
            Array.Clear(pending, 0, pendingCount);
            pendingCount = 0;
            modifiersDirty = true;
            ReconcileScheduler();
            if (hadEntries) Changed?.Invoke();
        }

        /// <summary>Scaled simulation time. Catch-up work is capped per status; overdue ticks are retained.</summary>
        public void Advance(float deltaTime)
        {
            if (advancing || !CombatMath.Positive(deltaTime) || !isActiveAndEnabled || entries.Count == 0) return;
            if (!Health.IsAlive) { Clear(); return; }
            var previousClock = clock;
            clock += deltaTime;
            var changed = false;
            // Expire all modifiers before any tick queries Health, independent of entry order.
            foreach (var entry in entries)
            {
                if (entry.IndependentStacks == null)
                {
                    if (previousClock < entry.ExpiresAt && clock >= entry.ExpiresAt) { modifiersDirty = true; changed = true; }
                }
                else foreach (var stack in entry.IndependentStacks)
                    if (previousClock < stack.ExpiresAt && clock >= stack.ExpiresAt) { modifiersDirty = true; changed = true; break; }
            }
            advancing = true;
            try
            {
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    if (entry.Definition == null) { RemoveAt(i); changed = true; continue; }
                    if (entry.IndependentStacks != null)
                    {
                        changed |= AdvanceIndependent(entry);
                        if (clearRequested || !Health.IsAlive) { clearRequested = true; break; }
                        if (entry.IndependentStacks.Count == 0) { RemoveAt(i); changed = true; }
                        continue;
                    }
                    var horizon = Math.Min(clock, entry.ExpiresAt);
                    var ticks = 0;
                    while (entry.NextTick <= horizon + TickEpsilon && ticks < Mathf.Clamp(maximumCatchUpTicksPerStatus, 1, 32))
                    {
                        entry.NextTick += entry.Interval;
                        entries[i] = entry;
                        ticks++;
                        var context = new StatusTickContext(this, Health, entry.Source, entry.Definition, entry.Stacks, entry.Origin);
                        ExecuteEffects(entry.Data, in context);
                        if (clearRequested || !Health.IsAlive) break;
                    }
                    if (clearRequested || !Health.IsAlive) { clearRequested = true; break; }
                    // Keep an expired entry while it still has due ticks to drain after a long frame.
                    if (clock >= entry.ExpiresAt && entry.NextTick > entry.ExpiresAt + TickEpsilon)
                    { RemoveAt(i); changed = true; }
                }
            }
            finally { advancing = false; }
            if (clearRequested)
            {
                clearRequested = false;
                Clear();
                return;
            }
            var count = pendingCount;
            pendingCount = 0;
            for (var i = 0; i < count; i++)
            {
                var operation = pending[i];
                pending[i] = default;
                if (operation.Remove) Remove(operation.Definition);
                else ApplyPrepared(operation.Definition, operation.Source, operation.Origin, operation.Data);
            }
            ReconcileScheduler();
            if (changed) Changed?.Invoke();
        }
        private bool AdvanceIndependent(Entry entry)
        {
            var stacks = entry.IndependentStacks;
            var budget = Mathf.Clamp(maximumCatchUpTicksPerStatus, 1, 32);
            for (var ticks = 0; ticks < budget; ticks++)
            {
                var earliest = -1;
                for (var i = 0; i < stacks.Count; i++)
                {
                    var stack = stacks[i];
                    if (stack.NextTick > Math.Min(clock, stack.ExpiresAt) + TickEpsilon) continue;
                    if (earliest < 0 || stack.NextTick < stacks[earliest].NextTick
                        || (stack.NextTick == stacks[earliest].NextTick && stack.Id < stacks[earliest].Id)) earliest = i;
                }
                if (earliest < 0) break;
                var due = stacks[earliest];
                due.NextTick += due.Interval; stacks[earliest] = due;
                var context = new StatusTickContext(this, Health, due.Source, entry.Definition, 1, due.Origin, due.Id);
                ExecuteEffects(due.Data, in context);
                if (clearRequested || !Health.IsAlive) return false;
            }
            var removed = false;
            for (var i = stacks.Count - 1; i >= 0; i--)
            {
                var stack = stacks[i];
                if (clock < stack.ExpiresAt || stack.NextTick <= stack.ExpiresAt + TickEpsilon) continue;
                ReleaseData(stack.Data);
                stacks[i] = stacks[stacks.Count - 1]; stacks.RemoveAt(stacks.Count - 1);
                independentRecordCount--; removed = true;
            }
            if (removed) modifiersDirty = true;
            return removed;
        }
        private void ExecuteEffects(ApplicationData data, in StatusTickContext context)
        {
            if (data == null) return;
            for (var i = 0; i < data.Effects.Count; i++)
            {
                var effect = data.Effects[i];
                var captured = WithCapture(context, data, effect.Value, effect.Formula);
                if (effect.Effect != null) effect.Effect.Execute(in captured);
                if (clearRequested || !Health.IsAlive) break;
            }
        }
        private StatusTickContext WithCapture(StatusTickContext context, ApplicationData data, float value, DamageFormulaSnapshot formula = default) =>
            new StatusTickContext(this, Health, context.Source, context.Definition, context.Stacks, context.Origin, context.StackId,
                value, true, data.Duration, data.Interval, data.PlannedTicks, formula);

        /// <summary>Inspect one periodic effect's next pre-resistance damage per stack without executing it.</summary>
        public bool TryGetStackDamage(StatusDefinition definition, int stackIndex, int effectIndex, out float amount)
        {
            amount = 0;
            var index = Find(definition);
            if (index < 0 || effectIndex < 0 || stackIndex < 0) return false;
            var entry = entries[index];
            ApplicationData data = entry.Data;
            var source = entry.Source; var origin = entry.Origin; long id = 0;
            if (entry.IndependentStacks != null)
            {
                data = null;
                foreach (var stack in entry.IndependentStacks)
                {
                    if (stack.ExpiresAt <= clock) continue;
                    if (stackIndex-- != 0) continue;
                    data = stack.Data; source = stack.Source; origin = stack.Origin; id = stack.Id; break;
                }
            }
            else if (stackIndex != 0) return false;
            if (data == null || effectIndex >= data.Effects.Count || !(data.Effects[effectIndex].Effect is PeriodicDamageEffect effect)) return false;
            var context = WithCapture(new StatusTickContext(this, Health, source, definition, 1, origin, id), data, data.Effects[effectIndex].Value, data.Effects[effectIndex].Formula);
            amount = effect.CalculatePerStack(in context);
            return true;
        }
        private void ReconcileScheduler()
        {
            if (NeedsTick) StatusScheduler.Register(this);
            else StatusScheduler.Unregister(this);
        }

        public float GetStatMultiplier(CombatStatDefinition stat)
        {
            if (stat == null) return 1f;
            RebuildModifiersIfNeeded();
            return statMultipliers.TryGetValue(stat, out var value) ? value : 1f;
        }
        public float GetResistanceModifier(DamageTypeDefinition type)
        {
            if (type == null) return 0f;
            RebuildModifiersIfNeeded();
            return resistanceModifiers.TryGetValue(type, out var value) ? value : 0f;
        }
        private void RebuildModifiersIfNeeded()
        {
            if (!modifiersDirty) return;
            modifiersDirty = false;
            statMultipliers.Clear();
            resistanceModifiers.Clear();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Definition == null || (entry.IndependentStacks == null && clock >= entry.ExpiresAt)) continue;
                var activeStacks = ActiveStacks(entry);
                if (activeStacks == 0) continue;
                var stats = entry.Definition.StatModifiers;
                if (stats != null)
                    for (var j = 0; j < stats.Length; j++)
                    {
                        var modifier = stats[j];
                        if (modifier.Stat == null || !CombatMath.Finite(modifier.MultiplierPerStack) || modifier.MultiplierPerStack < 0f) continue;
                        var previous = statMultipliers.TryGetValue(modifier.Stat, out var existing) ? existing : 1f;
                        var next = previous * Mathf.Pow(modifier.MultiplierPerStack, activeStacks);
                        statMultipliers[modifier.Stat] = CombatMath.Finite(next) ? Mathf.Min(next, 1000f) : 1000f;
                    }
                var resistances = entry.Definition.ResistanceModifiers;
                if (resistances != null)
                    for (var j = 0; j < resistances.Length; j++)
                    {
                        var modifier = resistances[j];
                        if (modifier.Type == null || !CombatMath.Finite(modifier.Reduction)) continue;
                        resistanceModifiers.TryGetValue(modifier.Type, out var previous);
                        resistanceModifiers[modifier.Type] = previous + Mathf.Clamp(modifier.Reduction, -1f, 1f) * activeStacks;
                    }
            }
        }
    }
}
