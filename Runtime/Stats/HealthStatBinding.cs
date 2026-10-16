using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health),typeof(CombatStats))]
    [AddComponentMenu("EasyCombat/Stats/Health Stat Binding")]
    public sealed class HealthStatBinding : MonoBehaviour
    {
        public CombatStatDefinition MaximumHealthStat;
        [Tooltip("Off: gaining maximum HP does not heal. On: preserve the existing HP ratio.")]
        public bool PreserveRatio;
        private Health health;
        private CombatStats stats;
        private void Awake() { health = GetComponent<Health>(); stats = GetComponent<CombatStats>(); }
        private void LateUpdate() => Synchronize();
        public bool Synchronize()
        {
            if (!isActiveAndEnabled) return false;
            if (health == null) health = GetComponent<Health>();
            if (stats == null) stats = GetComponent<CombatStats>();
            if (!health.isActiveAndEnabled || !stats.TryGetValue(MaximumHealthStat,out var maximum) || !CombatMath.Positive(maximum)) return false;
            if (health.Maximum != maximum) health.SetMaximum(maximum,PreserveRatio);
            return true;
        }
    }
}
