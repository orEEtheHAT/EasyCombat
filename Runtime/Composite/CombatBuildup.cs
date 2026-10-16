using System;
using UnityEngine;

namespace EasyCombat
{
    public readonly struct BuildupView
    {
        public readonly BuildupDefinition Definition;
        public readonly float Value,Threshold,Cooldown;
        public BuildupView(BuildupDefinition definition,float value,float cooldown)
        { Definition=definition; Value=value; Threshold=definition!=null ? definition.Threshold : 0; Cooldown=cooldown; }
    }
    public readonly struct BuildupChange
    {
        public readonly BuildupView Meter;
        public readonly float Previous;
        public readonly bool Triggered;
        public BuildupChange(BuildupView meter,float previous,bool triggered) { Meter=meter; Previous=previous; Triggered=triggered; }
    }
    [DisallowMultipleComponent,RequireComponent(typeof(Health)),AddComponentMenu("EasyCombat/Composite/Buildup")]
    public sealed class CombatBuildup : MonoBehaviour
    {
        [SerializeField] private BuildupDefinition[] definitions=Array.Empty<BuildupDefinition>();
        private struct State { public BuildupDefinition Definition; public float Value,Delay,Cooldown; }
        private State[] states=Array.Empty<State>();
        private Health owner;
        private bool mutating,configured,resetRequested;
        internal int SchedulerIndex=-1;
        private readonly CombatSignal<BuildupChange> changed=new CombatSignal<BuildupChange>();
        public event Action<BuildupChange> Changed { add=>changed.Add(value); remove=>changed.Remove(value); }
        public int Count=>states.Length;
        public int RejectedThresholds { get; private set; }
        public bool Configure(BuildupDefinition[] profiles)
        {
            if(mutating || profiles==null || profiles.Length>16) return false;
            for(var i=0;i<profiles.Length;i++)
            {
                if(profiles[i]==null || !profiles[i].IsValid) return false;
                for(var j=0;j<i;j++) if(profiles[j]==profiles[i]) return false;
            }
            var next=new State[profiles.Length]; for(var i=0;i<profiles.Length;i++) next[i].Definition=profiles[i];
            states=next; definitions=(BuildupDefinition[])profiles.Clone(); configured=true; Bind(); Reconcile(); return true;
        }
        private void OnEnable() { if(!configured) Configure(definitions); else { Bind(); Reconcile(); } }
        private void Bind()
        {
            owner=GetComponent<Health>(); if(!isActiveAndEnabled) return;
            owner.Resolved-=Resolved; owner.Resolved+=Resolved;
            owner.Changed-=HealthChanged; owner.Changed+=HealthChanged;
            owner.Died-=Died; owner.Died+=Died;
        }
        private void OnDisable()
        {
            if(owner!=null) { owner.Resolved-=Resolved; owner.Changed-=HealthChanged; owner.Died-=Died; }
            BuildupScheduler.Unregister(this);
        }
        private void OnDestroy()=>OnDisable();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var meter in FindObjectsByType<CombatBuildup>(FindObjectsSortMode.None)) if(meter.isActiveAndEnabled) { meter.Bind(); meter.Reconcile(); } }
        private void HealthChanged(HealthChange change) { if(change.Reason==HealthChangeReason.Reset || change.Reason==HealthChangeReason.Restore) ResetMeters(); }
        private void Died(Health _)=>ResetMeters();
        public BuildupView GetMeter(int index)=>new BuildupView(states[index].Definition,states[index].Value,states[index].Cooldown);
        public bool TryGet(BuildupDefinition definition,out BuildupView view)
        { for(var i=0;i<states.Length;i++) if(states[i].Definition==definition) { view=GetMeter(i); return true; } view=default; return false; }
        private void Resolved(CombatRecord record)
        {
            if(mutating || owner==null || !isActiveAndEnabled || record.Target.Id!=owner.Identity.Id) return;
            mutating=true;
            try
            {
                var hit=record.Result;
                for(var i=0;i<states.Length;i++)
                {
                    var d=states[i].Definition; if(d==null || !d.IsValid) continue;
                    if(!owner.IsStanding && !(d.TriggerOnFatalHit && hit.Killed) || !d.AllowPeriodic && hit.Damage.IsPeriodic
                        || !d.AllowReactionDamage && hit.Damage.Origin.Action.ReactionDepth>0
                        || d.RequiredTag!=null && !hit.Damage.Origin.Action.Tags.Contains(d.RequiredTag)) continue;
                    if(!hit.HitApplied && !hit.Killed && !(d.CountBlockedContacts && (hit.Rejection==DamageRejection.Blocked || hit.Rejection==DamageRejection.Immune
                        || hit.Rejection==DamageRejection.None && hit.Damage.Amount>0))) continue;
                    double hp=0,shield=0; var matched=!d.FilterType;
                    if(!d.FilterType) { hp=hit.AppliedAmount; shield=hit.ShieldAbsorbed; }
                    else
                    {
                        for(var p=0;p<hit.Breakdown.Count;p++)
                        {
                            var part=hit.Breakdown[p]; if(part.Type!=d.DamageType) continue;
                            matched|=part.Incoming>0; hp+=part.Applied; shield+=part.ShieldAbsorbed;
                        }
                    }
                    if(matched) Accumulate(i,d.FlatPerHit+hp*d.PerHealthDamage+shield*d.PerShieldDamage,in record);
                }
            }
            finally { CompleteMutation(); }
        }
        public bool Add(BuildupDefinition definition,float amount,GameObject source=null)
        {
            if(mutating || !isActiveAndEnabled || definition==null || !definition.IsValid || !CombatMath.Positive(amount) || owner==null || !owner.IsStanding) return false;
            for(var i=0;i<states.Length;i++) if(states[i].Definition==definition)
            {
                var damage=new DamageInfo(amount,source);
                var record=new CombatRecord(0,CombatClock.Now,owner.Identity,new DamageResult(damage,0,false),owner);
                CombatReactionQueue.Enter(); mutating=true;
                try { Accumulate(i,amount,in record); return true; }
                finally { try { CompleteMutation(); } finally { CombatReactionQueue.Exit(); } }
            }
            return false;
        }
        private void Accumulate(int index,double amount,in CombatRecord trigger)
        {
            if(amount<=0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;
            var state=states[index]; var d=state.Definition; var before=state.Value;
            var total=state.Value+amount; var triggered=false;
            state.Value=(float)Math.Min(d.Threshold,total); state.Delay=d.DecayDelay;
            if(total>=d.Threshold && state.Cooldown<=0)
            {
                var context=new ReactionContext(owner,owner,trigger,trigger.Result.Damage.Origin.Action.Child(CombatOriginKind.Reaction),true);
                if(CombatReactionQueue.Enqueue(d.Actions,in context))
                { state.Value=d.KeepOverflow ? (float)Math.Min(d.Threshold,total-d.Threshold) : 0; state.Cooldown=d.TriggerCooldown; triggered=true; }
                else RejectedThresholds++;
            }
            states[index]=state; changed.Publish(new BuildupChange(GetMeter(index),before,triggered));
        }
        public void Advance(float deltaTime)
        {
            if(mutating || !isActiveAndEnabled || !CombatMath.Positive(deltaTime)) return;
            mutating=true;
            try
            {
                for(var i=0;i<states.Length;i++)
                {
                    var state=states[i]; var d=state.Definition; if(d==null || !d.IsValid) continue;
                    var before=state.Value; var active=Mathf.Max(0,deltaTime-state.Delay);
                    state.Delay=Mathf.Max(0,state.Delay-deltaTime); state.Cooldown=Mathf.Max(0,state.Cooldown-deltaTime);
                    state.Value=(float)Math.Max(0,state.Value-(double)d.DecayPerSecond*active);
                    states[i]=state; if(before!=state.Value) changed.Publish(new BuildupChange(GetMeter(i),before,false));
                }
            }
            finally { CompleteMutation(); }
        }
        public void ResetMeters()
        {
            if(mutating) { resetRequested=true; return; } mutating=true;
            try
            {
                for(var i=0;i<states.Length;i++) { var before=states[i].Value; states[i].Value=states[i].Delay=states[i].Cooldown=0; if(before>0) changed.Publish(new BuildupChange(GetMeter(i),before,false)); }
                RejectedThresholds=0;
            }
            finally { CompleteMutation(); }
        }
        internal bool NeedsTick
        {
            get
            {
                if(this==null || !isActiveAndEnabled) return false;
                foreach(var state in states) if(state.Definition!=null && state.Definition.IsValid && (state.Cooldown>0 || state.Value>0 && state.Definition.DecayPerSecond>0)) return true;
                return false;
            }
        }
        private void CompleteMutation()
        {
            mutating=false; if(this==null) return;
            if(resetRequested) { resetRequested=false; ResetMeters(); }
            Reconcile();
        }
        private void Reconcile() { if(this==null) return; if(NeedsTick) BuildupScheduler.Register(this); else BuildupScheduler.Unregister(this); }
    }
}
