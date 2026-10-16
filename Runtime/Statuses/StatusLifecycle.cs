using System;

namespace EasyCombat
{
    public enum StatusChangeKind { Applied, StackAdded, Refreshed, StackReplaced, Tick, Removed }
    public enum StatusRemovalReason { Removed, Expired, Dispelled, Consumed, Death, Reset, Restore, Disabled, Cleared }
    public readonly struct StatusChange
    {
        public readonly StatusController Controller;
        public readonly StatusDefinition Definition;
        public readonly StatusChangeKind Kind;
        public readonly StatusRemovalReason RemovalReason;
        public readonly int StacksBefore, StacksAfter;
        public readonly long StackId;
        public readonly DamageOrigin Origin;
        public StatusChange(StatusController controller,StatusDefinition definition,StatusChangeKind kind,int before,int after,
            DamageOrigin origin = default,long stackId = 0,StatusRemovalReason reason = StatusRemovalReason.Removed)
        { Controller = controller; Definition = definition; Kind = kind; StacksBefore = before; StacksAfter = after; Origin = origin; StackId = stackId; RemovalReason = reason; }
    }
    public sealed partial class StatusController
    {
        private readonly CombatSignal<StatusChange> lifecycle = new CombatSignal<StatusChange>();
        private readonly CombatSignal changedSignal = new CombatSignal();
        public event Action<StatusChange> Lifecycle { add => lifecycle.Add(value); remove => lifecycle.Remove(value); }
        private StatusRemovalReason clearReason = StatusRemovalReason.Cleared;
        private CombatRestriction restrictions;
        public CombatRestriction Restrictions { get { RebuildModifiersIfNeeded(); return restrictions; } }
        public int OverdueTickRecords
        {
            get
            {
                var count=0;
                foreach(var entry in entries)
                {
                    if(entry.IndependentStacks==null) { if(entry.NextTick<=Math.Min(clock,entry.ExpiresAt)+TickEpsilon) count++; }
                    else foreach(var stack in entry.IndependentStacks) if(stack.NextTick<=Math.Min(clock,stack.ExpiresAt)+TickEpsilon) count++;
                }
                return count;
            }
        }
        /// <summary>Remove matching groups, with an explicit budget. Null matches all statuses.</summary>
        public int Dispel(CombatTagDefinition tag = null,int maximumGroups = 32)
        {
            if (maximumGroups <= 0) return 0;
            var removed = 0;
            if (advancing)
            {
                for (var i = entries.Count-1; i >= 0 && removed < maximumGroups; i--)
                    if (tag == null || entries[i].Definition != null && entries[i].Definition.HasTag(tag))
                        if (Remove(entries[i].Definition,StatusRemovalReason.Dispelled)) removed++;
                return removed;
            }
            while (removed < maximumGroups)
            {
                StatusDefinition found = null;
                foreach (var entry in entries)
                    if (entry.Definition != null && (tag == null || entry.Definition.HasTag(tag))) { found = entry.Definition; break; }
                if (found == null || !Remove(found,StatusRemovalReason.Dispelled)) break;
                removed++;
            }
            return removed;
        }
        /// <summary>Consume exactly the requested number of live stacks, or change nothing. Pending/expired stacks do not pay costs.</summary>
        public bool TryConsume(StatusDefinition definition,int count)
        {
            if (advancing || count <= 0 || GetStacks(definition) < count) return false;
            var index = Find(definition); var entry = entries[index]; var before = ActiveStacks(entry);
            if (entry.IndependentStacks == null)
            {
                if (entry.ExpiresAt <= clock) return false;
                if (entry.Stacks == count) { Remove(definition,StatusRemovalReason.Consumed); return true; }
                entry.Stacks -= count; entries[index] = entry;
            }
            else
            {
                for (var n = 0; n < count; n++)
                {
                    var earliest = -1;
                    for (var i = 0; i < entry.IndependentStacks.Count; i++)
                    {
                        var stack = entry.IndependentStacks[i]; if (stack.ExpiresAt <= clock) continue;
                        if (earliest < 0 || stack.ExpiresAt < entry.IndependentStacks[earliest].ExpiresAt) earliest = i;
                    }
                    var removed = entry.IndependentStacks[earliest]; ReleaseData(removed.Data);
                    entry.IndependentStacks.RemoveAt(earliest); independentRecordCount--;
                }
                if (entry.IndependentStacks.Count == 0) RemoveAt(index,StatusRemovalReason.Consumed);
            }
            modifiersDirty = true; ReconcileScheduler();
            lifecycle.Publish(new StatusChange(this,definition,StatusChangeKind.Removed,before,before-count,reason:StatusRemovalReason.Consumed));
            changedSignal.Publish(); return true;
        }
    }
}
