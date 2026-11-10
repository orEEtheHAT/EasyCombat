using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("EasyCombat/Damage Projectile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed partial class DamageProjectile : MonoBehaviour,ICombatTransient,IBatchedProjectile
    {
        private readonly AttackTrace trace = new AttackTrace();
        private float speed, remainingRange, remainingLifetime;
        private Action<DamageImpact> onImpact;
        private Action<DamageProjectile> release;
        private bool launched, simulating;
        private Transform pose;
        int IBatchedProjectile.BatchIndex { get; set; } = -1;
        private ProjectileFlight Flight => new ProjectileFlight
            { Direction = trace.Direction, Speed = speed, Range = remainingRange, Lifetime = remainingLifetime };
        bool IBatchedProjectile.TryCaptureFlight(out ProjectileFlight flight)
        {
            flight = Flight;
            return !usesGuidance;
        }
        void IBatchedProjectile.CommitFlight(in ProjectileStep step) => Advance(0f, in step);

        internal OverlapCapsuleCommand CreateProbe(PhysicsScene scene)
        {
            var start = pose.position;
            return new OverlapCapsuleCommand(scene, start, start, trace.ProbeRadius, trace.BatchFilter);
        }

        internal bool IsAtProbeStart(Vector3 start) => pose.position.Equals(start);

        // A certified empty sweep cannot invoke impact callbacks. Only use it while the world is still current.
        internal bool CommitEmpty(in ProjectileStep step, Vector3 start)
        {
            pose.position = start + step.Offset;
            remainingRange = step.RangeLeft;
            remainingLifetime = step.LifetimeLeft;
            if (remainingRange <= 0 || remainingLifetime <= 0)
            {
                Despawn();
                return false; // User release code may change arbitrary colliders.
            }
            // Moving a hitbox can invalidate speculative results belonging to later shots.
            return GetComponentInChildren<Collider>(true) == null;
        }
        public bool IsFlying => launched && trace.Active;
        public float Radius => trace.Radius;
        public Vector3 Direction=>trace.Direction;
        public bool IsReturning=>usesGuidance && guidance.Returning;

        // No Rigidbody or collision callbacks: each step sweeps the full spherical volume.
        public void Launch(Vector3 origin, Vector3 shotDirection, DamageInfo request, Transform sourceRoot,
            AttackDefinition attack, Action<DamageImpact> impact = null, Action<DamageProjectile> returnToPool = null, DamageAreaPool areaPool = null,Health homingTarget=null)
        {
            if (attack == null || !attack.IsValid || !CombatMath.Finite(origin)
                || !CombatMath.Finite(shotDirection) || shotDirection.sqrMagnitude < .000001f || !request.IsValid)
                throw new ArgumentException("Projectile launch requires a valid attack, damage and direction.");
            if (launched) throw new InvalidOperationException("An active projectile cannot be launched twice.");
            if (attack.ImpactArea != null && (areaPool == null || !areaPool.isActiveAndEnabled))
                throw new ArgumentException("This projectile requires an active DamageAreaPool.");
            trace.Begin(attack, request, sourceRoot, shotDirection, areaPool);
            ConfigureGuidance(attack.ProjectileMotion,sourceRoot,homingTarget);
            speed = attack.ProjectileSpeed;
            remainingRange = attack.Range;
            remainingLifetime = attack.ProjectileLifetime;
            onImpact = impact;
            release = returnToPool;
            pose=transform; pose.SetPositionAndRotation(origin, Quaternion.LookRotation(shotDirection));
            launched = true;
            gameObject.SetActive(true);
            RegisterIfFlying();
        }

        private void RegisterIfFlying()
        {
            if (isActiveAndEnabled && IsFlying) ProjectileBatchScheduler.Register(this);
        }
        private void OnEnable() => RegisterIfFlying();

        public void Simulate(float deltaTime)
        {
            if (!IsFlying || simulating || !CombatMath.Finite(deltaTime) || deltaTime<0 || deltaTime==0 && (!usesGuidance || guidance.PendingTime<=0)) return;
            ProjectileBatchScheduler.InvalidatePlan(this);
            var step = usesGuidance ? default : Flight.Plan(deltaTime);
            Advance(deltaTime, in step);
        }

        private void Advance(float deltaTime, in ProjectileStep step)
        {
            simulating = true;
            CombatReactionQueue.Enter();
            try
            {
                if(usesGuidance) { SimulateGuided(deltaTime); return; }
                trace.Advance(pose.position, step.Distance, onImpact, out var end, pose, displacement: step.Offset);
                pose.position = end;
                remainingRange = step.RangeLeft;
                remainingLifetime = step.LifetimeLeft;
                if (remainingRange <= 0 || remainingLifetime <= 0) trace.Stop();
            }
            catch { trace.Stop(); throw; }
            finally
            {
                simulating = false;
                try { if (!trace.Active) Finish(); }
                finally { CombatReactionQueue.Exit(); }
            }
        }

        public void Despawn()
        {
            if (!launched) return;
            if(guidance!=null) guidance.Cancelled=true;
            trace.Stop();
            // Do not return to a pool while callbacks still use this shot's trace.
            if (!simulating) Finish();
        }

        private void Finish()
        {
            if (!launched) return;
            launched = false;
            ProjectileBatchScheduler.Unregister(this);
            var returnToPool = release;
            release = null;
            onImpact = null;
            trace.Clear();
            guidance?.Clear(); usesGuidance=false;
            if (returnToPool != null) returnToPool(this);
            else Destroy(gameObject);
        }

        private void OnDisable() => Despawn();
        public void CancelForRestore()=>Despawn();
    }
}
