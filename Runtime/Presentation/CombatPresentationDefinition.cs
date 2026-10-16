using System;
using UnityEngine;

namespace EasyCombat
{
    public enum CombatPresentationKind { Damage,Shield,Blocked,Dodged,Healing,Death,Downed,StatusApplied,StatusTick,StatusRemoved,AbilityStarted,AbilityCommitted,AbilityCancelled,Shot,Impact,Custom,AreaStarted,AreaEnded }
    [Serializable]
    public struct CombatPresentationRule
    {
        public CombatPresentationKind Kind;
        public bool Enabled;
        [Min(0)] public float MergeWindow;
        [Min(0),Tooltip("Fixed one-second windows per event key. Zero means unlimited.")] public int MaximumPerSecond;
        public bool SeparateSources;
        public bool DiscardAfterRespawn;
        public CombatPresentationRule(CombatPresentationKind kind,float merge=0,int maximum=0)
        { Kind=kind; Enabled=true; MergeWindow=merge; MaximumPerSecond=maximum; SeparateSources=true; DiscardAfterRespawn=kind!=CombatPresentationKind.Death; }
    }
    [CreateAssetMenu(menuName="EasyCombat/Presentation/Output Rules",fileName="CombatPresentation")]
    public sealed class CombatPresentationDefinition:ScriptableObject
    {
        public const int KindCount=(int)CombatPresentationKind.AreaEnded+1;
        [Range(1,16384)] public int Capacity=256;
        [Range(1,16384)] public int RateKeyCapacity=512;
        [Range(1,1024)] public int MaximumPerFrame=64;
        [Min(0)] public int GlobalMaximumPerSecond=256;
        public CombatPresentationRule[] Rules={ new CombatPresentationRule(CombatPresentationKind.Damage,.05f,20),new CombatPresentationRule(CombatPresentationKind.Shield,.05f,20),new CombatPresentationRule(CombatPresentationKind.StatusTick,.1f,10) };
        public bool IsValid
        {
            get
            {
                if(Capacity<1 || Capacity>16384 || RateKeyCapacity<1 || RateKeyCapacity>16384 || MaximumPerFrame<1 || MaximumPerFrame>1024
                    || GlobalMaximumPerSecond<0 || Rules==null || Rules.Length>KindCount) return false;
                for(var i=0;i<Rules.Length;i++)
                {
                    var rule=Rules[i]; if(rule.Kind<CombatPresentationKind.Damage || rule.Kind>CombatPresentationKind.AreaEnded || !CombatMath.Finite(rule.MergeWindow) || rule.MergeWindow<0 || rule.MaximumPerSecond<0) return false;
                    for(var j=0;j<i;j++) if(Rules[j].Kind==rule.Kind) return false;
                }
                return true;
            }
        }
    }
    public readonly struct CombatPresentationEvent
    {
        public readonly CombatPresentationKind Kind;
        public readonly CombatActorIdentity Subject,Source;
        public readonly Vector3 Position,Direction;
        public readonly float Amount;
        public readonly int Count;
        public readonly bool Critical;
        public readonly ScriptableObject Definition;
        public readonly Health SubjectHealth;
        public CombatPresentationEvent(CombatPresentationKind kind,CombatActorIdentity subject,CombatActorIdentity source,Vector3 position,float amount=0,
            ScriptableObject definition=null,bool critical=false,Health health=null,Vector3 direction=default,int count=1)
        { Kind=kind; Subject=subject; Source=source; Position=position; Amount=amount; Definition=definition; Critical=critical; SubjectHealth=health; Direction=direction; Count=count; }
        public bool IsValid=>Kind>=CombatPresentationKind.Damage && Kind<=CombatPresentationKind.AreaEnded && CombatMath.Finite(Position) && CombatMath.Finite(Direction)
            && CombatMath.Finite(Amount) && Amount>=0 && Count>0;
        internal CombatPresentationEvent Merge(in CombatPresentationEvent value)
        {
            var source=Source.Id==value.Source.Id ? Source : default;
            return new CombatPresentationEvent(Kind,Subject,source,value.Position,(float)Math.Min(float.MaxValue,(double)Amount+value.Amount),Definition,Critical,value.SubjectHealth,value.Direction,
                (int)Math.Min(int.MaxValue,(long)Count+value.Count));
        }
    }
}
