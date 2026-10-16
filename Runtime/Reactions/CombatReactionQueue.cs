using System;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>One bounded queue on the main thread. Reactions execute only after the outer damage transaction finishes.</summary>
    public static class CombatReactionQueue
    {
        public const int Capacity = 256, MaximumActionsPerFlush = 64, MaximumReactionDepth = 8;
        private struct Pending
        {
            public CombatReactionAction Action;
            public ReactionContext Context;
            public long OwnerLife, TargetLife;
        }
        private static readonly Pending[] pending = new Pending[Capacity];
        private static int head, count, transactions;
        private static bool flushing;
        public static long ResolutionId { get; private set; }
        public static long DroppedActions { get; private set; }
        internal static void Enter() { if (transactions == 0 && !flushing) ResolutionId++; transactions++; }
        internal static void Exit() { transactions--; if (transactions == 0) Flush(); }
        internal static bool Enqueue(ReactionDefinition recipe, in ReactionContext context)
        {
            if (context.Action.ReactionDepth > MaximumReactionDepth || recipe.Actions.Length > Capacity-count)
            { DroppedActions += recipe.Actions.Length; return false; }
            foreach (var action in recipe.Actions)
            {
                pending[(head+count)%Capacity] = new Pending { Action = action, Context = context,
                    OwnerLife = context.Owner != null ? context.Owner.Identity.Id : 0, TargetLife = context.Target != null ? context.Target.Identity.Id : 0 };
                count++;
            }
            return true;
        }
        public static void Flush()
        {
            if (flushing || transactions > 0) return;
            flushing = true;
            try
            {
                var executed = 0;
                while (count > 0 && executed++ < MaximumActionsPerFlush)
                {
                    var item = pending[head]; pending[head] = default; head = (head+1)%Capacity; count--;
                    var context = item.Context;
                    var deathReaction = context.Trigger.Result.Killed && context.Trigger.Target.Id == item.OwnerLife;
                    if (item.Action == null || context.Owner == null || !context.Owner.isActiveAndEnabled && !deathReaction || context.Owner.Identity.Id != item.OwnerLife
                        || context.Target == null || !context.Target.isActiveAndEnabled && !(deathReaction && ReferenceEquals(context.Target,context.Owner))
                        || context.Target.Identity.Id != item.TargetLife) continue;
                    try { item.Action.Execute(in context); } catch (Exception exception) { Debug.LogException(exception); }
                }
                if (count > 0) { DroppedActions += count; Array.Clear(pending,0,pending.Length); count = head = 0; }
            }
            finally { flushing = false; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Array.Clear(pending,0,pending.Length); count = head = transactions = 0; flushing = false; DroppedActions = 0; }
    }
}
