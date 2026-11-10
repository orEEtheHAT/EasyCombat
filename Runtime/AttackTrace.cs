using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>Shared contact policy for instantaneous rays and travelling projectiles. One instance per active shot.</summary>
    internal sealed class AttackTrace
    {
        // Selection must remain private until all contact callbacks finish (a callback may shoot again).
        // Scratch capacity is proportional to nested queries rather than to the number of flying objects.
        private const int MaxCachedQueries=8, ProbeCooldownSteps=4;
        private static readonly Stack<DamageQuery> scratch=new Stack<DamageQuery>(MaxCachedQueries);
        private static readonly Collider[] probe=new Collider[1];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetScratch() { scratch.Clear(); probe[0]=null; }
        private int exactSteps, visitedCount;
        private CombatHitKey first, second, third, fourth;
        private HashSet<CombatHitKey> overflow;
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
        internal Vector3 Direction=>direction;
        internal QueryParameters BatchFilter => new QueryParameters(mask, false, triggers);
        internal float ProbeRadius
        {
            get
            {
                float extent = Mathf.Max(.001f, Radius);
                return extent + Mathf.Max(.001f, extent * .00001f);
            }
        }
        internal DamageInfo Payload=>damage;
        internal bool TargetBudgetExhausted=>maximumTargets>0 && visitedCount>=maximumTargets;
        private bool InFirstFour(in CombatHitKey key)=>(visitedCount>0 && first.Equals(key)) || (visitedCount>1 && second.Equals(key))
            || (visitedCount>2 && third.Equals(key)) || (visitedCount>3 && fourth.Equals(key));
        internal bool HasVisited(MonoBehaviour receiver)
        { var key=new CombatHitKey(receiver); return InFirstFour(in key) || (visitedCount>4 && overflow.Contains(key)); }
        private bool Visit(in CombatHitKey key)
        {
            if(InFirstFour(in key)) return false;
            switch(visitedCount)
            {
                case 0:first=key; break;
                case 1:second=key; break;
                case 2:third=key; break;
                case 3:fourth=key; break;
                default: if(!(overflow??=new HashSet<CombatHitKey>()).Add(key)) return false; break;
            }
            visitedCount++; return true;
        }
        internal void SetDirection(Vector3 value)=>direction=value.normalized;
        internal void ClearVisited() { first=second=third=fourth=default; visitedCount=0; overflow?.Clear(); }
        internal void Continue(Vector3 value,float multiplier=1) { direction=value.normalized; damage=damage.Scale(multiplier); Active=damage.IsValid; }

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
            Active = true;
        }

        internal void ResetDistantContactHint(Transform previousPose, Vector3 launchPosition)
        {
            if (exactSteps > 0 && previousPose != null &&
                (previousPose.position - launchPosition).sqrMagnitude > Mathf.Max(1f, 4 * Radius * Radius))
                exactSteps = 0;
        }

        private bool EmptyShortStep(Vector3 origin,Vector3 end,float distance)
        {
            // The padded capsule encloses the entire swept segment AND its initial overlap.
            // A false positive falls back to exact queries. Never remember empty *space* across steps.
            // Avoid an extra query on repeated contacts (including when a pooled shot is relaunched).
            if(exactSteps>0) { exactSteps--; return false; }
            if(distance<=0 || distance>Mathf.Max(2f,4*Radius)) return false;
            float extent=ProbeRadius;
            if(!CombatMath.Finite(extent) || !CombatMath.Finite(end)) return false;
            DamageQuery.CountTravelQuery();
            int count=CombatPhysics.Scene.OverlapCapsule(origin,end,extent,probe,mask,triggers);
            probe[0]=null;
            if(count==0) return true;
            exactSteps=ProbeCooldownSteps; return false;
        }

        /// <summary>Collect before callbacks, resolve in travel order, and remember final receivers across steps.</summary>
        public void Advance(Vector3 origin, float distance, Action<DamageImpact> onImpact, out Vector3 end, Transform movingObject = null,Action<DamageContact> onContact=null,Vector3? displacement=null)
        {
            end = origin + (displacement ?? direction * distance);
            if (!Active || (movingObject!=null && EmptyShortStep(origin,end,distance))) return;
            var query=scratch.Count!=0 ? scratch.Pop() : new DamageQuery();
            try
            {
                var count = query.Collect(origin, direction, distance, Radius, mask, triggers, owner);
                if(count>0) exactSteps=ProbeCooldownSteps;
                for (var i = 0; i < count && Active; i++)
                {
                    var contact = query.GetContact(i);
                    if (contact.Collider == null || !contact.Collider.enabled || !contact.Collider.gameObject.activeInHierarchy) continue;
                    // A receiver destroyed by an earlier callback must not become a new wall.
                    if (contact.WasTarget && contact.Receiver == null) continue;
                    if (!contact.IsCurrent) continue;
                    if (contact.WasTarget && !Visit(in contact.HitKey)) continue;
                    contactCount++;
                    var stop = !piercing || !contact.WasTarget || TargetBudgetExhausted;
                    if (movingObject != null) movingObject.position = origin + direction * contact.Distance;
                    // Commit before callbacks: rejected damage still consumes a physical contact.
                    if (stop) { Active = false; end = origin + direction * contact.Distance; }
                    if (area.Enabled)
                        AreaDamageQuery.ApplyTransient(contact.Point + contact.Normal * .001f, direction, damage, area, owner, onImpact);
                    else
                    {
                        var impact = contact.Apply(damage.At(contact.Point, direction));
                        onImpact?.Invoke(impact);
                    }
                    if (spawnedArea.Enabled && areaPool != null && (stop || spawnedArea.Trigger == AreaSpawnTrigger.EveryContact))
                        areaPool.Spawn(spawnedArea, contact.Point, contact.Normal, direction, owner);
                    onContact?.Invoke(contact);
                    // External cancellation from an impact callback also ends at this contact.
                    if (!Active) end = origin + direction * contact.Distance;
                }
            }
            catch { Active = false; throw; }
            finally { query.ClearContacts(); if(scratch.Count<MaxCachedQueries) scratch.Push(query); }
        }

        public void CompleteHitscan(Vector3 end, Action<DamageImpact> onImpact)
        {
            Active = false;
            // Preserve legacy miss explosions, but never add a phantom blast after piercing real targets.
            if (area.Enabled && contactCount == 0)
            {
                AreaDamageQuery.ApplyTransient(end, direction, damage, area, owner, onImpact);
                if (spawnedArea.Enabled && areaPool != null) areaPool.Spawn(spawnedArea, end, Vector3.up, direction, owner);
            }
        }

        public void Stop() => Active = false;
        public void Clear()
        {
            Active = false;
            ClearVisited();
            damage = default; area = default; owner = null; contactCount = 0;
            spawnedArea = default; areaPool = null;
        }
    }
}
