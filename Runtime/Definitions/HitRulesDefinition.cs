using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Hit Rules", fileName = "HitRules")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class HitRulesDefinition : ScriptableObject
    {
        public bool AllowSelf = true;
        public bool AllowAllies = true;
        public bool AllowEnemies = true;
        public bool AllowNeutralActors = true;
        public bool AllowEnvironment = true;
        public bool Invulnerable;
        [Range(0, 1)] public float DodgeChance;
        public bool DodgePeriodic;
        [Min(0)] public float InvulnerabilityAfterHit;
        public bool HitInvulnerabilityAffectsPeriodic;
        public bool IsValid => CombatMath.Finite(DodgeChance) && DodgeChance >= 0 && DodgeChance <= 1
            && CombatMath.Finite(InvulnerabilityAfterHit) && InvulnerabilityAfterHit >= 0;

        internal DamageRejection CheckRelationship(Health target, in DamageInfo damage)
        {
            var actor = damage.Origin.Actor;
            if (actor.Id == 0)
            {
                if (!AllowEnvironment) return DamageRejection.Environment;
            }
            else if (actor.Id == target.Identity.Id)
            {
                if (!AllowSelf) return DamageRejection.Self;
            }
            else
            {
                var team = target.GetComponentInParent<CombatTeam>();
                if (!damage.Origin.HasTeam || team == null)
                { if (!AllowNeutralActors) return DamageRejection.Neutral; }
                else if (damage.Origin.Team == team.Team)
                { if (!AllowAllies) return DamageRejection.Ally; }
                else if (!AllowEnemies) return DamageRejection.Enemy;
            }
            return DamageRejection.None;
        }
        internal DamageRejection Check(Health target, in DamageInfo damage)
        {
            var relationship = CheckRelationship(target, damage);
            if (relationship != DamageRejection.None) return relationship;
            if (Invulnerable) return DamageRejection.Invulnerable;
            if ((!damage.IsPeriodic || DodgePeriodic) && DodgeChance > 0
                && (DodgeChance >= 1 || CombatRandom.Value < DodgeChance)) return DamageRejection.Dodged;
            return DamageRejection.None;
        }
    }
}
