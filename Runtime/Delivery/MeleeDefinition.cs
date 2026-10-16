using System;
using UnityEngine;

namespace EasyCombat
{
    public enum MeleeRepeatPolicy { OncePerSwing, OncePerWindow, Interval }
    [Serializable]
    public struct MeleeWindow
    {
        [Min(0)] public float Start;
        [Min(.001f)] public float End;
        public MeleeWindow(float start,float end) { Start=start; End=end; }
    }
    [CreateAssetMenu(menuName="EasyCombat/Delivery/Melee",fileName="Melee")]
    public sealed class MeleeDefinition:ScriptableObject
    {
        [Tooltip("Damage/formula/status payload only; its delivery and explosion geometry are not used.")] public AttackDefinition Attack;
        [Min(.001f)] public float Duration=.6f;
        public MeleeWindow[] Windows={new MeleeWindow(.15f,.4f)};
        [Min(0)] public float Radius=.3f;
        public LayerMask HitMask=~0;
        public QueryTriggerInteraction Triggers=QueryTriggerInteraction.Ignore;
        public MeleeRepeatPolicy Repeat;
        [Min(.01f)] public float RepeatInterval=.2f;
        [Range(1,4096)] public int TargetMemory=512;
        public bool StopSweepAtWorld=true;
        public bool InterruptWhenAttackBlocked=true;
        public bool IsValid
        {
            get
            {
                if(Attack==null || !Attack.IsValid || !CombatMath.Positive(Duration) || !CombatMath.Finite(Radius) || Radius<0
                    || Repeat<MeleeRepeatPolicy.OncePerSwing || Repeat>MeleeRepeatPolicy.Interval || !CombatMath.Finite(RepeatInterval) || RepeatInterval<.01f
                    || TargetMemory<1 || TargetMemory>4096 || Windows==null || Windows.Length==0 || Windows.Length>8) return false;
                var previous=0f;
                foreach(var window in Windows)
                { if(!CombatMath.Finite(window.Start) || !CombatMath.Finite(window.End) || window.Start<previous || window.End<=window.Start || window.End>Duration) return false; previous=window.End; }
                return true;
            }
        }
    }
}
