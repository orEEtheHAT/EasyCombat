using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/Delivery/Beam")]
    public sealed class CombatBeam:MonoBehaviour,ICombatDeliveryTick,ICombatTransient
    {
        [SerializeField] private Transform ownerRoot,muzzle;
        [SerializeField] private DamageAreaPool areaPool;
        [SerializeField] private bool automaticAdvance=true;
        private BeamDefinition definition;
        private DamageInfo payload;
        private DamageOrigin origin;
        private Health sourceHealth;
        private Transform source;
        private CombatResources resources;
        private long sourceLife;
        private double elapsed,nextPulse;
        private float duration,interval,multiplier;
        private bool reroll,perSecond,interrupt,advancing,finishing;
        private readonly AttackTrace trace=new AttackTrace();
        private readonly CombatSignal<DamageImpact> impacts=new CombatSignal<DamageImpact>();
        private readonly CombatSignal ended=new CombatSignal();
        private readonly CombatSignal<ShotInfo> pulses=new CombatSignal<ShotInfo>();
        private Action<DamageImpact> impactCallback;
        private int tickIndex=-1;
        int ICombatDeliveryTick.TickIndex { get=>tickIndex; set=>tickIndex=value; }
        bool ICombatDeliveryTick.WantsAutomaticTick=>this!=null && isActiveAndEnabled && automaticAdvance && IsRunning;
        public bool IsRunning { get; private set; }
        public Transform OwnerRoot { get=>ownerRoot!=null ? ownerRoot : transform; set=>ownerRoot=value; }
        public Transform Muzzle { get=>muzzle!=null ? muzzle : transform; set=>muzzle=value; }
        public DamageAreaPool AreaPool { get=>areaPool; set=>areaPool=value; }
        public bool AutomaticAdvance { get=>automaticAdvance; set { automaticAdvance=value; Reconcile(); } }
        public float Remaining=>(float)Math.Max(0,duration-elapsed);
        public event Action<DamageImpact> Impacted { add=>impacts.Add(value); remove=>impacts.Remove(value); }
        public event Action<ShotInfo> Pulsed { add=>pulses.Add(value); remove=>pulses.Remove(value); }
        public event Action Ended { add=>ended.Add(value); remove=>ended.Remove(value); }
        public bool CanBegin(BeamDefinition recipe)
        {
            if(!isActiveAndEnabled || IsRunning || advancing || finishing || recipe==null || !recipe.IsValid
                || recipe.Attack.ImpactArea!=null && (areaPool==null || !areaPool.isActiveAndEnabled)
                || !CombatControl.IsAllowed(OwnerRoot.gameObject,CombatRestriction.Cast)) return false;
            var stats=OwnerRoot.GetComponent<StatusController>(); var outgoing=stats!=null ? stats.GetStatMultiplier(recipe.Attack.DamageModifierStat) : 1;
            if(!CombatMath.Positive(outgoing) || !recipe.Attack.RollDamage(OwnerRoot.gameObject,Muzzle.position,Muzzle.forward,0,1,outgoing).IsValid) return false;
            var budget=OwnerRoot.GetComponent<CombatResources>(); return recipe.PulseCosts.Length==0 || budget!=null && budget.CanSpend(recipe.PulseCosts);
        }
        public bool Begin(BeamDefinition recipe,DamageOrigin parent=default)
        {
            if(!CanBegin(recipe)) return false;
            definition=recipe; source=OwnerRoot; sourceHealth=source.GetComponentInParent<Health>(); sourceLife=sourceHealth!=null ? sourceHealth.Identity.Id : 0;
            resources=source.GetComponent<CombatResources>(); var stats=source.GetComponent<StatusController>(); multiplier=stats!=null ? stats.GetStatMultiplier(recipe.Attack.DamageModifierStat) : 1;
            origin=parent.CastId!=0 ? parent : DamageOrigin.Capture(source.gameObject,recipe.Attack);
            payload=recipe.RerollEachPulse ? default : recipe.Attack.RollRandom(source.gameObject,Muzzle.position,Muzzle.forward,multiplier,origin);
            if(!recipe.RerollEachPulse && !payload.IsValid) return false;
            duration=recipe.Duration; interval=recipe.Interval; reroll=recipe.RerollEachPulse; perSecond=recipe.DamagePerSecond; interrupt=recipe.InterruptWhenCastBlocked;
            elapsed=0; nextPulse=recipe.PulseImmediately ? 0 : interval; IsRunning=true;
            impactCallback??=impacts.Publish; Reconcile(); if(recipe.PulseImmediately) Advance(0); return true;
        }
        public void Advance(float deltaTime)
        {
            if(!IsRunning || advancing || !CombatMath.Finite(deltaTime) || deltaTime<0) return;
            if(!SourceValid()) { Stop(); return; }
            advancing=true;
            try
            {
                elapsed+=deltaTime; var limit=Math.Min(elapsed,duration); var count=0;
                while(IsRunning && nextPulse<=limit+1e-7 && count++<8)
                {
                    if(!SourceValid()) { Stop(); break; }
                    nextPulse+=interval; Pulse();
                }
                if(elapsed>=duration && nextPulse>duration+1e-7) Stop();
            }
            catch { Stop(); throw; }
            finally { advancing=false; trace.Clear(); if(!IsRunning) Finish(); }
        }
        private bool SourceValid()=>source!=null && definition!=null && definition.IsValid && isActiveAndEnabled
            && (sourceLife==0 || sourceHealth!=null && sourceHealth.Identity.Id==sourceLife && sourceHealth.IsStanding)
            && (!interrupt || CombatControl.IsAllowed(source.gameObject,CombatRestriction.Cast));
        private void Pulse()
        {
            var at=Muzzle.position; var direction=Muzzle.forward;
            if(!CombatMath.Finite(at) || !CombatMath.Finite(direction)) { Stop(); return; }
            var receipt=default(ResourceSpendReceipt);
            if(definition.PulseCosts.Length>0 && (resources==null || !resources.TrySpendSilently(definition.PulseCosts,out receipt))) { Stop(); return; }
            CombatReactionQueue.Enter();
            try
            {
                var request=reroll ? definition.Attack.RollRandom(source.gameObject,at,direction,multiplier,origin.Child(CombatOriginKind.Attack)) : payload;
                if(perSecond) request=request.Scale(interval);
                trace.Begin(definition.Attack,request,source,direction,areaPool); trace.Advance(at,definition.Attack.Range,impactCallback,out var end); trace.CompleteHitscan(end,impactCallback);
                pulses.Publish(new ShotInfo(at,end,DamageDelivery.Hitscan,definition.Attack.HitscanRadius,definition.Attack,request.Origin,request.IsCritical,request.Source));
            }
            finally { trace.Clear(); try { receipt.NotifyAndRelease(); } finally { CombatReactionQueue.Exit(); } }
        }
        public void Stop() { if(!IsRunning) return; IsRunning=false; trace.Stop(); if(!advancing) Finish(); }
        private void Finish()
        {
            if(finishing) return; finishing=true;
            try { definition=null; payload=default; source=null; sourceHealth=null; resources=null; trace.Clear(); DeliveryScheduler.Unregister(this); ended.Publish(); }
            finally { finishing=false; }
        }
        private void Reconcile() { if(((ICombatDeliveryTick)this).WantsAutomaticTick) DeliveryScheduler.Register(this); else DeliveryScheduler.Unregister(this); }
        private void OnDisable()=>Stop();
        public void CancelForRestore()=>Stop();
    }
}
