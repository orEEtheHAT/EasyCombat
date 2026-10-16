using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>Game-owned stable IDs for pre-existing actors and modifier/source objects. No global scene lookup.</summary>
    public sealed class CombatReferenceRegistry
    {
        private sealed class ReferenceComparer:IEqualityComparer<object>
        {
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        private readonly Dictionary<string,object> references=new Dictionary<string,object>(StringComparer.Ordinal);
        private readonly Dictionary<object,string> ids=new Dictionary<object,string>(new ReferenceComparer());
        internal readonly SortedDictionary<string,Health> Actors=new SortedDictionary<string,Health>(StringComparer.Ordinal);
        public int ActorCount=>Actors.Count;
        public bool RegisterActor(string id,Health actor)
        {
            if(actor==null || !RegisterSource(id,actor.gameObject)) return false;
            Actors[id]=actor; return true;
        }
        public bool RegisterSource(string id,object value)
        {
            if(string.IsNullOrWhiteSpace(id) || value==null || value is UnityEngine.Object unity && unity==null) return false;
            if(references.TryGetValue(id,out var existing)) return ReferenceEquals(existing,value);
            if(ids.ContainsKey(value)) return false;
            references.Add(id,value); ids.Add(value,id); return true;
        }
        public bool TryGetId(object value,out string id) { id=null; return value!=null && ids.TryGetValue(value,out id); }
        public bool TryResolve(string id,out object value) { value=null; return !string.IsNullOrEmpty(id) && references.TryGetValue(id,out value); }
        public bool TryGetActor(string id,out Health actor) { actor=null; return !string.IsNullOrEmpty(id) && Actors.TryGetValue(id,out actor) && actor!=null; }
        internal IEnumerable<KeyValuePair<string,object>> References=>references;
        public bool Unregister(string id)
        {
            if(string.IsNullOrEmpty(id) || !references.TryGetValue(id,out var value)) return false;
            references.Remove(id); ids.Remove(value); Actors.Remove(id); return true;
        }
        public void Clear() { references.Clear(); ids.Clear(); Actors.Clear(); }
    }
}
