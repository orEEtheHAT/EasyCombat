using System.Runtime.CompilerServices;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EasyCombat
{
    /// <summary>Fallback names captured in combat records. Weak keys do not keep actors or assets alive.</summary>
    public static class CombatNames
    {
        private sealed class Entry { internal readonly string Value; internal Entry(Object key) { Value=key.name; } }
        private static ConditionalWeakTable<Object,Entry> names=new ConditionalWeakTable<Object,Entry>();
        private static readonly ConditionalWeakTable<Object,Entry>.CreateValueCallback create=key=>new Entry(key);
        internal static string Capture(Object key)=>names.GetValue(key,create).Value;
        /// <summary>After renaming a non-Health source or definition at runtime, refresh subsequent records.
        /// Already captured origins retain their original names.</summary>
        public static void Invalidate(Object key) { if(!ReferenceEquals(key,null)) names.Remove(key); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginSession()=>names=new ConditionalWeakTable<Object,Entry>();
    }
}
