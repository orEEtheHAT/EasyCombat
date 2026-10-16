using System;
using UnityEngine;

namespace EasyCombat
{
    public enum AbilityRejection { None, Invalid, Disabled, Busy, Blocked, Cooldown, Charges, Resources, Target, Range, LineOfSight, Delivery, Cancelled, OwnerChanged, ExecutionFailed }
    public enum AbilityEventKind { Started, Committed, Cancelled, Failed, Recharged, Reset }
    public readonly struct AbilityEvent
    {
        public readonly AbilityDefinition Definition;
        public readonly AbilityEventKind Kind;
        public readonly AbilityRejection Reason;
        public readonly long CastId;
        public AbilityEvent(AbilityDefinition definition,AbilityEventKind kind,AbilityRejection reason,long castId)
        { Definition=definition; Kind=kind; Reason=reason; CastId=castId; }
    }
    public readonly struct AbilityView
    {
        public readonly AbilityDefinition Definition;
        public readonly int Charges;
        public readonly float Cooldown,RechargeRemaining;
        public AbilityView(AbilityDefinition definition,int charges,float cooldown,float recharge)
        { Definition=definition; Charges=charges; Cooldown=cooldown; RechargeRemaining=recharge; }
    }
    [DisallowMultipleComponent,RequireComponent(typeof(Health)),AddComponentMenu("EasyCombat/Abilities/Combat Abilities")]
    public sealed partial class CombatAbilities:MonoBehaviour
    {
        [SerializeField] private AbilityDefinition[] abilities=Array.Empty<AbilityDefinition>();
        [SerializeField] private Transform muzzle;
        [SerializeField] private ProjectilePool projectilePool;
        [SerializeField] private DamageAreaPool areaPool;
        [SerializeField] private bool automaticAdvance=true;
        private struct State { public AbilityDefinition Definition; public int Charges; public float Cooldown,Recharge; }
        private State[] states=Array.Empty<State>();
        private Health owner;
        private CombatResources resources;
        private readonly DamageQuery visibility=new DamageQuery();
        private readonly AttackTrace trace=new AttackTrace();
        private readonly AreaDamageQuery area=new AreaDamageQuery();
        private readonly CombatSignal<AbilityEvent> events=new CombatSignal<AbilityEvent>();
        private readonly CombatSignal<DamageImpact> impacts=new CombatSignal<DamageImpact>();
        private Action<DamageImpact> impactCallback;
        private bool configured,mutating,resetRequested;
        private AbilityRejection pendingCancel;
        private int casting=-1;
        private float castRemaining,globalCooldown;
        private Health castTarget;
        private long targetLife;
        private Vector3 castPoint;
        private DamageOrigin castOrigin;
        internal int SchedulerIndex=-1;
        public event Action<AbilityEvent> Changed { add=>events.Add(value); remove=>events.Remove(value); }
        public event Action<DamageImpact> Impacted { add=>impacts.Add(value); remove=>impacts.Remove(value); }
        public Health Owner { get { if(owner==null) owner=GetComponent<Health>(); return owner; } }
        public Transform Muzzle { get=>muzzle!=null ? muzzle : transform; set=>muzzle=value; }
        public ProjectilePool Pool { get=>projectilePool; set=>projectilePool=value; }
        public DamageAreaPool AreaPool { get=>areaPool; set=>areaPool=value; }
        public bool AutomaticAdvance { get=>automaticAdvance; set { automaticAdvance=value; Reconcile(); } }
        public int Count=>states.Length;
        public bool IsCasting=>casting>=0;
        public float CastRemaining=>castRemaining;
        public float GlobalCooldownRemaining=>globalCooldown;
        public AbilityDefinition Casting=>casting>=0 ? states[casting].Definition : null;
        public AbilityView GetAbility(int index) { var s=states[index]; return new AbilityView(s.Definition,s.Charges,s.Cooldown,s.Recharge); }
        private void OnEnable() { if(!configured) Configure(abilities); else { Bind(); Reconcile(); } }
        private void Bind()
        {
            owner=GetComponent<Health>(); resources=GetComponent<CombatResources>();
            if(!isActiveAndEnabled) return;
            owner.Changed-=HealthChanged; owner.Changed+=HealthChanged;
            owner.Died-=OwnerStopped; owner.Died+=OwnerStopped; owner.Downed-=OwnerStopped; owner.Downed+=OwnerStopped;
        }
        private void OnDisable()
        {
            if(owner!=null) { owner.Changed-=HealthChanged; owner.Died-=OwnerStopped; owner.Downed-=OwnerStopped; }
            Interrupt(AbilityRejection.Disabled); AbilityScheduler.Unregister(this);
        }
        private void OnDestroy()=>OnDisable();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession() { foreach(var actor in FindObjectsByType<CombatAbilities>(FindObjectsSortMode.None)) { actor.Bind(); actor.Reconcile(); } }
        public bool Configure(AbilityDefinition[] definitions)
        {
            if(mutating || IsCasting || definitions==null || definitions.Length>32) return false;
            for(var i=0;i<definitions.Length;i++)
            { if(definitions[i]==null || !definitions[i].IsValid) return false; for(var j=0;j<i;j++) if(definitions[j]==definitions[i]) return false; }
            states=new State[definitions.Length];
            for(var i=0;i<states.Length;i++) states[i]=new State { Definition=definitions[i],Charges=definitions[i].MaximumCharges };
            abilities=(AbilityDefinition[])definitions.Clone(); configured=true; globalCooldown=0; Bind(); Reconcile(); return true;
        }
        private void HealthChanged(HealthChange change)
        { if(change.Reason==HealthChangeReason.Reset || change.Reason==HealthChangeReason.Restore) ResetAbilities(); }
        private void OwnerStopped(Health _) =>Interrupt(AbilityRejection.Blocked);
        private int Find(AbilityDefinition definition) { for(var i=0;i<states.Length;i++) if(states[i].Definition==definition) return i; return -1; }
        public bool TryActivate(AbilityDefinition definition,Vector3 point,Health target=null)=>TryActivate(definition,point,target,out _);
        public bool TryActivate(AbilityDefinition definition,Vector3 point,Health target,out AbilityRejection reason)
        {
            reason=AbilityRejection.None;
            if(mutating || IsCasting) { reason=AbilityRejection.Busy; return false; }
            var index=Find(definition);
            if(index<0 || definition==null || !definition.IsValid || !CombatMath.Finite(point)) { reason=AbilityRejection.Invalid; return false; }
            if(!isActiveAndEnabled) { reason=AbilityRejection.Disabled; return false; }
            if(!Owner.IsStanding || !CombatControl.IsAllowed(gameObject,CombatRestriction.Cast)) { reason=AbilityRejection.Blocked; return false; }
            var state=states[index];
            if(state.Cooldown>0 || globalCooldown>0) { reason=AbilityRejection.Cooldown; return false; }
            if(definition.MaximumCharges>0 && state.Charges<=0) { reason=AbilityRejection.Charges; return false; }
            casting=index; castPoint=point; castTarget=definition.TargetMode==AbilityTargetMode.Self ? Owner : target;
            if(definition.TargetMode==AbilityTargetMode.Unit && castTarget!=null) castPoint=castTarget.transform.position;
            targetLife=castTarget!=null ? castTarget.Identity.Id : 0;
            var source=DamageOrigin.Capture(gameObject); var action=CombatActionOrigin.Root(CombatOriginKind.Ability);
            castOrigin=new DamageOrigin(source.Actor,castId:action.Id,hasTeam:source.HasTeam,team:source.Team,action:action);
            if(!Prepare(out _,out reason)) { ClearCast(); return false; }
            castRemaining=definition.CastTime; mutating=true; CombatReactionQueue.Enter();
            try
            {
                events.Publish(new AbilityEvent(definition,AbilityEventKind.Started,AbilityRejection.None,castOrigin.CastId));
                if(pendingCancel!=AbilityRejection.None || resetRequested) { CancelNow(pendingCancel==AbilityRejection.None ? AbilityRejection.OwnerChanged : pendingCancel); return true; }
                if(castRemaining<=0) Commit(); return true;
            }
            finally { try { CompleteMutation(); } finally { CombatReactionQueue.Exit(); } }
        }
        private bool Prepare(out AbilityContext context,out AbilityRejection reason)
        {
            context=default; reason=AbilityRejection.None;
            var definition=Casting;
            if(definition==null || !definition.IsValid) { reason=AbilityRejection.Invalid; return false; }
            if(owner==null || owner.Identity.Id!=castOrigin.Actor.Id) { reason=AbilityRejection.OwnerChanged; return false; }
            if(!isActiveAndEnabled || !owner.IsStanding || !CombatControl.IsAllowed(gameObject,CombatRestriction.Cast)) { reason=AbilityRejection.Blocked; return false; }
            var point=castPoint; var target=castTarget;
            if(definition.TargetMode==AbilityTargetMode.Unit || definition.TargetMode==AbilityTargetMode.Self)
            {
                if(target==null || !target.isActiveAndEnabled || target.Identity.Id!=targetLife
                    || !target.IsAlive && !definition.AllowDeadTargets || target.LifeState==CombatLifeState.Downed && !definition.AllowDownedTargets)
                { reason=AbilityRejection.Target; return false; }
                if(definition.TargetMode==AbilityTargetMode.Self || definition.FollowTargetDuringCast) point=target.transform.position;
                if(definition.TargetMode==AbilityTargetMode.Unit && !AllowsTarget(definition,target)) { reason=AbilityRejection.Target; return false; }
            }
            else target=null;
            var origin=Muzzle.position; var delta=point-origin;
            if(!CombatMath.Finite(origin) || !CombatMath.Finite(point)) { reason=AbilityRejection.Invalid; return false; }
            if(definition.TargetMode==AbilityTargetMode.Point || definition.TargetMode==AbilityTargetMode.Unit)
            { var rangePoint=definition.TargetMode==AbilityTargetMode.Unit ? target.transform.position : point;
              if((rangePoint-transform.position).sqrMagnitude>definition.Range*definition.Range) { reason=AbilityRejection.Range; return false; } }
            if(definition.TargetMode==AbilityTargetMode.Direction && delta.sqrMagnitude<.000001f) { reason=AbilityRejection.Target; return false; }
            var direction=delta.sqrMagnitude>.000001f ? delta.normalized : transform.forward;
            var sightDelta=definition.TargetMode==AbilityTargetMode.Unit ? target.transform.position-origin : delta;
            if(definition.RequireLineOfSight && sightDelta.sqrMagnitude>.000001f && visibility.Cast(origin,sightDelta.normalized,sightDelta.magnitude,0,definition.ObstacleMask,QueryTriggerInteraction.Ignore,transform,out var obstruction))
            {
                var blocked=obstruction.collider.GetComponentInParent<Health>();
                if(target==null || blocked!=target) { reason=AbilityRejection.LineOfSight; return false; }
            }
            context=new AbilityContext(this,definition,owner,target,origin,point,direction,castOrigin);
            if(resources==null) resources=GetComponent<CombatResources>();
            if(definition.Costs.Length>0 && (resources==null || !resources.CanSpend(definition.Costs))) { reason=AbilityRejection.Resources; return false; }
            var projectiles=0; var melees=0; var beams=0; BeamDefinition beam=null;
            foreach(var action in definition.Actions)
            {
                try { if(!action.CanExecute(in context)) { reason=AbilityRejection.Delivery; return false; } }
                catch(Exception exception) { Debug.LogException(exception); reason=AbilityRejection.Delivery; return false; }
                if(action is AbilityAttackAction attack && !attack.DirectToTarget && attack.Attack.Delivery==DamageDelivery.Projectile) projectiles++;
                if(action is AbilityMeleeAction) melees++;
                if(action is AbilityBeamAction channel) { beams++; beam=channel.Beam; }
            }
            if(melees>1 || beams>1) { reason=AbilityRejection.Delivery; return false; }
            if(beam!=null && beam.PulseImmediately && beam.PulseCosts.Length>0
                && (resources==null || !resources.CanSpend(definition.Costs,beam.PulseCosts))) { reason=AbilityRejection.Resources; return false; }
            if(projectiles>0 && (projectilePool==null || projectilePool.RemainingCapacity<projectiles)) { reason=AbilityRejection.Delivery; return false; }
            return true;
        }
        private bool AllowsTarget(AbilityDefinition definition,Health target)
        {
            if(target==owner) return definition.AllowSelf;
            if(definition.UnitTargets==AreaTargetFilter.All) return true;
            var a=GetComponentInParent<CombatTeam>(); var b=target.GetComponentInParent<CombatTeam>();
            if(a==null || b==null) return false;
            return definition.UnitTargets==AreaTargetFilter.Allies ? a.Team==b.Team : a.Team!=b.Team;
        }
        private void Commit()
        {
            if(!Prepare(out var context,out var reason)) { CancelNow(reason); return; }
            var index=casting; var definition=context.Definition; var receipt=default(ResourceSpendReceipt);
            if(definition.Costs.Length>0 && !resources.TrySpendSilently(definition.Costs,out receipt)) { CancelNow(AbilityRejection.Resources); return; }
            // Reserve all timing and charges before any effect/callback can re-enter.
            var state=states[index]; state.Cooldown=definition.Cooldown;
            if(definition.MaximumCharges>0) { if(state.Charges==definition.MaximumCharges) state.Recharge=definition.ChargeRecovery; state.Charges--; }
            states[index]=state; globalCooldown=Mathf.Max(globalCooldown,definition.GlobalCooldown); ClearCast();
            var success=true;
            try
            {
                foreach(var action in definition.Actions)
                {
                    if(this==null || !isActiveAndEnabled || owner==null || owner.Identity.Id!=context.SourceOrigin.Actor.Id || !owner.IsStanding || resetRequested
                        || context.TargetLife!=0 && (context.Target==null || context.Target.Identity.Id!=context.TargetLife)) { success=false; break; }
                    try { if(!action.Execute(in context)) success=false; } catch(Exception exception) { success=false; Debug.LogException(exception); }
                }
                events.Publish(new AbilityEvent(definition,success ? AbilityEventKind.Committed : AbilityEventKind.Failed,
                    success ? AbilityRejection.None : AbilityRejection.ExecutionFailed,context.SourceOrigin.CastId));
            }
            finally { receipt.NotifyAndRelease(); }
        }
        public bool Cancel()
        {
            if(!IsCasting || !Casting.CanCancel) return false;
            Interrupt(AbilityRejection.Cancelled); return true;
        }
        public void Interrupt(AbilityRejection reason=AbilityRejection.Cancelled)
        {
            if(!IsCasting) return; if(reason==AbilityRejection.None) reason=AbilityRejection.Cancelled;
            if(mutating) { pendingCancel=reason; return; }
            mutating=true; try { CancelNow(reason); } finally { CompleteMutation(); }
        }
        private void CancelNow(AbilityRejection reason)
        {
            var definition=Casting; var id=castOrigin.CastId; ClearCast();
            if(definition!=null) events.Publish(new AbilityEvent(definition,AbilityEventKind.Cancelled,reason,id));
        }
        private void ClearCast() { casting=-1; castRemaining=0; castTarget=null; targetLife=0; castPoint=default; castOrigin=default; pendingCancel=AbilityRejection.None; }
        public void Advance(float deltaTime)
        {
            if(mutating || !isActiveAndEnabled || !CombatMath.Positive(deltaTime)) return;
            mutating=true; CombatReactionQueue.Enter();
            try
            {
                if(IsCasting && (owner==null || owner.Identity.Id!=castOrigin.Actor.Id || !owner.IsStanding
                    || Casting.InterruptWhenCastBlocked && !CombatControl.IsAllowed(gameObject,CombatRestriction.Cast))) CancelNow(AbilityRejection.Blocked);
                var beforeRelease=IsCasting ? Mathf.Min(deltaTime,castRemaining) : deltaTime;
                AdvanceTimers(beforeRelease);
                if(IsCasting)
                {
                    castRemaining=Mathf.Max(0,castRemaining-beforeRelease);
                    if(pendingCancel!=AbilityRejection.None || resetRequested) CancelNow(pendingCancel==AbilityRejection.None ? AbilityRejection.OwnerChanged : pendingCancel);
                    else if(castRemaining<=0) Commit();
                }
                if(deltaTime>beforeRelease) AdvanceTimers(deltaTime-beforeRelease);
            }
            finally { try { CompleteMutation(); } finally { CombatReactionQueue.Exit(); } }
        }
        private void AdvanceTimers(float dt)
        {
            globalCooldown=Mathf.Max(0,globalCooldown-dt);
            for(var i=0;i<states.Length;i++)
            {
                var state=states[i]; var d=state.Definition; if(d==null || !d.IsValid) continue;
                state.Cooldown=Mathf.Max(0,state.Cooldown-dt); var recharged=false;
                if(d.MaximumCharges>0 && state.Charges<d.MaximumCharges)
                {
                    var remaining=(double)state.Recharge-dt;
                    if(remaining<=0)
                    {
                        var gained=(int)Math.Min(d.MaximumCharges-state.Charges,1+Math.Floor(-remaining/d.ChargeRecovery));
                        state.Charges+=gained; remaining+=gained*(double)d.ChargeRecovery; recharged=true;
                    }
                    state.Recharge=state.Charges>=d.MaximumCharges ? 0 : (float)Math.Max(0,remaining);
                }
                states[i]=state;
                if(recharged) events.Publish(new AbilityEvent(d,AbilityEventKind.Recharged,AbilityRejection.None,0));
            }
        }
        public void ResetAbilities()
        {
            if(mutating) { resetRequested=true; return; }
            mutating=true;
            try
            {
                CancelNow(AbilityRejection.OwnerChanged); globalCooldown=0;
                for(var i=0;i<states.Length;i++) { var definition=states[i].Definition; states[i]=new State { Definition=definition,Charges=definition!=null ? definition.MaximumCharges : 0 }; }
                events.Publish(new AbilityEvent(null,AbilityEventKind.Reset,AbilityRejection.None,0));
            }
            finally { resetRequested=false; CompleteMutation(); }
        }
        private void CompleteMutation()
        {
            mutating=false; if(this==null) return;
            if(resetRequested) { resetRequested=false; ResetAbilities(); }
            else if(pendingCancel!=AbilityRejection.None) Interrupt(pendingCancel);
            Reconcile();
        }
        internal bool NeedsTick
        {
            get
            {
                if(this==null || !automaticAdvance || !isActiveAndEnabled) return false;
                if(IsCasting || globalCooldown>0) return true;
                foreach(var state in states) if(state.Cooldown>0 || state.Recharge>0) return true;
                return false;
            }
        }
        private void Reconcile() { if(this==null) return; if(NeedsTick) AbilityScheduler.Register(this); else AbilityScheduler.Unregister(this); }
        internal bool CanDeliver(AttackDefinition attack)=>attack!=null && attack.IsValid
            && (attack.ImpactArea==null || areaPool!=null && areaPool.isActiveAndEnabled)
            && (attack.Delivery!=DamageDelivery.Projectile || projectilePool!=null && projectilePool.CanSpawn(attack));
        internal bool CanPrepareAttack(AttackDefinition attack,in AbilityContext context)
        {
            if(owner==null || attack==null || !attack.IsValid) return false;
            var statuses=owner.GetComponent<StatusController>(); var multiplier=statuses!=null ? statuses.GetStatMultiplier(attack.DamageModifierStat) : 1;
            return CombatMath.Positive(multiplier) && attack.RollDamage(owner.gameObject,context.Origin,context.Direction,0,1,multiplier).IsValid;
        }
        internal bool Deliver(AbilityAttackAction action,in AbilityContext context)
        {
            if(!action.CanExecute(in context)) return false;
            var attack=action.Attack; var source=owner.gameObject;
            var statuses=source.GetComponent<StatusController>(); var multiplier=statuses!=null ? statuses.GetStatMultiplier(attack.DamageModifierStat) : 1;
            if(!CombatMath.Positive(multiplier)) return false;
            var damage=attack.RollRandom(source,context.Origin,context.Direction,multiplier,context.OriginFor(attack)); if(!damage.IsValid) return false;
            if(impactCallback==null) impactCallback=impacts.Publish;
            if(action.DirectToTarget) { context.Target.ApplyDamage(damage.At(context.Point,context.Direction)); return true; }
            if(attack.Delivery==DamageDelivery.Projectile) return projectilePool.Spawn(context.Origin,context.Direction,damage,transform,attack,impactCallback,areaPool,context.Target)!=null;
            if(attack.Delivery==DamageDelivery.Area) { area.Apply(action.AreaAtPoint ? context.Point : context.Origin,context.Direction,damage,new AreaSettings(attack.Area),transform,impactCallback); return true; }
            try { trace.Begin(attack,damage,transform,context.Direction,areaPool); trace.Advance(context.Origin,attack.Range,impactCallback,out var end); trace.CompleteHitscan(end,impactCallback); return true; }
            finally { trace.Clear(); }
        }
    }
}
