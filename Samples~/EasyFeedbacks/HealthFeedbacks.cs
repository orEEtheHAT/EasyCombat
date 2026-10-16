using EasyFeedbacks;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("EasyCombat/Integrations/Health Feedbacks")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Integrations", "GamesFactory.Combat.EasyFeedbacks")]
    public sealed class HealthFeedbacks : MonoBehaviour
    {
        public FeedbackPreset Damage;
        public FeedbackPreset Death;
        public FeedbackPreset Healing;
        private Health health;

        private void OnEnable()
        {
            health = GetComponent<Health>();
            health.Damaged += OnDamage;
            health.Changed += OnChange;
        }
        private void Start()
        {
            if (Damage != null) FeedbackService.Prewarm(Damage);
            if (Death != null) FeedbackService.Prewarm(Death);
            if (Healing != null) FeedbackService.Prewarm(Healing);
        }
        private void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamage;
            health.Changed -= OnChange;
        }
        private void OnDamage(DamageResult result)
        {
            var preset = result.Killed && Death != null ? Death : Damage;
            if (preset != null)
                FeedbackService.Play(preset, new FeedbackContext(result.Damage.Point, Quaternion.identity, transform));
        }
        private void OnChange(HealthChange change)
        {
            if (change.Reason == HealthChangeReason.Healing && Healing != null)
                FeedbackService.Play(Healing, transform);
        }
    }
}
