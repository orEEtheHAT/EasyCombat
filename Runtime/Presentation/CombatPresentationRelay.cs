using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/Presentation/Actor or Delivery Relay")]
    public sealed class CombatPresentationRelay:MonoBehaviour
    {
        [SerializeField] private CombatPresentationHub hub;
        [SerializeField] private Health health;
        [SerializeField] private DamageEmitter emitter;
        [SerializeField] private CombatAbilities abilities;
        [SerializeField] private DamageArea area;
        [SerializeField] private CombatMelee melee;
        [SerializeField] private CombatBeam beam;
        [SerializeField] private bool statusTickEvents;
        private StatusController statuses;
        private CombatActorIdentity cachedIdentity;
        public CombatPresentationHub Hub { get=>hub; set=>hub=value; }
        public bool StatusTickEvents { get=>statusTickEvents; set=>statusTickEvents=value; }
        public void Configure(CombatPresentationHub destination) { Unbind(); hub=destination; Resolve(); Bind(); }
        private void OnEnable() { Resolve(); Bind(); }
        private void Resolve()
        {
            if(hub==null) hub=GetComponentInParent<CombatPresentationHub>();
            if(health==null) health=GetComponent<Health>(); if(emitter==null) emitter=GetComponent<DamageEmitter>();
            if(abilities==null) abilities=GetComponent<CombatAbilities>(); if(area==null) area=GetComponent<DamageArea>();
            if(melee==null) melee=GetComponent<CombatMelee>(); if(beam==null) beam=GetComponent<CombatBeam>();
            statuses=health!=null ? health.GetComponent<StatusController>() : null; cachedIdentity=CombatActorIdentity.Capture(gameObject);
        }
        private void Bind()
        {
            if(!isActiveAndEnabled) return; Unbind();
            if(health!=null) { health.Resolved+=Resolved; health.Healed+=Healed; }
            if(statuses!=null) statuses.Lifecycle+=StatusChanged;
            if(emitter!=null) { emitter.Fired+=Shot; emitter.Impacted+=Impact; }
            if(abilities!=null) { abilities.Changed+=AbilityChanged; abilities.Impacted+=Impact; }
            if(area!=null) { area.Began+=AreaBegan; area.Ended+=AreaEnded; area.Impacted+=Impact; }
            if(melee!=null) melee.Impacted+=Impact;
            if(beam!=null) { beam.Pulsed+=Shot; beam.Impacted+=Impact; }
        }
        private void Unbind()
        {
            if(health!=null) { health.Resolved-=Resolved; health.Healed-=Healed; }
            if(statuses!=null) statuses.Lifecycle-=StatusChanged;
            if(emitter!=null) { emitter.Fired-=Shot; emitter.Impacted-=Impact; }
            if(abilities!=null) { abilities.Changed-=AbilityChanged; abilities.Impacted-=Impact; }
            if(area!=null) { area.Began-=AreaBegan; area.Ended-=AreaEnded; area.Impacted-=Impact; }
            if(melee!=null) melee.Impacted-=Impact;
            if(beam!=null) { beam.Pulsed-=Shot; beam.Impacted-=Impact; }
        }
        private void OnDisable()=>Unbind();
        private void OnDestroy()=>Unbind();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var relay in FindObjectsByType<CombatPresentationRelay>(FindObjectsSortMode.None)) if(relay.isActiveAndEnabled) { relay.Resolve(); relay.Bind(); } }
        private void Send(in CombatPresentationEvent value) { if(hub!=null) hub.Submit(in value); }
        private void Resolved(CombatRecord record)
        {
            if(this==null || !isActiveAndEnabled || !ReferenceEquals(record.TargetHealth,health)) return;
            var result=record.Result; var damage=result.Damage;
            var definition=damage.Origin.Status!=null ? (ScriptableObject)damage.Origin.Status : damage.Origin.Attack;
            if(result.AppliedAmount>0) DamageEvent(CombatPresentationKind.Damage,result.AppliedAmount);
            if(result.ShieldAbsorbed>0) DamageEvent(CombatPresentationKind.Shield,result.ShieldAbsorbed);
            if(result.Rejection==DamageRejection.Blocked) DamageEvent(CombatPresentationKind.Blocked,result.ArmorPrevented);
            if(result.Rejection==DamageRejection.Dodged) DamageEvent(CombatPresentationKind.Dodged,0);
            if(result.Killed) DamageEvent(CombatPresentationKind.Death,0);
            if(result.Downed) DamageEvent(CombatPresentationKind.Downed,0);
            void DamageEvent(CombatPresentationKind kind,float amount)
                =>Send(new CombatPresentationEvent(kind,record.Target,damage.Origin.Actor,damage.Point,amount,definition,damage.IsCritical,health,damage.Direction));
        }
        private void Healed(HealingResult result)
        {
            if(!result.Applied) return;
            Send(new CombatPresentationEvent(CombatPresentationKind.Healing,result.Target,result.Healing.SourceIdentity,result.Position,result.AppliedAmount+result.ShieldRestored,
                critical:result.Healing.IsCritical,health:health));
        }
        private void StatusChanged(StatusChange change)
        {
            if(health==null || change.Kind==StatusChangeKind.Tick && !statusTickEvents) return;
            var kind=change.Kind==StatusChangeKind.Removed ? CombatPresentationKind.StatusRemoved : change.Kind==StatusChangeKind.Tick ? CombatPresentationKind.StatusTick : CombatPresentationKind.StatusApplied;
            Send(new CombatPresentationEvent(kind,health.Identity,change.Origin.Actor,health.transform.position,Mathf.Abs(change.StacksAfter-change.StacksBefore),change.Definition,health:health));
        }
        private void AbilityChanged(AbilityEvent change)
        {
            if(change.Kind==AbilityEventKind.Reset || change.Kind==AbilityEventKind.Recharged) return;
            var kind=change.Kind==AbilityEventKind.Started ? CombatPresentationKind.AbilityStarted : change.Kind==AbilityEventKind.Committed ? CombatPresentationKind.AbilityCommitted : CombatPresentationKind.AbilityCancelled;
            var identity=health!=null ? health.Identity : cachedIdentity;
            Send(new CombatPresentationEvent(kind,identity,identity,transform.position,definition:change.Definition,health:health));
        }
        private void Shot(ShotInfo shot)=>PublishShot(hub,in shot);
        private void Impact(DamageImpact impact)
        {
            var collider=impact.Collider; var zone=collider!=null ? collider.GetComponentInParent<Hurtbox>() : null;
            var receiver=zone!=null ? zone.Receiver : collider!=null ? collider.GetComponentInParent<IDamageable>() as MonoBehaviour : null;
            PublishImpact(hub,receiver,impact.Point,impact.Normal,impact.Result);
        }
        private void AreaBegan(DamageArea value)=>AreaEvent(CombatPresentationKind.AreaStarted,value);
        private void AreaEnded(DamageArea value)=>AreaEvent(CombatPresentationKind.AreaEnded,value);
        private void AreaEvent(CombatPresentationKind kind,DamageArea value)
            =>Send(new CombatPresentationEvent(kind,value.Origin.Actor,value.Origin.Actor,value.transform.position,value.Radius,value.Origin.Attack,direction:value.transform.up));
        public static void PublishShot(CombatPresentationHub hub,in ShotInfo shot)
        {
            if(hub==null) return; var health=shot.Source!=null ? shot.Source.GetComponentInParent<Health>() : null;
            hub.Submit(new CombatPresentationEvent(CombatPresentationKind.Shot,shot.DamageOrigin.Actor,shot.DamageOrigin.Actor,shot.Origin,1,shot.Attack,shot.Critical,health,(shot.End-shot.Origin).normalized));
        }
        public static void PublishImpact(CombatPresentationHub hub,MonoBehaviour receiver,Vector3 point,Vector3 normal,DamageResult result)
        {
            if(hub==null) return; var health=CombatTargetGroups.Rules(receiver); var subject=health!=null ? health.Identity : default;
            hub.Submit(new CombatPresentationEvent(CombatPresentationKind.Impact,subject,result.Damage.Origin.Actor,point,result.AppliedAmount+result.ShieldAbsorbed,
                result.Damage.Origin.Attack,result.Damage.IsCritical,health,normal));
        }
    }
}
