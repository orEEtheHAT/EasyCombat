using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    public static class CombatCatalogBuilder
    {
        [MenuItem("EasyCombat/Saving/Create Definition Catalog")]
        private static void CreateMenu()
        {
            var path=EditorUtility.SaveFilePanelInProject("Create combat catalog","CombatCatalog","asset","Choose a catalog location.");
            if(!string.IsNullOrEmpty(path)) Selection.activeObject=Create(path);
        }
        public static CombatDefinitionCatalog Create(string path)
        {
            if(string.IsNullOrEmpty(path) || !path.StartsWith("Assets/",StringComparison.Ordinal) || !path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase)
                || AssetDatabase.LoadMainAssetAtPath(path)!=null) throw new ArgumentException("Choose a new .asset path under Assets.");
            var catalog=ScriptableObject.CreateInstance<CombatDefinitionCatalog>(); catalog.name=System.IO.Path.GetFileNameWithoutExtension(path);
            try
            {
                Populate(catalog); CombatAssetLibrary.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\','/'));
                AssetDatabase.CreateAsset(catalog,path); AssetDatabase.SaveAssets(); return catalog;
            }
            catch { UnityEngine.Object.DestroyImmediate(catalog); throw; }
        }
        /// <summary>Preserve existing IDs; append known definitions and their SO dependencies using GUID + local file ID.</summary>
        public static void Populate(CombatDefinitionCatalog catalog)
        {
            if(catalog==null) throw new ArgumentNullException(nameof(catalog));
            var entries=new List<CombatCatalogEntry>(); var seen=new HashSet<ScriptableObject>();
            for(var i=0;i<catalog.Count;i++) { var entry=catalog.GetEntry(i); entries.Add(entry); if(entry.Definition!=null) seen.Add(entry.Definition); }
            var paths=new HashSet<string>(StringComparer.Ordinal);
            foreach(var record in CombatAssetLibrary.Scan()) if(!(record.Asset is CombatDefinitionCatalog)) paths.Add(record.Path);
            foreach(var path in AssetDatabase.GetDependencies(new List<string>(paths).ToArray(),true))
            {
                foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if(!(asset is ScriptableObject definition) || definition is CombatDefinitionCatalog || CombatAssetLibrary.Category(definition.GetType())<0 || !seen.Add(definition)) continue;
                    if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(definition,out string guid,out long localId)) throw new InvalidOperationException("Definition must be saved before cataloging: "+definition.name);
                    entries.Add(new CombatCatalogEntry(guid+":"+localId,definition));
                }
            }
            Undo.RecordObject(catalog,"Populate combat catalog");
            if(!catalog.Configure(entries.ToArray(),catalog.CompatibilityKey)) throw new InvalidOperationException("Catalog contains duplicate, empty or ambiguous IDs. Existing contents were preserved.");
            EditorUtility.SetDirty(catalog);
        }
    }
    [CustomEditor(typeof(CombatDefinitionCatalog))]
    public sealed class CombatDefinitionCatalogEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var catalog=(CombatDefinitionCatalog)target;
            EditorGUILayout.HelpBox("ID определения сохраняется при переносе ассета. Кнопка добавляет определения проекта и их зависимости, сохраняя уже заданные ID. Compatibility Key меняй при несовместимой правке данных; перед загрузкой старые снимки нужно мигрировать.",MessageType.Info);
            if(GUILayout.Button("Добавить определения проекта")) { CombatCatalogBuilder.Populate(catalog); AssetDatabase.SaveAssets(); }
            if(!catalog.Validate()) EditorGUILayout.HelpBox("Пустой Compatibility Key, пропущенный ассет или неоднозначный ID.",MessageType.Error);
        }
    }
}
