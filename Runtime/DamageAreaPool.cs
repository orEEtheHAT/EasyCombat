using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("EasyCombat/Damage Area Pool")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageAreaPool : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumActive = 64;
        private readonly Dictionary<DamageArea, Stack<DamageArea>> available = new Dictionary<DamageArea, Stack<DamageArea>>();
        private readonly Dictionary<DamageArea, DamageArea> templates = new Dictionary<DamageArea, DamageArea>();
        private readonly HashSet<DamageArea> active = new HashSet<DamageArea>();
        private readonly List<DamageArea> all = new List<DamageArea>();
        private RaycastHit[] groundHits = new RaycastHit[16];
        private Transform storage;
        private Action<DamageArea> returnCallback;
        private bool shuttingDown;
        public int ActiveCount => active.Count;
        public int SkippedSpawns { get; private set; }
        public int MaximumActive { get => maximumActive; set => maximumActive = Mathf.Max(1, value); }

        public void Prewarm(DamageArea template, int count)
        {
            if (template == null || shuttingDown) return;
            var stack = StackFor(template);
            count = Mathf.Clamp(count, 0, maximumActive);
            while (stack.Count < count) stack.Push(Create(template));
        }
        private Stack<DamageArea> StackFor(DamageArea template)
        {
            if (!available.TryGetValue(template, out var stack))
            { stack = new Stack<DamageArea>(8); available.Add(template, stack); }
            return stack;
        }
        private DamageArea Create(DamageArea template)
        {
            if (storage == null)
            {
                var go = new GameObject("Inactive areas"); go.SetActive(false);
                storage = go.transform; storage.SetParent(transform, false);
            }
            // An inactive parent prevents prefab playOnEnable from damaging anything before placement.
            var field = Instantiate(template, storage);
            field.gameObject.SetActive(false); field.PoolManaged = true;
            all.Add(field); templates.Add(field, template); return field;
        }

        public DamageArea Spawn(in AreaSpawnSnapshot settings, Vector3 contact, Vector3 normal, Vector3 forward, Transform owner = null)
        {
            if (shuttingDown || !isActiveAndEnabled || !settings.Enabled || settings.Prefab == null) return null;
            if (active.Count >= maximumActive || !TryPlace(settings, contact, normal, forward, owner, out var position, out var rotation))
            { SkippedSpawns++; return null; }
            var stack = StackFor(settings.Prefab);
            DamageArea field = null;
            while (stack.Count > 0 && field == null) field = stack.Pop();
            if (field == null) field = Create(settings.Prefab);
            active.Add(field);
            field.transform.SetParent(transform, false);
            field.transform.SetPositionAndRotation(position, rotation);
            field.gameObject.SetActive(true);
            returnCallback ??= Return;
            try
            {
                if (!field.Begin(settings.Pulses, returnCallback)) { Return(field); return null; }
                return field;
            }
            catch { field.Stop(); Return(field); throw; }
        }

        private bool TryPlace(in AreaSpawnSnapshot settings, Vector3 point, Vector3 normal, Vector3 forward, Transform owner,
            out Vector3 position, out Quaternion rotation)
        {
            position = point; rotation = Quaternion.identity;
            if (!CombatMath.Finite(point) || !CombatMath.Finite(normal) || !CombatMath.Finite(forward)) return false;
            var up = normal.sqrMagnitude > .000001f ? normal.normalized : Vector3.up;
            if (settings.Placement == AreaSpawnPlacement.GroundBelow)
            {
                var start = point + up * settings.SurfaceOffset + Vector3.up * settings.ProbeHeight;
                int count;
                while (true)
                {
                    count = CombatPhysics.Scene.Raycast(start, Vector3.down, groundHits, settings.ProbeHeight + settings.GroundDistance,
                        settings.GroundMask, QueryTriggerInteraction.Ignore);
                    if (count < groundHits.Length) break;
                    Array.Resize(ref groundHits, groundHits.Length * 2);
                }
                var best = -1;
                for (var i = 0; i < count; i++)
                {
                    var hit = groundHits[i]; var collider = hit.collider;
                    if (collider == null || Vector3.Dot(hit.normal, Vector3.up) < settings.MinimumUpDot
                        || (owner != null && (collider.transform == owner || collider.transform.IsChildOf(owner)))
                        || collider.GetComponentInParent<Hurtbox>() != null || collider.GetComponentInParent<IDamageable>() != null) continue;
                    if (best < 0 || hit.distance < groundHits[best].distance
                        || (hit.distance == groundHits[best].distance && collider.GetInstanceID() < groundHits[best].collider.GetInstanceID())) best = i;
                }
                if (best < 0) return false;
                point = groundHits[best].point; up = groundHits[best].normal;
            }
            position = point + up * settings.SurfaceOffset;
            var tangent = Vector3.ProjectOnPlane(forward, up);
            if (tangent.sqrMagnitude < .000001f) tangent = Vector3.Cross(up, Vector3.right);
            if (tangent.sqrMagnitude < .000001f) tangent = Vector3.Cross(up, Vector3.forward);
            rotation = Quaternion.LookRotation(tangent.normalized, up);
            return true;
        }
        private void Return(DamageArea field)
        {
            if (!active.Remove(field) || field == null) return;
            field.gameObject.SetActive(false);
            if (!shuttingDown && templates.TryGetValue(field, out var template))
                // Keep returned instances under this root. Reparenting during a parent's OnDisable is forbidden by Unity.
                StackFor(template).Push(field);
        }
        public void ClearActive()
        {
            for (var i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsRunning) all[i].Stop();
        }
        private void OnDisable() => ClearActive();
        private void OnDestroy() { shuttingDown = true; ClearActive(); }
    }
}
