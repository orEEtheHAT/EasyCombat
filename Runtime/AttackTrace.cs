using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>Shared contact policy for instantaneous rays and travelling projectiles. One instance per active shot.</summary>
    internal sealed class AttackTrace
    {
        private readonly DamageQuery query = new DamageQuery();
        private readonly HashSet<CombatHitKey> visited = new HashSet<CombatHitKey>(16);
        private AreaDamageQuery areaQuery;
        private AreaSettings area;
        private AreaSpawnSnapshot spawnedArea;
        private DamageAreaPool areaPool;
        private DamageInfo damage;
        private Transform owner;
        private Vector3 direction;
        private LayerMask mask;
        private QueryTriggerInteraction triggers;
        private bool piercing;
        private int maximumTargets, contactCount;
        public bool Active { get; private set; }
        public float Radius { get; private set; }

        public void Begin(AttackDefinition attack, DamageInfo request, Transform source, Vector3 forward, DamageAreaPool fieldPool = null)
        {
            Clear();
            damage = request;
            owner = source;
            direction = forward.normalized;
            Radius = attack.HitRadius;
            mask = attack.HitMask;
            triggers = attack.Triggers;
            piercing = attack.ContactBehavior == ContactBehavior.PierceTargets;
            maximumTargets = attack.MaximumTargets;
            area = new AreaSettings(attack.Area);
            spawnedArea = new AreaSpawnSnapshot(attack.ImpactArea, request.Source, request.Origin);
            areaPool = fieldPool;
            if (area.Enabled && areaQuery == null) areaQuery = new AreaDamageQuery();
            Active = true;
        }

        /// <summary>Collect before callbacks, resolve in travel order, and remember final receivers across steps.</summary>
        public void Advance(Vector3 origin, float distance, Action<DamageImpact> onImpact, out Vector3 end, Transform movingObject = null)
        {
            end = origin + direction * distance;
            if (!Active) return;
            try
            {
                var count = query.Collect(origin, direction, distance, Radius, mask, triggers, owner);
                for (var i = 0; i < count && Active; i++)
                {
                    var contact = query.GetContact(i);
                    if (contact.Collider == null || !contact.Collider.enabled || !contact.Collider.gameObject.activeInHierarchy) continue;
                    // A receiver destroyed by an earlier callback must not become a new wall.
                    if (contact.WasTarget && contact.Receiver == null) continue;
                    if (!contact.IsCurrent) continue;
                    if (contact.WasTarget && !visited.Add(contact.HitKey)) continue;
                    contactCount++;
                    var stop = !piercing || !contact.WasTarget || (maximumTargets > 0 && visited.Count >= maximumTargets);
                    if (movingObject != null) movingObject.position = origin + direction * contact.Distance;
                    // Commit before callbacks: rejected damage still consumes a physical contact.
                    if (stop) { Active = false; end = origin + direction * contact.Distance; }
                    if (area.Enabled)
                        areaQuery.Apply(contact.Point + contact.Normal * .001f, direction, damage, area, owner, onImpact);
                    else
                    {
                        var impact = contact.Apply(damage.At(contact.Point, direction));
                        onImpact?.Invoke(impact);
                    }
                    if (spawnedArea.Enabled && areaPool != null && (stop || spawnedArea.Trigger == AreaSpawnTrigger.EveryContact))
                        areaPool.Spawn(spawnedArea, contact.Point, contact.Normal, direction, owner);
                    // External cancellation from an impact callback also ends at this contact.
                    if (!Active) end = origin + direction * contact.Distance;
                }
            }
            catch { Active = false; throw; }
            finally { query.ClearContacts(); }
        }

        public void CompleteHitscan(Vector3 end, Action<DamageImpact> onImpact)
        {
            Active = false;
            // Preserve legacy miss explosions, but never add a phantom blast after piercing real targets.
            if (area.Enabled && contactCount == 0)
            {
                areaQuery.Apply(end, direction, damage, area, owner, onImpact);
                if (spawnedArea.Enabled && areaPool != null) areaPool.Spawn(spawnedArea, end, Vector3.up, direction, owner);
            }
        }

        public void Stop() => Active = false;
        public void Clear()
        {
            Active = false;
            visited.Clear();
            query.ClearContacts();
            damage = default; area = default; owner = null; contactCount = 0;
            spawnedArea = default; areaPool = null;
        }
    }
}
