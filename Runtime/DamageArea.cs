using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Damage Area")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageArea : MonoBehaviour,ICombatTransient
    {
        [SerializeField] private AreaPulseDefinition definition;
        [SerializeField] private GameObject source;
        [SerializeField] private bool playOnEnable;
        private readonly AreaDamageQuery query = new AreaDamageQuery();
        private DamageInfo payload;
        private AreaSettings area;
        private Transform owner;
        private double elapsed, nextTick, duration, interval;
        private bool advancing, finishing;
        private Action<DamageArea> release;
        internal bool PoolManaged;
        internal long OverlapLeaseId;
        public bool UsesSharedClock { get; private set; }
        public bool IsRunning { get; private set; }
        public float Radius => area.Radius;
        public float Remaining => (float)Math.Max(0, duration - elapsed);
        public float NormalizedAge => duration > 0 ? Mathf.Clamp01((float)(elapsed / duration)) : 1;
        public event Action<DamageArea> Began;
        public event Action<DamageArea> Ended;
        public event Action<DamageImpact> Impacted;
        private Action<DamageImpact> callback;
        private void OnEnable() { if (playOnEnable && !PoolManaged) Begin(definition, source); }
        private void OnDisable() => Stop();
        public bool Begin(AreaPulseDefinition recipe, GameObject instigator = null)
            => Begin(new AreaPulseSnapshot(recipe, instigator));

        public bool Begin(in AreaPulseSnapshot snapshot, Action<DamageArea> returnToPool = null)
        {
            if (!snapshot.IsValid || !isActiveAndEnabled || advancing || finishing || release != null) return false;
            owner = snapshot.Payload.Source != null ? snapshot.Payload.Source.transform : null;
            area = snapshot.Area;
            // A persistent field snapshots its payload once, and applies it once per target per pulse.
            payload = snapshot.Payload;
            elapsed = 0; duration = snapshot.Duration; interval = snapshot.Interval;
            nextTick = snapshot.ApplyImmediately ? 0 : interval;
            release = returnToPool;
            callback ??= ReportImpact;
            IsRunning = true;
            UsesSharedClock = snapshot.OverlapGroup != null && snapshot.OverlapMode != AreaOverlapMode.Stack;
            try
            {
                if (UsesSharedClock && !AreaOverlapScheduler.Register(this, snapshot)) { Stop(); return false; }
                Began?.Invoke(this);
                if (!UsesSharedClock && snapshot.ApplyImmediately) Advance(0);
            }
            catch { Stop(); throw; }
            return true;
        }
        private void ReportImpact(DamageImpact impact) => Impacted?.Invoke(impact);
        private void Update() { if (!UsesSharedClock && Time.deltaTime > 0) Advance(Time.deltaTime); }
        public void Advance(float deltaTime)
        {
            if (UsesSharedClock && IsRunning) throw new InvalidOperationException("Advance AreaOverlapScheduler once for all exclusive areas instead of advancing each area.");
            if (!IsRunning || advancing || !CombatMath.Finite(deltaTime) || deltaTime < 0) return;
            advancing = true;
            try
            {
                elapsed += deltaTime;
                var limit = Math.Min(elapsed, duration);
                var ticks = 0;
                while (IsRunning && nextTick <= limit + 1e-7 && ticks++ < 8)
                {
                    nextTick += interval;
                    query.Apply(transform.position, transform.forward, payload, area, owner, callback);
                }
                if (elapsed >= duration && nextTick > duration + 1e-7) Stop();
            }
            catch { Stop(); throw; }
            finally { advancing = false; if (!IsRunning) Finish(); }
        }
        internal int SelectOverlapCandidates() => query.Select(transform.position, transform.forward, payload, area, owner);
        internal AreaCandidate GetOverlapCandidate(int index) => query.GetCandidate(index);
        internal bool AllowsOverlapCandidate(in AreaCandidate candidate)
        {
            var health=CombatTargetGroups.Rules(candidate.Receiver);
            return health==null || health.HitRules==null || health.HitRules.IsValid && health.HitRules.CheckRelationship(health,payload)==DamageRejection.None;
        }
        internal void ClearOverlapSelection() => query.ClearSelection();
        internal void SetOverlapAge(double age) => elapsed = age;
        internal void ApplyOverlapCandidate(in AreaCandidate candidate, long lease)
        {
            if (!IsRunning || !UsesSharedClock || advancing || OverlapLeaseId != lease) return;
            advancing = true;
            try { AreaDamageQuery.ApplyCandidate(candidate, transform.position, transform.forward, payload, area, callback); }
            catch { Stop(); throw; }
            finally { advancing = false; if (!IsRunning) Finish(); }
        }
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            if (!advancing) Finish();
        }
        private void Finish()
        {
            if (finishing) return;
            finishing = true;
            var returnToPool = release; release = null;
            payload = default; owner = null;
            try { if(!CombatSaveSystem.IsRestoring) Ended?.Invoke(this); }
            finally
            {
                try { returnToPool?.Invoke(this); }
                finally { finishing = false; }
            }
        }
        public void CancelForRestore()=>Stop();
    }
}
