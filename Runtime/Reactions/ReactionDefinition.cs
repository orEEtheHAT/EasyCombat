using System;
using UnityEngine;

namespace EasyCombat
{
    public enum ReactionTrigger { DamageTaken, DamageDealt, Kill, Death, ShieldAbsorbed }
    public enum ReactionTarget { Owner, Attacker, Victim }
    public enum ReactionCooldownScope { Actor, Target, RootAction }
    [CreateAssetMenu(menuName = "EasyCombat/Reactions/Reaction", fileName = "Reaction")]
    public sealed class ReactionDefinition : ScriptableObject
    {
        public ReactionTrigger Trigger;
        public ReactionTarget Target;
        [Tooltip("Attribute actions to the original attacker rather than this reaction's owner. Useful for buildup and linked body parts.")]
        public bool UseTriggerSource;
        [Range(0,1)] public float Chance = 1;
        [Min(0)] public float Cooldown;
        public ReactionCooldownScope CooldownScope;
        [Min(1), Tooltip("Maximum activations within one synchronous resolution, including its deferred reaction chain.")]
        public int MaximumProcsPerResolution = 1;
        public bool AllowPeriodic;
        public bool AllowReactionChains;
        public bool RequireCritical;
        [Min(0)] public float MinimumHealthDamage;
        [Range(0,1)] public float OwnerHealthBelow = 1;
        public DamageTypeDefinition DamageType;
        public CombatTagDefinition[] RequiredTags = Array.Empty<CombatTagDefinition>();
        public CombatTagDefinition[] ExcludedTags = Array.Empty<CombatTagDefinition>();
        public CombatReactionAction[] Actions = Array.Empty<CombatReactionAction>();
        public bool IsValid
        {
            get
            {
                if (Trigger < ReactionTrigger.DamageTaken || Trigger > ReactionTrigger.ShieldAbsorbed || Target < ReactionTarget.Owner || Target > ReactionTarget.Victim
                    || CooldownScope < ReactionCooldownScope.Actor || CooldownScope > ReactionCooldownScope.RootAction
                    || !CombatMath.Finite(Chance) || Chance < 0 || Chance > 1 || !CombatMath.Finite(Cooldown) || Cooldown < 0 || MaximumProcsPerResolution < 1
                    || !CombatMath.Finite(MinimumHealthDamage) || MinimumHealthDamage < 0 || !CombatMath.Finite(OwnerHealthBelow) || OwnerHealthBelow < 0 || OwnerHealthBelow > 1
                    || Actions == null || Actions.Length == 0 || Actions.Length > 8) return false;
                foreach (var action in Actions) if (action == null || !action.IsValid) return false;
                return true;
            }
        }
    }
}
