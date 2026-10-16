using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,RequireComponent(typeof(Health),typeof(Hurtbox)),AddComponentMenu("EasyCombat/Composite/Body Part")]
    public sealed partial class CombatBodyPart : MonoBehaviour,IDamageable,ICombatTargetGroup
    {
        [SerializeField] private BodyPartDefinition definition;
        [SerializeField] private Health core;
        private Health local,subscribedCore;
        private Collider[] colliders=Array.Empty<Collider>();
        private bool[] originalColliderStates=Array.Empty<bool>();
        private bool initialized,broken,applying;
        private long activeCoreLife;
        private DamageResult lastHit;
        private DamageInfo activeRequest;
        private readonly CombatSignal<CombatBodyPart> brokenSignal=new CombatSignal<CombatBodyPart>();
        public event Action<CombatBodyPart> Broken { add=>brokenSignal.Add(value); remove=>brokenSignal.Remove(value); }
        public Health LocalHealth { get { if(local==null) local=GetComponent<Health>(); return local; } }
        public Health Core=>core;
        public BodyPartDefinition Definition=>definition;
        public bool IsBroken=>broken || !LocalHealth.IsAlive;
        public MonoBehaviour HitGroup=>definition!=null && definition.GroupWithCore && core!=null ? core : this;
        public Health RulesTarget=>definition!=null && definition.RespectCoreEligibility && core!=null ? core : LocalHealth;
        public long LifeId=>LocalHealth.Identity.Id;
        private void Awake() { if(definition!=null) Configure(definition,core); }
        private void OnEnable() { if(initialized) Bind(); }
        private void OnDisable()
        { if(local!=null) { local.Damaged-=Damaged; local.Died-=Died; } }
        private void OnDestroy()=>Unbind();
        public bool Configure(BodyPartDefinition profile,Health owner)
        {
            if(profile==null || !profile.IsValid || owner==LocalHealth) return false;
            Unbind(); definition=profile; core=owner;
            if(core==null && transform.parent!=null) core=transform.parent.GetComponentInParent<Health>();
            colliders=GetComponentsInChildren<Collider>(true); originalColliderStates=new bool[colliders.Length];
            for(var i=0;i<colliders.Length;i++) originalColliderStates[i]=colliders[i].enabled;
            LocalHealth.ApplyDefinition(profile.Health); broken=false; lastHit=default;
            var zone=GetComponent<Hurtbox>(); zone.Configure(this,zone.Multiplier);
            initialized=true; Bind(); return true;
        }
        private void Bind()
        {
            if(!isActiveAndEnabled) return;
            LocalHealth.Damaged-=Damaged; LocalHealth.Damaged+=Damaged;
            LocalHealth.Died-=Died; LocalHealth.Died+=Died;
            subscribedCore=core;
            if(subscribedCore!=null) { subscribedCore.Died-=CoreDied; subscribedCore.Died+=CoreDied; subscribedCore.Changed-=CoreChanged; subscribedCore.Changed+=CoreChanged; }
        }
        private void Unbind()
        {
            if(local!=null) { local.Damaged-=Damaged; local.Died-=Died; }
            if(subscribedCore!=null) { subscribedCore.Died-=CoreDied; subscribedCore.Changed-=CoreChanged; }
            subscribedCore=null;
        }
        public DamageResult ApplyDamage(DamageInfo damage)
        {
            if(applying) return DamageResult.Rejected(damage,DamageRejection.Reentrant);
            applying=true; activeRequest=damage; activeCoreLife=core!=null ? core.Identity.Id : 0; CombatReactionQueue.Enter();
            try { return Apply(damage); }
            finally { applying=false; activeRequest=default; CombatReactionQueue.Exit(); }
        }
        private DamageResult Reject(DamageInfo damage,DamageRejection reason)
        {
            var result=DamageResult.Rejected(damage,reason); CombatEvents.Publish(LocalHealth.Identity,result,LocalHealth); return result;
        }
        private DamageResult Apply(DamageInfo damage)
        {
            if(!isActiveAndEnabled) return Reject(damage,DamageRejection.Disabled);
            if(definition==null || !definition.IsValid) return Reject(damage,DamageRejection.InvalidDefense);
            if(core!=null && definition.RespectCoreEligibility)
            {
                var rejection=core.CheckSharedEligibility(damage); if(rejection!=DamageRejection.None) return Reject(damage,rejection);
            }
            if(core!=null && definition.PercentageUsesCoreHealth && damage.HasFormula)
            { if(!damage.TryResolve(core.Current,core.Maximum,out damage)) return Reject(damage,DamageRejection.InvalidAmount); }
            if(core!=null && definition.TransferStatusesToCore && !damage.HasFormula && damage.Amount==0 && damage.IsValid)
                return core.ApplyDamage(damage);
            if(definition.SuppressLocalStatuses && core!=null) damage=damage.WithStatuses(null);
            return LocalHealth.ApplyDamage(damage);
        }
        private void Damaged(DamageResult result)
        {
            lastHit=result;
            if(core==null || !core.isActiveAndEnabled || !core.IsAlive || definition==null || definition.CoreDamageFraction<=0) return;
            if(applying && core.Identity.Id!=activeCoreLife) return;
            var parts=new DamageAmounts();
            for(var i=0;i<result.Breakdown.Count;i++)
            {
                var part=result.Breakdown[i]; parts.Add(part.Type,(definition.ShareBeforeLocalHpClamp ? part.HealthDamage : part.Applied)*definition.CoreDamageFraction);
            }
            if(parts.Total<=0) return;
            var hit=result.Damage;
            var forwarded=new DamageInfo(parts,hit.Source,hit.Point,hit.Direction,hit.IsCritical,hit.IsPeriodic,
                onHitStatuses:definition.TransferStatusesToCore ? applying ? activeRequest.OnHitStatuses : hit.OnHitStatuses : null,
                origin:hit.Origin.Child(CombatOriginKind.BodyPart),statusPolicy:hit.StatusPolicy,protection:hit.Protection);
            CombatReactionQueue.EnqueueDamage(core,forwarded);
        }
        private void Died(Health _)
        {
            if(broken || local==null || local.IsAlive) return; broken=true; var life=local.Identity.Id;
            if(core!=null && core.isActiveAndEnabled && core.IsAlive && (!applying || core.Identity.Id==activeCoreLife) && definition!=null && definition.CoreBreakAttack!=null)
            {
                var hit=lastHit.Damage;
                var damage=definition.CoreBreakAttack.RollRandom(hit.Source,core.transform.position,hit.Direction,origin:hit.Origin.Child(CombatOriginKind.BodyPart));
                CombatReactionQueue.EnqueueDamage(core,damage);
            }
            brokenSignal.Publish(this);
            if(this==null || local==null || local.Identity.Id!=life) return;
            if(definition!=null && definition.OnBreak==BodyPartBreakBehavior.DisableObject) gameObject.SetActive(false);
            else if(definition!=null && definition.OnBreak==BodyPartBreakBehavior.DisableColliders) SetColliders(false);
        }
        private void CoreDied(Health _) { if(definition!=null && definition.DisableWithCoreDeath) SetColliders(false); }
        private void CoreChanged(HealthChange change)
        {
            if(this==null || !enabled || definition==null || !definition.ResetWithCore) return;
            if(change.Reason==HealthChangeReason.Reset) ResetPart();
        }
        private void SetColliders(bool restore)
        { for(var i=0;i<colliders.Length;i++) if(colliders[i]!=null) colliders[i].enabled=restore && originalColliderStates[i]; }
        public void ResetPart()
        {
            if(!initialized) return;
            broken=false; lastHit=default; LocalHealth.ResetToFull(); SetColliders(true);
            gameObject.SetActive(true); Bind();
        }
    }
}
