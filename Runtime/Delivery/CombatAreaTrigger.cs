using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    public enum AreaContactKind { Enter, Stay, Exit }
    public readonly struct AreaContactEvent
    {
        public readonly AreaContactKind Kind;
        public readonly MonoBehaviour Receiver;
        public readonly CombatActorIdentity Actor;
        public readonly Vector3 Point;
        public AreaContactEvent(AreaContactKind kind,MonoBehaviour receiver,CombatActorIdentity actor,Vector3 point)
        { Kind=kind; Receiver=receiver; Actor=actor; Point=point; }
    }
    /// <summary>Sampled occupancy sensor. Uses the same receiver grouping and area filters as ordinary AOE.</summary>
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/Delivery/Area Enter Stay Exit")]
    public sealed class CombatAreaTrigger:MonoBehaviour,ICombatDeliveryTick
    {
        [SerializeField] private bool automaticAdvance=true;
        private struct Member
        {
            public CombatHitKey Key;
            public AreaCandidate Candidate;
            public CombatActorIdentity Identity;
            public long Seen;
            public double NextStay;
            public bool Entered;
        }
        private readonly List<Member> members=new List<Member>(32);
        private readonly List<Member> leaving=new List<Member>(32);
        private readonly Dictionary<CombatHitKey,int> indices=new Dictionary<CombatHitKey,int>(32);
        private readonly AreaDamageQuery query=new AreaDamageQuery();
        private readonly CombatSignal<AreaContactEvent> contacts=new CombatSignal<AreaContactEvent>();
        private readonly CombatSignal<DamageImpact> impacts=new CombatSignal<DamageImpact>();
        private Action<DamageImpact> callback;
        private AreaSettings settings;
        private DamageInfo selection,enter,stay,exit;
        private Transform source;
        private Health sourceHealth;
        private long sourceLife,revision;
        private double elapsed;
        private float duration,interval;
        private int maximumOccupants;
        private bool immediate,emitExitOnStop,stopWithSource,advancing,finishing;
        private int tickIndex=-1;
        int ICombatDeliveryTick.TickIndex { get=>tickIndex; set=>tickIndex=value; }
        bool ICombatDeliveryTick.WantsAutomaticTick=>this!=null && isActiveAndEnabled && automaticAdvance && IsRunning;
        public bool IsRunning { get; private set; }
        public int OccupantCount=>members.Count;
        public int RejectedOccupants { get; private set; }
        public bool AutomaticAdvance { get=>automaticAdvance; set { automaticAdvance=value; Reconcile(); } }
        public event Action<AreaContactEvent> Contact { add=>contacts.Add(value); remove=>contacts.Remove(value); }
        public event Action<DamageImpact> Impacted { add=>impacts.Add(value); remove=>impacts.Remove(value); }
        public bool Begin(AreaTriggerDefinition definition,GameObject instigator=null,DamageOrigin origin=default)
        {
            if(!isActiveAndEnabled || IsRunning || advancing || finishing || definition==null || !definition.IsValid) return false;
            var parent=origin.CastId!=0 ? origin : DamageOrigin.Capture(instigator);
            if(!Prepare(definition.EnterAttack,instigator,parent,out var enterPayload) || !Prepare(definition.StayAttack,instigator,parent,out var stayPayload)
                || !Prepare(definition.ExitAttack,instigator,parent,out var exitPayload)) return false;
            source=instigator!=null ? instigator.transform : null; sourceHealth=instigator!=null ? instigator.GetComponentInParent<Health>() : null; sourceLife=sourceHealth!=null ? sourceHealth.Identity.Id : 0;
            enter=enterPayload; stay=stayPayload; exit=exitPayload; selection=new DamageInfo(1,instigator,origin:parent);
            settings=new AreaSettings(definition.Area); duration=definition.Duration; interval=definition.StayInterval; immediate=definition.StayImmediately;
            emitExitOnStop=definition.EmitExitOnStop; stopWithSource=definition.StopWithSource; maximumOccupants=definition.MaximumOccupants;
            members.Clear(); indices.Clear(); elapsed=0; revision=0; RejectedOccupants=0; IsRunning=true; callback??=impacts.Publish;
            Reconcile(); Advance(0); return true;
        }
        private static bool Prepare(AttackDefinition attack,GameObject source,DamageOrigin parent,out DamageInfo payload)
        {
            payload=default; if(attack==null) return true;
            var statuses=source!=null ? source.GetComponent<StatusController>() : null; var multiplier=statuses!=null ? statuses.GetStatMultiplier(attack.DamageModifierStat) : 1;
            if(!CombatMath.Positive(multiplier)) return false;
            var origin=new DamageOrigin(parent.Actor,attack,parent.CastId,hasTeam:parent.HasTeam,team:parent.Team,action:parent.Action.Child(CombatOriginKind.Area));
            payload=attack.RollRandom(source,default,Vector3.forward,multiplier,origin); return payload.IsValid;
        }
        public void Advance(float deltaTime)
        {
            if(!IsRunning || advancing || !CombatMath.Finite(deltaTime) || deltaTime<0) return;
            if(stopWithSource && (source==null || sourceLife!=0 && (sourceHealth==null || sourceHealth.Identity.Id!=sourceLife || !sourceHealth.IsAlive))) { Stop(); return; }
            advancing=true; CombatReactionQueue.Enter();
            try
            {
                elapsed+=deltaTime; var time=duration>0 ? Math.Min(duration,elapsed) : elapsed; revision++;
                var count=query.Select(transform.position,transform.forward,selection,settings,source);
                try
                {
                    // Establish occupancy before callbacks, freeing departed slots before admitting newcomers.
                    for(var i=0;i<count;i++)
                    {
                        var candidate=query.GetCandidate(i); var key=new CombatHitKey(candidate.Receiver);
                        if(indices.TryGetValue(key,out var index))
                        { var member=members[index]; member.Candidate=candidate; member.Seen=revision; members[index]=member; }
                    }
                    leaving.Clear();
                    for(var i=members.Count-1;i>=0;i--) if(members[i].Seen!=revision) { leaving.Add(members[i]); Remove(i); }
                    for(var i=0;i<count;i++)
                    {
                        var candidate=query.GetCandidate(i); var key=new CombatHitKey(candidate.Receiver);
                        if(!indices.ContainsKey(key))
                        {
                            if(members.Count>=maximumOccupants) { RejectedOccupants++; continue; }
                            var health=CombatTargetGroups.Rules(candidate.Receiver);
                            indices.Add(key,members.Count); members.Add(new Member { Key=key,Candidate=candidate,Identity=health!=null ? health.Identity : CombatActorIdentity.Capture(candidate.Receiver.gameObject),Seen=revision,NextStay=time+(immediate ? 0 : interval) });
                        }
                    }
                }
                finally { query.ClearSelection(); }
                for(var i=0;i<leaving.Count && IsRunning;i++) { var member=leaving[i]; Dispatch(in member,AreaContactKind.Exit,exit); }
                leaving.Clear();
                var backlog=false;
                for(var i=0;i<members.Count && IsRunning;i++)
                {
                    var member=members[i];
                    if(!member.Entered) { member.Entered=true; members[i]=member; Dispatch(in member,AreaContactKind.Enter,enter); }
                    var pulses=0;
                    while(IsRunning && member.NextStay<=time+1e-7 && pulses++<8)
                    { member.NextStay+=interval; members[i]=member; Dispatch(in member,AreaContactKind.Stay,stay); }
                    backlog|=member.NextStay<=time+1e-7;
                }
                if(duration>0 && elapsed>=duration && !backlog) Stop();
            }
            catch { Stop(); throw; }
            finally { advancing=false; if(!IsRunning) Finish(); CombatReactionQueue.Exit(); }
        }
        private void Dispatch(in Member member,AreaContactKind kind,DamageInfo payload)
        {
            var candidate=member.Candidate;
            var current=candidate.Receiver!=null && member.Key.Equals(new CombatHitKey(candidate.Receiver))
                && (candidate.LifeId==0 || CombatTargetGroups.Life(candidate.Receiver)==candidate.LifeId);
            if(current && payload.IsValid)
            {
                if(kind==AreaContactKind.Exit)
                {
                    // Exit is a direct packet using the last sampled zone; no range/falloff is re-applied.
                    if(candidate.Receiver.isActiveAndEnabled)
                    {
                        var point=candidate.Receiver.transform.position; var request=payload.At(point,(point-transform.position).normalized);
                        var result=settings.UseHurtboxMultiplier && candidate.Zone!=null ? candidate.Zone.ApplyDamage(request) : ((IDamageable)candidate.Receiver).ApplyDamage(request);
                        impacts.Publish(new DamageImpact(candidate.Collider,point,-request.Direction,result));
                    }
                }
                else AreaDamageQuery.ApplyCandidate(candidate,transform.position,transform.forward,payload,settings,callback);
            }
            contacts.Publish(new AreaContactEvent(kind,current ? candidate.Receiver : null,member.Identity,candidate.Point));
        }
        private void Remove(int index)
        {
            indices.Remove(members[index].Key); var last=members[members.Count-1];
            if(index<members.Count-1) { members[index]=last; indices[last.Key]=index; } members.RemoveAt(members.Count-1);
        }
        public void Stop() { if(!IsRunning) return; IsRunning=false; if(!advancing) Finish(); }
        private void Finish()
        {
            if(finishing) return; finishing=true; CombatReactionQueue.Enter();
            try
            {
                if(emitExitOnStop) for(var i=members.Count-1;i>=0;i--) { var member=members[i]; if(member.Entered) Dispatch(in member,AreaContactKind.Exit,exit); }
            }
            finally
            {
                members.Clear(); leaving.Clear(); indices.Clear(); selection=enter=stay=exit=default; source=null; sourceHealth=null;
                DeliveryScheduler.Unregister(this); finishing=false; CombatReactionQueue.Exit();
            }
        }
        private void Reconcile() { if(((ICombatDeliveryTick)this).WantsAutomaticTick) DeliveryScheduler.Register(this); else DeliveryScheduler.Unregister(this); }
        private void OnDisable()=>Stop();
    }
}
