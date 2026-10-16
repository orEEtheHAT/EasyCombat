using System;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("EasyCombat/2D/Damage Emitter")]
    public sealed class DamageEmitter2D:MonoBehaviour
    {
        [SerializeField] private AttackDefinition attack;
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private ProjectilePool2D pool;
        private readonly AttackTrace2D trace=new AttackTrace2D();
        private readonly AreaDamageQuery2D area=new AreaDamageQuery2D();
        private readonly CombatSignal<DamageImpact2D> impacts=new CombatSignal<DamageImpact2D>();
        private readonly CombatSignal<ShotInfo> shots=new CombatSignal<ShotInfo>();
        private Action<DamageImpact2D> impactCallback;
        private double nextShot=double.NegativeInfinity;
        private bool firing;
        public AttackDefinition Attack { get=>attack; set=>attack=value; }
        public Transform OwnerRoot { get=>ownerRoot!=null ? ownerRoot : transform; set=>ownerRoot=value; }
        public ProjectilePool2D Pool { get=>pool; set=>pool=value; }
        public event Action<DamageImpact2D> Impacted { add=>impacts.Add(value); remove=>impacts.Remove(value); }
        public event Action<ShotInfo> Fired { add=>shots.Add(value); remove=>shots.Remove(value); }
        public bool TryFire(Vector2 origin,Vector2 direction)
        {
            if(firing || !isActiveAndEnabled || !DamageQuery2D.Supports(attack) || !CombatMath.Finite(origin) || !CombatMath.Finite(direction)
                || direction.sqrMagnitude<.000001f || CombatClock.Now<nextShot || !CombatControl.IsAllowed(OwnerRoot.gameObject,CombatRestriction.Attack)
                || attack.Delivery==DamageDelivery.Projectile && (pool==null || !pool.CanSpawn)) return false;
            var source=OwnerRoot; var statuses=source.GetComponent<StatusController>();
            var power=statuses!=null ? statuses.GetStatMultiplier(attack.DamageModifierStat) : 1; var rate=statuses!=null ? statuses.GetStatMultiplier(attack.FireRateModifierStat) : 1;
            if(!CombatMath.Positive(power) || !CombatMath.Positive(rate)) return false;
            direction.Normalize(); var request=attack.RollRandom(source.gameObject,origin,direction,power); if(!request.IsValid) return false;
            firing=true; CombatReactionQueue.Enter();
            try
            {
                var definition=attack; nextShot=CombatClock.Now+definition.Cooldown/rate; impactCallback??=impacts.Publish; var end=origin+direction*definition.Range;
                if(definition.Delivery==DamageDelivery.Area) { end=origin; area.Apply(origin,direction,request,new AreaSettings(definition.Area),source,impactCallback); }
                else if(definition.Delivery==DamageDelivery.Projectile)
                { if(pool.Spawn(origin,direction,request,source,definition,impactCallback)==null) { nextShot=double.NegativeInfinity; return false; } }
                else { trace.Begin(definition,request,source,direction); trace.Advance(origin,definition.Range,impactCallback,out end); trace.Complete(end,impactCallback); }
                shots.Publish(new ShotInfo(origin,end,definition.Delivery,definition.Delivery==DamageDelivery.Area ? 0 : definition.HitRadius)); return true;
            }
            finally { trace.Clear(); firing=false; CombatReactionQueue.Exit(); }
        }
        public void ResetCooldown()=>nextShot=double.NegativeInfinity;
    }
}
