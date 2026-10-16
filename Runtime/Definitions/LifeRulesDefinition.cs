using UnityEngine;

namespace EasyCombat
{
    public enum CombatLifeState { Alive, Downed, Dead }
    [CreateAssetMenu(menuName="EasyCombat/Life/Rules",fileName="LifeRules")]
    public sealed class LifeRulesDefinition : ScriptableObject
    {
        [Min(0)] public float MinimumHealth;
        public bool AllowDowned;
        public bool FinishDownedOnDamage=true;
        public bool PeriodicCanFinish;
        public CombatRestriction DownedRestrictions=CombatRestriction.Move|CombatRestriction.Attack|CombatRestriction.Cast|CombatRestriction.Jump;
        [Range(.001f,1)] public float ReviveHealthFraction=.25f;
        [Min(0)] public float ReviveInvulnerability=1;
        public bool ClearStatusesOnRevive;
        public bool IsValid => CombatMath.Finite(MinimumHealth) && MinimumHealth>=0 && CombatMath.Finite(ReviveHealthFraction)
            && ReviveHealthFraction>0 && ReviveHealthFraction<=1 && CombatMath.Finite(ReviveInvulnerability) && ReviveInvulnerability>=0
            && (DownedRestrictions & ~CombatRestriction.All)==0;
    }
}
