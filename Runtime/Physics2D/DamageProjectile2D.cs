using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/2D/Damage Projectile")]
    public sealed class DamageProjectile2D:MonoBehaviour,IBatchedProjectile,ICombatTransient
    {
        private readonly AttackTrace2D trace=new AttackTrace2D();
        private float speed,range,lifetime;
        private Action<DamageImpact2D> impact;
        private Action<DamageProjectile2D> release;
        private bool launched,simulating;
        [SerializeField] private bool automaticAdvance=true;
        int IBatchedProjectile.BatchIndex { get; set; } = -1;
        private ProjectileFlight Flight => new ProjectileFlight
            { Direction = trace.Direction, Speed = speed, Range = range, Lifetime = lifetime };
        bool IBatchedProjectile.TryCaptureFlight(out ProjectileFlight flight) { flight = Flight; return true; }
        void IBatchedProjectile.CommitFlight(in ProjectileStep step) => Advance(in step);
        public bool IsFlying=>launched && trace.Active;
        public bool AutomaticAdvance { get=>automaticAdvance; set { automaticAdvance=value; Reconcile(); } }
        public float Radius=>trace.Radius;
        public void Launch(Vector2 origin,Vector2 direction,DamageInfo request,Transform source,AttackDefinition attack,
            Action<DamageImpact2D> onImpact=null,Action<DamageProjectile2D> returnToPool=null)
        {
            if(launched) throw new InvalidOperationException("An active 2D projectile cannot be launched twice.");
            if(!DamageQuery2D.Supports(attack) || !request.IsValid || !CombatMath.Finite(origin) || !CombatMath.Finite(direction) || direction.sqrMagnitude<.000001f)
                throw new ArgumentException("A 2D projectile requires supported attack, damage and direction.");
            trace.Begin(attack,request,source,direction); speed=attack.ProjectileSpeed; range=attack.Range; lifetime=attack.ProjectileLifetime;
            impact=onImpact; release=returnToPool; launched=true; transform.position=origin; transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            gameObject.SetActive(true); Reconcile();
        }
        public void Simulate(float deltaTime)
        {
            if(!IsFlying || simulating || !CombatMath.Positive(deltaTime)) return;
            ProjectileBatchScheduler.InvalidatePlan(this);
            var step = Flight.Plan(deltaTime);
            Advance(in step);
        }
        private void Advance(in ProjectileStep step)
        {
            simulating=true; CombatReactionQueue.Enter();
            try
            {
                trace.Advance(transform.position,step.Distance,impact,out var end,step.Offset); transform.position=end;
                range=step.RangeLeft; lifetime=step.LifetimeLeft; if(range<=0 || lifetime<=0) trace.Stop();
            }
            catch { trace.Stop(); throw; }
            finally { simulating=false; try { if(!trace.Active) Finish(); } finally { CombatReactionQueue.Exit(); } }
        }
        public void Despawn() { if(!launched) return; trace.Stop(); if(!simulating) Finish(); }
        private void Finish()
        {
            if(!launched) return; launched=false; var callback=release; release=null; impact=null; trace.Clear(); ProjectileBatchScheduler.Unregister(this);
            if(callback!=null) callback(this); else Destroy(gameObject);
        }
        private void Reconcile() { if(isActiveAndEnabled && automaticAdvance && IsFlying) ProjectileBatchScheduler.Register(this); else ProjectileBatchScheduler.Unregister(this); }
        private void OnEnable()=>Reconcile();
        private void OnDisable()=>Despawn();
        public void CancelForRestore()=>Despawn();
    }
}
