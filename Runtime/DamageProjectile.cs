using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("EasyCombat/Damage Projectile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed partial class DamageProjectile : MonoBehaviour
    {
        private readonly AttackTrace trace = new AttackTrace();
        private float speed, remainingRange, remainingLifetime;
        private Action<DamageImpact> onImpact;
        private Action<DamageProjectile> release;
        private bool launched, simulating;
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
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(shotDirection));
            launched = true;
            gameObject.SetActive(true);
        }

        private void Update() => Simulate(Time.deltaTime);

        public void Simulate(float deltaTime)
        {
            if (!IsFlying || simulating || !CombatMath.Finite(deltaTime) || deltaTime<0 || deltaTime==0 && (!usesGuidance || guidance.PendingTime<=0)) return;
            simulating = true;
            CombatReactionQueue.Enter();
            try
            {
                if(usesGuidance) { SimulateGuided(deltaTime); return; }
                var step = Mathf.Min(remainingRange, speed * Mathf.Min(deltaTime, remainingLifetime));
                trace.Advance(transform.position, step, onImpact, out var end, transform);
                transform.position = end;
                remainingRange -= step;
                remainingLifetime -= deltaTime;
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
            var returnToPool = release;
            release = null;
            onImpact = null;
            trace.Clear();
            guidance?.Clear(); usesGuidance=false;
            if (returnToPool != null) returnToPool(this);
            else Destroy(gameObject);
        }

        private void OnDisable() => Despawn();
    }
}
