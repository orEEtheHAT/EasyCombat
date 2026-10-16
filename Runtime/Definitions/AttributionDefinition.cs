using UnityEngine;

namespace EasyCombat
{
    public enum KillCreditMode { LastHit, HighestDamage }
    [CreateAssetMenu(menuName="EasyCombat/Life/Kill Attribution",fileName="KillAttribution")]
    public sealed class AttributionDefinition : ScriptableObject
    {
        public KillCreditMode Killer;
        [Min(.001f)] public float ContributionWindow=15;
        [Range(0,1)] public float MinimumAssistFraction=.05f;
        public bool IncludeShieldDamage;
        [Range(1,128)] public int MaximumContributors=32;
        public bool IsValid => Killer>=KillCreditMode.LastHit && Killer<=KillCreditMode.HighestDamage
            && CombatMath.Positive(ContributionWindow) && CombatMath.Finite(MinimumAssistFraction) && MinimumAssistFraction>=0 && MinimumAssistFraction<=1
            && MaximumContributors>=1 && MaximumContributors<=128;
    }
}
