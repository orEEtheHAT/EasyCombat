using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health)), AddComponentMenu("EasyCombat/Combat Reactions")]
    public sealed class CombatReactions : MonoBehaviour
    {
        [SerializeField] private ReactionDefinition[] reactions = Array.Empty<ReactionDefinition>();
        private struct Cooldown { public long Key; public double Until; }
        private sealed class State
        {
            public ReactionDefinition Definition;
            public long Resolution;
            public int Procs;
            public double NextActorTime = double.NegativeInfinity;
            public readonly List<Cooldown> Cooldowns = new List<Cooldown>(4);
        }
        private State[] states = Array.Empty<State>();
        private Health owner;
        private bool configured;
        public int RejectedByBudget { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        {
            foreach (var actor in FindObjectsByType<CombatReactions>(FindObjectsSortMode.None))
                if (actor.isActiveAndEnabled) actor.Configure(actor.reactions);
        }
        private void OnEnable()
        {
            if (!configured) Configure(reactions);
            else Bind();
        }
        private void Bind()
        {
            owner = GetComponent<Health>();
            if (!isActiveAndEnabled) return;
            CombatEvents.DamageResolved -= Resolved;
            CombatEvents.DamageResolved += Resolved;
            owner.Changed -= OwnerChanged;
            owner.Changed += OwnerChanged;
        }
        private void OnDisable() { CombatEvents.DamageResolved -= Resolved; if (owner != null) owner.Changed -= OwnerChanged; }
        private void OnDestroy() => OnDisable();
        private void OwnerChanged(HealthChange change)
        { if (change.Reason == HealthChangeReason.Reset || change.Reason == HealthChangeReason.Restore) ResetCooldowns(); }
        public bool Configure(ReactionDefinition[] definitions)
        {
            if (definitions == null || definitions.Length > 32) return false;
            foreach (var definition in definitions) if (definition == null || !definition.IsValid) return false;
            var next = new State[definitions.Length];
            for (var i = 0; i < next.Length; i++) next[i] = new State { Definition = definitions[i] };
            states = next; reactions = (ReactionDefinition[])definitions.Clone(); configured = true; Bind(); return true;
        }
        public void ResetCooldowns()
        {
            foreach (var state in states) { state.NextActorTime = double.NegativeInfinity; state.Cooldowns.Clear(); state.Procs = 0; state.Resolution = 0; }
            RejectedByBudget = 0;
        }
        private void Resolved(CombatRecord record)
        {
            if (owner == null || !owner.isActiveAndEnabled || !isActiveAndEnabled) return;
            var result = record.Result;
            var dealt = result.Damage.Origin.Actor.Id == owner.Identity.Id;
            var taken = record.Target.Id == owner.Identity.Id;
            if (!dealt && !taken) return;
            foreach (var state in states)
            {
                var rule = state.Definition;
                if (rule == null || !rule.IsValid || rule.Chance <= 0) continue;
                var matches = rule.Trigger == ReactionTrigger.DamageTaken ? taken && result.AppliedAmount > 0
                    : rule.Trigger == ReactionTrigger.DamageDealt ? dealt && result.HitApplied
                    : rule.Trigger == ReactionTrigger.Kill ? dealt && result.Killed
                    : rule.Trigger == ReactionTrigger.Death ? taken && result.Killed
                    : taken && result.ShieldAbsorbed > 0;
                if (!matches || !rule.AllowPeriodic && result.Damage.IsPeriodic || !rule.AllowReactionChains && result.Damage.Origin.Action.ReactionDepth > 0
                    || rule.RequireCritical && !result.Damage.IsCritical || result.AppliedAmount < rule.MinimumHealthDamage
                    || owner.Normalized > rule.OwnerHealthBelow
                    || !result.Damage.Origin.Action.Tags.ContainsAll(rule.RequiredTags) || result.Damage.Origin.Action.Tags.ContainsAny(rule.ExcludedTags)) continue;
                if (rule.DamageType != null)
                {
                    var found = false;
                    for (var i = 0; i < result.Damage.Parts.Count; i++) if (result.Damage.Parts[i].Type == rule.DamageType && result.Damage.Parts[i].Amount > 0) { found = true; break; }
                    if (!found) continue;
                }
                var target = rule.Target == ReactionTarget.Owner ? owner : rule.Target == ReactionTarget.Victim ? record.TargetHealth
                    : result.Damage.Source != null ? result.Damage.Source.GetComponentInParent<Health>() : null;
                if (target == null) continue;
                if (state.Resolution != CombatReactionQueue.ResolutionId) { state.Resolution = CombatReactionQueue.ResolutionId; state.Procs = 0; }
                if (state.Procs >= rule.MaximumProcsPerResolution) continue;
                var now = CombatClock.Now;
                // Per-target cooldown follows the other combat participant, independently of action recipient (e.g. lifesteal heals self).
                var key = rule.CooldownScope == ReactionCooldownScope.Target ? dealt ? record.Target.Id : result.Damage.Origin.Actor.Id : result.Damage.Origin.Action.RootId;
                if (rule.CooldownScope == ReactionCooldownScope.Actor)
                { if (now < state.NextActorTime) continue; }
                else if (rule.Cooldown > 0)
                {
                    var blocked = false;
                    for (var i = state.Cooldowns.Count-1; i >= 0; i--)
                    {
                        var cooldown = state.Cooldowns[i];
                        if (cooldown.Until <= now) { state.Cooldowns.RemoveAt(i); continue; }
                        if (cooldown.Key == key) blocked = true;
                    }
                    if (blocked) continue;
                    if (state.Cooldowns.Count >= 128) { RejectedByBudget++; continue; }
                }
                if (rule.Chance < 1 && CombatRandom.Value >= rule.Chance) continue;
                var action = result.Damage.Origin.Action.Child(CombatOriginKind.Reaction);
                var context = new ReactionContext(owner,target,record,action);
                if (!CombatReactionQueue.Enqueue(rule,in context)) { RejectedByBudget++; continue; }
                state.Procs++;
                if (rule.CooldownScope == ReactionCooldownScope.Actor) state.NextActorTime = now+rule.Cooldown;
                else if (rule.Cooldown > 0) state.Cooldowns.Add(new Cooldown { Key = key, Until = now+rule.Cooldown });
            }
        }
    }
}
