using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("EasyCombat/Projectile Pool")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class ProjectilePool : MonoBehaviour
    {
        [SerializeField] private DamageProjectile prefab;
        [SerializeField, Min(1)] private int maximumActive = 128;
        [SerializeField, Min(0)] private int prewarmCount = 8;
        private readonly Dictionary<DamageProjectile, Stack<DamageProjectile>> available = new Dictionary<DamageProjectile, Stack<DamageProjectile>>();
        private readonly Dictionary<DamageProjectile, DamageProjectile> origins = new Dictionary<DamageProjectile, DamageProjectile>();
        private readonly HashSet<DamageProjectile> active = new HashSet<DamageProjectile>();
        private readonly List<DamageProjectile> all = new List<DamageProjectile>();
        private bool shuttingDown;
        private Action<DamageProjectile> returnCallback;
        public int ActiveCount => active.Count;
        public int RemainingCapacity => Mathf.Max(0,maximumActive-active.Count);
        public bool CanSpawn(AttackDefinition attack,int count=1) => !shuttingDown && isActiveAndEnabled && attack!=null && attack.IsValid
            && (attack.ProjectilePrefab!=null || prefab!=null) && count>0 && count<=RemainingCapacity;
        public DamageProjectile Prefab { get => prefab; set => prefab = value; }
        private void Start() { if (prefab != null) Prewarm(prefab, prewarmCount); }

        public void Prewarm(DamageProjectile template, int count)
        {
            if (template == null || shuttingDown) return;
            var stack = StackFor(template);
            count = Mathf.Clamp(count, 0, maximumActive);
            while (stack.Count < count) stack.Push(Create(template));
        }
        private Stack<DamageProjectile> StackFor(DamageProjectile template)
        {
            if (!available.TryGetValue(template, out var stack))
            {
                stack = new Stack<DamageProjectile>(Mathf.Max(prewarmCount, 8));
                available.Add(template, stack);
            }
            return stack;
        }
        private DamageProjectile Create(DamageProjectile template)
        {
            var projectile = Instantiate(template, transform);
            projectile.gameObject.SetActive(false);
            all.Add(projectile);
            origins.Add(projectile, template);
            return projectile;
        }

        public DamageProjectile Spawn(Vector3 origin, Vector3 direction, DamageInfo damage, Transform owner,
            AttackDefinition attack, Action<DamageImpact> onImpact, DamageAreaPool areaPool = null)
        {
            var template = attack != null && attack.ProjectilePrefab != null ? attack.ProjectilePrefab : prefab;
            if (shuttingDown || !isActiveAndEnabled || template == null || active.Count >= maximumActive) return null;
            var stack = StackFor(template);
            DamageProjectile projectile = null;
            while (stack.Count > 0 && projectile == null) projectile = stack.Pop();
            if (projectile == null) projectile = Create(template);
            active.Add(projectile);
            if (returnCallback == null) returnCallback = Return;
            try { projectile.Launch(origin, direction, damage, owner, attack, onImpact, returnCallback, areaPool); }
            catch { Return(projectile); throw; }
            return projectile;
        }

        private void Return(DamageProjectile projectile)
        {
            if (!active.Remove(projectile) || projectile == null) return;
            projectile.gameObject.SetActive(false);
            if (!shuttingDown && origins.TryGetValue(projectile, out var template)) StackFor(template).Push(projectile);
        }

        public void ClearActive()
        {
            // Despawn mutates the active set. The full instance list remains stable.
            for (var i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].IsFlying) all[i].Despawn();
        }

        private void OnDisable() => ClearActive();
        private void OnDestroy() { shuttingDown = true; ClearActive(); }
    }
}
