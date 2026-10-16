using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/Delivery/Melee")]
    public sealed class CombatMelee:MonoBehaviour,ICombatDeliveryTick
    {
        [SerializeField] private Transform ownerRoot;
        [SerializeField,Tooltip("Up to eight sample points along a weapon. Empty uses this transform.")] private Transform[] anchors=Array.Empty<Transform>();
        [SerializeField] private bool automaticAdvance=true;
        private readonly Vector3[] previous=new Vector3[8],current=new Vector3[8];
        private readonly Transform[] activeAnchors=new Transform[8];
        private readonly MeleeWindow[] windows=new MeleeWindow[8];
        private int anchorCount,windowCount,targetMemory;
        private struct Memory { public int Window; public double NextTime; }
        private readonly Dictionary<CombatHitKey,Memory> hits=new Dictionary<CombatHitKey,Memory>(32);
        private readonly DamageQuery query=new DamageQuery();
        private readonly CombatSignal<DamageImpact> impacts=new CombatSignal<DamageImpact>();
        private readonly CombatSignal ended=new CombatSignal();
        private DamageInfo damage;
        private Transform source;
        private Health sourceHealth;
        private long sourceLife;
        private double elapsed;
        private float duration,radius,repeatInterval;
        private LayerMask mask;
        private QueryTriggerInteraction triggers;
        private MeleeRepeatPolicy repeat;
        private bool stopAtWorld,interruptWhenBlocked,advancing;
        private int tickIndex=-1;
        int ICombatDeliveryTick.TickIndex { get=>tickIndex; set=>tickIndex=value; }
        bool ICombatDeliveryTick.WantsAutomaticTick=>this!=null && isActiveAndEnabled && automaticAdvance && IsRunning;
        public bool IsRunning { get; private set; }
        public int RejectedByMemory { get; private set; }
        public float Remaining=>(float)Math.Max(0,duration-elapsed);
        public Transform OwnerRoot { get=>ownerRoot!=null ? ownerRoot : transform; set=>ownerRoot=value; }
        public bool AutomaticAdvance { get=>automaticAdvance; set { automaticAdvance=value; Reconcile(); } }
        public event Action<DamageImpact> Impacted { add=>impacts.Add(value); remove=>impacts.Remove(value); }
        public event Action Ended { add=>ended.Add(value); remove=>ended.Remove(value); }
        public bool SetAnchors(params Transform[] points)
        {
            if(IsRunning || advancing || points==null || points.Length>8) return false;
            foreach(var point in points) if(point==null) return false;
            anchors=(Transform[])points.Clone(); return true;
        }
        public bool CanBegin(MeleeDefinition definition)
        {
            if(this==null || !isActiveAndEnabled || IsRunning || advancing || definition==null || !definition.IsValid || anchors==null || anchors.Length>8
                || !CombatControl.IsAllowed(OwnerRoot.gameObject,CombatRestriction.Attack)) return false;
            foreach(var anchor in anchors) if(anchor==null || !CombatMath.Finite(anchor.position)) return false;
            var statuses=OwnerRoot.GetComponent<StatusController>(); var multiplier=statuses!=null ? statuses.GetStatMultiplier(definition.Attack.DamageModifierStat) : 1;
            return CombatMath.Positive(multiplier) && definition.Attack.RollDamage(OwnerRoot.gameObject,transform.position,transform.forward,0,1,multiplier).IsValid;
        }
        public bool Begin(MeleeDefinition definition,DamageOrigin origin=default)
        {
            if(!CanBegin(definition) || anchors==null || anchors.Length>8) return false;
            source=OwnerRoot; sourceHealth=source.GetComponentInParent<Health>(); sourceLife=sourceHealth!=null ? sourceHealth.Identity.Id : 0;
            var statuses=source.GetComponent<StatusController>(); var multiplier=statuses!=null ? statuses.GetStatMultiplier(definition.Attack.DamageModifierStat) : 1;
            damage=definition.Attack.RollRandom(source.gameObject,transform.position,transform.forward,multiplier,origin);
            if(!damage.IsValid) return false;
            anchorCount=Mathf.Max(1,anchors.Length);
            for(var i=0;i<anchorCount;i++)
            { activeAnchors[i]=anchors.Length==0 ? transform : anchors[i]; if(activeAnchors[i]==null || !CombatMath.Finite(activeAnchors[i].position)) return false; previous[i]=activeAnchors[i].position; }
            windowCount=definition.Windows.Length; Array.Copy(definition.Windows,windows,windowCount);
            duration=definition.Duration; radius=definition.Radius; repeatInterval=definition.RepeatInterval; repeat=definition.Repeat;
            mask=definition.HitMask; triggers=definition.Triggers; targetMemory=definition.TargetMemory; stopAtWorld=definition.StopSweepAtWorld; interruptWhenBlocked=definition.InterruptWhenAttackBlocked;
            hits.Clear(); elapsed=0; RejectedByMemory=0; IsRunning=true; Reconcile(); return true;
        }
        public void Advance(float deltaTime)
        {
            if(!IsRunning || advancing || !CombatMath.Positive(deltaTime)) return;
            if(source==null || sourceLife!=0 && (sourceHealth==null || sourceHealth.Identity.Id!=sourceLife)
                || interruptWhenBlocked && !CombatControl.IsAllowed(source.gameObject,CombatRestriction.Attack)) { Stop(); return; }
            for(var i=0;i<anchorCount;i++)
            { if(activeAnchors[i]==null || !CombatMath.Finite(activeAnchors[i].position)) { Stop(); return; } current[i]=activeAnchors[i].position; }
            advancing=true; CombatReactionQueue.Enter();
            try
            {
                var end=Math.Min(duration,elapsed+deltaTime);
                for(var w=0;w<windowCount && IsRunning;w++)
                {
                    var from=Math.Max(elapsed,windows[w].Start); var to=Math.Min(end,windows[w].End);
                    if(from>to || elapsed>=windows[w].End || end<windows[w].Start) continue;
                    var a=(float)((from-elapsed)/deltaTime); var b=(float)((to-elapsed)/deltaTime);
                    for(var sample=0;sample<anchorCount && IsRunning;sample++)
                        Sweep(Vector3.LerpUnclamped(previous[sample],current[sample],a),Vector3.LerpUnclamped(previous[sample],current[sample],b),w,to);
                }
                elapsed=end; for(var i=0;i<anchorCount;i++) previous[i]=current[i];
                if(elapsed>=duration) Stop();
            }
            catch { Stop(); throw; }
            finally { advancing=false; query.ClearContacts(); if(!IsRunning) Finish(); CombatReactionQueue.Exit(); }
        }
        private void Sweep(Vector3 from,Vector3 to,int window,double time)
        {
            var delta=to-from; var direction=delta.sqrMagnitude>.000001f ? delta.normalized : transform.forward;
            var count=query.Collect(from,direction,delta.magnitude,radius,mask,triggers,source);
            try
            {
                for(var i=0;i<count && IsRunning;i++)
                {
                    if(source==null || sourceLife!=0 && (sourceHealth==null || sourceHealth.Identity.Id!=sourceLife)) { Stop(); break; }
                    var contact=query.GetContact(i);
                    if(contact.Collider==null || !contact.Collider.enabled || !contact.Collider.gameObject.activeInHierarchy || !contact.IsCurrent) continue;
                    if(!contact.WasTarget) { if(stopAtWorld) break; continue; }
                    if(hits.TryGetValue(contact.HitKey,out var memory))
                    {
                        if(repeat==MeleeRepeatPolicy.OncePerSwing || repeat==MeleeRepeatPolicy.OncePerWindow && memory.Window==window
                            || repeat==MeleeRepeatPolicy.Interval && time+1e-7<memory.NextTime) continue;
                    }
                    else if(hits.Count>=targetMemory) { RejectedByMemory++; continue; }
                    hits[contact.HitKey]=new Memory { Window=window,NextTime=time+repeatInterval };
                    var impact=contact.Apply(damage.At(contact.Point,direction)); impacts.Publish(impact);
                }
            }
            finally { query.ClearContacts(); }
        }
        public void Stop() { if(!IsRunning) return; IsRunning=false; if(!advancing) Finish(); }
        private void Finish()
        { hits.Clear(); damage=default; source=null; sourceHealth=null; Array.Clear(activeAnchors,0,activeAnchors.Length); DeliveryScheduler.Unregister(this); ended.Publish(); }
        private void Reconcile() { if(((ICombatDeliveryTick)this).WantsAutomaticTick) DeliveryScheduler.Register(this); else DeliveryScheduler.Unregister(this); }
        private void OnDisable()=>Stop();
    }
}
