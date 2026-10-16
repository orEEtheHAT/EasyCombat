using ECM2;
using UnityEngine;

namespace EasyCombat.Integrations
{
    [RequireComponent(typeof(Character), typeof(StatusController))]
    [AddComponentMenu("EasyCombat/Integrations/ECM2 Status Modifiers")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Integrations", "GamesFactory.Combat.ECM2")]
    public sealed class CharacterStatusModifiers : MonoBehaviour
    {
        public CombatStatDefinition MovementSpeedStat;
        private Character character;
        private StatusController statuses;
        private float baseWalkSpeed;
        private void Awake()
        {
            character = GetComponent<Character>();
            statuses = GetComponent<StatusController>();
            baseWalkSpeed = character.maxWalkSpeed;
        }
        private void OnEnable() { statuses.Changed += Refresh; Refresh(); }
        private void OnDisable()
        {
            if (statuses != null) statuses.Changed -= Refresh;
            if (character != null) character.maxWalkSpeed = baseWalkSpeed;
        }
        public void SetBaseWalkSpeed(float speed) { baseWalkSpeed = Mathf.Max(0f, speed); Refresh(); }
        private void Refresh() => character.maxWalkSpeed = baseWalkSpeed * statuses.GetStatMultiplier(MovementSpeedStat);
    }
}
