using System;
using UnityEngine;

namespace EasyCombat
{
    public enum AbilityTargetMode { Direction, Point, Unit, Self }
    [CreateAssetMenu(menuName="EasyCombat/Abilities/Ability",fileName="Ability")]
    public sealed class AbilityDefinition:ScriptableObject
    {
        public string DisplayName;
        public AbilityTargetMode TargetMode;
        [Min(.001f)] public float Range=30;
        public AreaTargetFilter UnitTargets;
        public bool AllowSelf;
        public bool AllowDownedTargets;
        public bool AllowDeadTargets;
        public bool FollowTargetDuringCast=true;
        public bool RequireLineOfSight;
        public LayerMask ObstacleMask=~0;
        [Min(0)] public float CastTime;
        [Min(0)] public float Cooldown=.25f;
        [Min(0)] public float GlobalCooldown;
        [Range(0,32),Tooltip("Zero means unlimited charges; cooldown still applies.")] public int MaximumCharges=1;
        [Min(.001f)] public float ChargeRecovery=1;
        public bool CanCancel=true;
        public bool InterruptWhenCastBlocked=true;
        [Tooltip("Committed together at release, after targeting and action validation. Cancelled windups are free.")]
        public ResourceCost[] Costs=Array.Empty<ResourceCost>();
        public CombatAbilityAction[] Actions=Array.Empty<CombatAbilityAction>();
        public bool IsValid
        {
            get
            {
                if(TargetMode<AbilityTargetMode.Direction || TargetMode>AbilityTargetMode.Self || !CombatMath.Positive(Range)
                    || UnitTargets<AreaTargetFilter.All || UnitTargets>AreaTargetFilter.Allies || !Nonnegative(CastTime) || !Nonnegative(Cooldown)
                    || !Nonnegative(GlobalCooldown) || MaximumCharges<0 || MaximumCharges>32 || !CombatMath.Positive(ChargeRecovery)
                    || Costs==null || Costs.Length>32 || Actions==null || Actions.Length==0 || Actions.Length>8) return false;
                foreach(var cost in Costs) if(!cost.IsValid) return false;
                foreach(var action in Actions) if(action==null || !action.IsValid) return false;
                return true;
            }
        }
        private static bool Nonnegative(float value)=>CombatMath.Finite(value) && value>=0;
    }
}
