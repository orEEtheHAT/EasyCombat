using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [Serializable]
    public struct CombatCatalogEntry
    {
        public string Id;
        public ScriptableObject Definition;
        public CombatCatalogEntry(string id,ScriptableObject definition) { Id=id; Definition=definition; }
    }
    [CreateAssetMenu(menuName="EasyCombat/Saving/Definition Catalog",fileName="CombatCatalog")]
    public sealed class CombatDefinitionCatalog:ScriptableObject
    {
        [SerializeField] private string compatibilityKey="1";
        [SerializeField] private CombatCatalogEntry[] entries=Array.Empty<CombatCatalogEntry>();
        private Dictionary<string,ScriptableObject> byId;
        private Dictionary<ScriptableObject,string> byObject;
        public string CompatibilityKey=>compatibilityKey;
        public int Count=>entries?.Length ?? 0;
        public CombatCatalogEntry GetEntry(int index)=>entries[index];
        private void OnValidate() { byId=null; byObject=null; }
        public bool Configure(CombatCatalogEntry[] values,string key="1")
        {
            if(!Build(values,key,out var ids,out var objects)) return false;
            entries=(CombatCatalogEntry[])values.Clone(); compatibilityKey=key; byId=ids; byObject=objects; return true;
        }
        public bool Validate()=>Ensure();
        private bool Ensure()
        { if(byId!=null) return true; return Build(entries,compatibilityKey,out byId,out byObject); }
        private static bool Build(CombatCatalogEntry[] values,string key,out Dictionary<string,ScriptableObject> ids,out Dictionary<ScriptableObject,string> objects)
        {
            ids=null; objects=null; if(values==null || string.IsNullOrWhiteSpace(key)) return false;
            var a=new Dictionary<string,ScriptableObject>(StringComparer.Ordinal); var b=new Dictionary<ScriptableObject,string>();
            foreach(var entry in values)
            {
                if(string.IsNullOrWhiteSpace(entry.Id) || entry.Definition==null || a.ContainsKey(entry.Id) || b.ContainsKey(entry.Definition)) return false;
                a.Add(entry.Id,entry.Definition); b.Add(entry.Definition,entry.Id);
            }
            ids=a; objects=b; return true;
        }
        public bool TryGetId(ScriptableObject definition,out string id)
        { id=null; return definition!=null && Ensure() && byObject.TryGetValue(definition,out id); }
        public bool TryResolve<T>(string id,out T definition) where T:ScriptableObject
        { definition=null; if(string.IsNullOrEmpty(id) || !Ensure() || !byId.TryGetValue(id,out var value)) return false; definition=value as T; return definition!=null; }
    }
}
