using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Composite/Buildup",fileName="Buildup")]
    public sealed class BuildupDefinition : ScriptableObject
    {
        public string DisplayName;
        [Min(.001f)] public float Threshold=100;
        [Min(0)] public float FlatPerHit;
        [Min(0)] public float PerHealthDamage=1;
        [Min(0)] public float PerShieldDamage;
        public bool FilterType;
        public DamageTypeDefinition DamageType;
        public CombatTagDefinition RequiredTag;
        public bool AllowPeriodic;
        public bool AllowReactionDamage;
        public bool CountBlockedContacts;
        public bool TriggerOnFatalHit;
        [Min(0)] public float DecayPerSecond=5;
        [Min(0)] public float DecayDelay=2;
        [Min(0)] public float TriggerCooldown;
        public bool KeepOverflow;
        public CombatReactionAction[] Actions=Array.Empty<CombatReactionAction>();
        public bool IsValid
        {
            get
            {
                if(!CombatMath.Positive(Threshold) || !Nonnegative(FlatPerHit) || !Nonnegative(PerHealthDamage) || !Nonnegative(PerShieldDamage)
                    || !Nonnegative(DecayPerSecond) || !Nonnegative(DecayDelay) || !Nonnegative(TriggerCooldown)
                    || Actions==null || Actions.Length==0 || Actions.Length>8) return false;
                foreach(var action in Actions) if(action==null || !action.IsValid) return false;
                return true;
            }
        }
        private static bool Nonnegative(float value)=>CombatMath.Finite(value) && value>=0;
    }
}
