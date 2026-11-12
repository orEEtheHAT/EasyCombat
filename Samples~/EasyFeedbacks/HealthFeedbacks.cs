using EasyFeedbacks;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("EasyCombat/Integrations/Health Feedbacks")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Integrations", "GamesFactory.Combat.EasyFeedbacks")]
    public sealed class HealthFeedbacks : MonoBehaviour
    {
        [Tooltip("Optional player context provider, such as PlayerFeedbackContext. Must implement IFeedbackContextProvider.")]
        public MonoBehaviour ContextProvider;
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
                FeedbackService.Play(preset, Prepare(new FeedbackContext(result.Damage.Point, Quaternion.identity, transform)));
        }
        private FeedbackContext Prepare(FeedbackContext context)
            => ContextProvider != null && ContextProvider is IFeedbackContextProvider provider ? provider.Prepare(context) : context;

        private void OnValidate()
        {
            if (ContextProvider != null && !(ContextProvider is IFeedbackContextProvider))
                Debug.LogWarning("Health Feedbacks: Context Provider must implement IFeedbackContextProvider. Assign a player context adapter or leave it empty.", this);
        }

        private void OnChange(HealthChange change)
        {
            if (change.Reason == HealthChangeReason.Healing && Healing != null)
                FeedbackService.Play(Healing, Prepare(FeedbackContext.From(transform)));
        }
    }
}
