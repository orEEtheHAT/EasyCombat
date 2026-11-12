using EasyResources;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Integrations.Editor
{
    [CustomEditor(typeof(EasyResourcesCostProvider))]
    public sealed class EasyResourcesCostProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var provider=(EasyResourcesCostProvider)target;
            if(provider.Mapping==null)
                EditorGUILayout.HelpBox("Create EasyCombat / Integrations / EasyResources Map and assign it here. Map every combat cost resource to a wallet resource.",MessageType.Error);
            else if(!provider.Mapping.Validate(out var error)) EditorGUILayout.HelpBox(error,MessageType.Error);
            if(provider.Container==null && provider.GetComponent<ResourceContainer>()==null)
                EditorGUILayout.HelpBox("Assign the player's ResourceContainer. A runtime-only store can instead be connected through Bind(store, mapping).",MessageType.Info);
            if(provider.GetComponent<CombatResources>()!=null)
                EditorGUILayout.HelpBox("Costs use EasyResources only. Remove CombatResources when migrating this actor; its regeneration, restore reactions and saved values belong to the separate legacy backend.",MessageType.Warning);
            if(!string.IsNullOrEmpty(provider.LastError)) EditorGUILayout.HelpBox(provider.LastError,MessageType.Warning);
        }
    }
}
