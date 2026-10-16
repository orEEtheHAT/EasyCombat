using UnityEngine;

namespace EasyCombat
{
    /// <summary>Optional presentation adapter. The area radius controls the footprint; pooled particles restart cleanly.</summary>
    [RequireComponent(typeof(DamageArea)), AddComponentMenu("EasyCombat/Damage Area Visual")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DamageAreaVisual : MonoBehaviour
    {
        public Transform RadiusRoot;
        [Min(.001f)] public float ReferenceRadius = 1;
        public ParticleSystem[] Particles;
        private DamageArea area;
        private Vector3 originalScale;
        private bool initialized;
        private void Awake()
        {
            area = GetComponent<DamageArea>();
            if (RadiusRoot != null) originalScale = RadiusRoot.localScale;
            initialized = true;
        }
        private void OnEnable()
        {
            if (!initialized) Awake();
            area.Began += Begin;
            area.Ended += End;
            if (area.IsRunning) Begin(area);
        }
        private void OnDisable()
        {
            if (area != null) { area.Began -= Begin; area.Ended -= End; }
            End(area);
        }
        private void Begin(DamageArea field)
        {
            if (RadiusRoot != null && ReferenceRadius > 0) RadiusRoot.localScale = originalScale * (field.Radius / ReferenceRadius);
            if (Particles == null) return;
            foreach (var particles in Particles)
            {
                if (particles == null) continue;
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(true);
            }
        }
        private void End(DamageArea _)
        {
            if (Particles == null) return;
            foreach (var particles in Particles)
                if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
