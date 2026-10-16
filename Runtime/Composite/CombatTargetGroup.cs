using UnityEngine;

namespace EasyCombat
{
    internal readonly struct CombatHitKey : System.IEquatable<CombatHitKey>
    {
        internal readonly MonoBehaviour Receiver;
        internal readonly long Life;
        internal CombatHitKey(MonoBehaviour receiver)
        { Receiver=CombatTargetGroups.Key(receiver); Life=Receiver is Health health ? health.Identity.Id : CombatTargetGroups.Life(receiver); }
        public bool Equals(CombatHitKey other)=>ReferenceEquals(Receiver,other.Receiver) && Life==other.Life;
        public override bool Equals(object obj)=>obj is CombatHitKey other && Equals(other);
        public override int GetHashCode()=>(ReferenceEquals(Receiver,null) ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Receiver))*397 ^ Life.GetHashCode();
    }
    /// <summary>Optional adapter identity for compound actors. Application still goes to the actual receiver.</summary>
    public interface ICombatTargetGroup
    {
        MonoBehaviour HitGroup { get; }
        Health RulesTarget { get; }
        long LifeId { get; }
    }
    internal static class CombatTargetGroups
    {
        internal static MonoBehaviour Key(MonoBehaviour receiver)=>receiver is ICombatTargetGroup group && group.HitGroup!=null ? group.HitGroup : receiver;
        internal static Health Rules(MonoBehaviour receiver)=>receiver is ICombatTargetGroup group ? group.RulesTarget : receiver as Health;
        internal static long Life(MonoBehaviour receiver)=>receiver is ICombatTargetGroup group ? group.LifeId : receiver is Health health ? health.Identity.Id : 0;
    }
}
