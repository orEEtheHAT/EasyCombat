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
            public Health DamageTarget;
            public DamageInfo Damage;
            public bool IsDamage;
        }
        private static readonly Pending[] pending = new Pending[Capacity];
        private static int head, count, transactions,reserved;
        private static bool flushing;
        public static long ResolutionId { get; private set; }
        public static long DroppedActions { get; private set; }
        internal static bool IsProcessing=>transactions>0 || flushing || count>0;
        internal static void Enter() { if (transactions == 0 && !flushing) { ResolutionId++; reserved=0; } transactions++; }
        internal static void Exit() { transactions--; if (transactions == 0) Flush(); }
        internal static bool Enqueue(ReactionDefinition recipe, in ReactionContext context)
            =>Enqueue(recipe.Actions,in context);
        internal static bool Enqueue(CombatReactionAction[] actions,in ReactionContext context)
        {
            if(actions==null || actions.Length==0 || actions.Length>8) return false;
            foreach(var action in actions) if(action==null || !action.IsValid) return false;
            if (context.Action.ReactionDepth > MaximumReactionDepth || actions.Length > Capacity-count || actions.Length>MaximumActionsPerFlush-reserved)
            { DroppedActions += actions.Length; return false; }
            foreach (var action in actions)
            {
                pending[(head+count)%Capacity] = new Pending { Action = action, Context = context,
                    OwnerLife = context.Owner != null ? context.Owner.Identity.Id : 0, TargetLife = context.Target != null ? context.Target.Identity.Id : 0 };
                count++;
            }
            reserved+=actions.Length;
            return true;
        }
        internal static bool EnqueueDamage(Health target,DamageInfo damage)
        {
            if(target==null || !damage.IsValid || damage.Origin.Action.Depth>16 || count>=Capacity || reserved>=MaximumActionsPerFlush)
            { DroppedActions++; return false; }
            pending[(head+count)%Capacity]=new Pending { IsDamage=true,DamageTarget=target,Damage=damage,TargetLife=target.Identity.Id };
            count++; reserved++; return true;
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
                    if(item.IsDamage)
                    {
                        if(item.DamageTarget!=null && item.DamageTarget.isActiveAndEnabled && item.DamageTarget.Identity.Id==item.TargetLife)
                            try { item.DamageTarget.ApplyDamage(item.Damage); } catch(Exception exception) { Debug.LogException(exception); }
                        continue;
                    }
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
        private static void Reset() { Array.Clear(pending,0,pending.Length); count = head = transactions = reserved = 0; flushing = false; DroppedActions = 0; }
    }
}
