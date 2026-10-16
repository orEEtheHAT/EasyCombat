using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Delivery/Beam",fileName="Beam")]
    public sealed class BeamDefinition:ScriptableObject
    {
        public AttackDefinition Attack;
        [Min(.001f)] public float Duration=2;
        [Min(.01f)] public float Interval=.1f;
        public bool PulseImmediately=true;
        [Tooltip("Scale numeric/formula damage by Interval. Status chance still runs per pulse.")] public bool DamagePerSecond;
        public bool RerollEachPulse;
        public bool InterruptWhenCastBlocked=true;
        public ResourceCost[] PulseCosts=Array.Empty<ResourceCost>();
        public bool IsValid
        {
            get
            {
                if(Attack==null || !Attack.IsValid || Attack.Delivery!=DamageDelivery.Hitscan || !CombatMath.Positive(Duration)
                    || !CombatMath.Finite(Interval) || Interval<.01f || PulseCosts==null || PulseCosts.Length>32) return false;
                foreach(var cost in PulseCosts) if(!cost.IsValid) return false;
                return true;
            }
        }
    }
}
