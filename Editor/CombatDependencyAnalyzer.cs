using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    public sealed class CombatDiagnostic
    {
        public ScriptableObject Asset;
        public string Property,Message;
        public bool Error;
    }
    public sealed class CombatDependencyReport
    {
        public readonly List<ScriptableObject> Assets=new List<ScriptableObject>();
        public readonly List<CombatDiagnostic> Issues=new List<CombatDiagnostic>();
        public int Edges,Cycles;
        public bool Truncated;
    }
    public static class CombatDependencyAnalyzer
    {
        private struct Frame { public ScriptableObject Asset; public int Next; }
        public static CombatDependencyReport Analyze(IEnumerable<ScriptableObject> roots)
        {
            var report=new CombatDependencyReport(); var graph=new Dictionary<ScriptableObject,List<ScriptableObject>>();
            var reverse=new Dictionary<ScriptableObject,List<ScriptableObject>>(); var queue=new Queue<ScriptableObject>(); var seen=new HashSet<ScriptableObject>();
            if(roots!=null) foreach(var root in roots) if(root!=null && seen.Add(root)) queue.Enqueue(root);
            while(queue.Count>0)
            {
                if(report.Assets.Count>=4096 || report.Edges>=16384) { report.Truncated=true; break; }
                var asset=queue.Dequeue(); report.Assets.Add(asset); var children=new List<ScriptableObject>(); graph[asset]=children;
                using(var serialized=new SerializedObject(asset))
                {
                    var property=serialized.GetIterator();
                    while(property.Next(true))
                    {
                        if(property.propertyType!=SerializedPropertyType.ObjectReference) continue;
                        var reference=property.objectReferenceValue;
                        if(reference==null && property.objectReferenceInstanceIDValue!=0)
                            report.Issues.Add(new CombatDiagnostic { Asset=asset,Property=property.propertyPath,Message="Потерянная ссылка",Error=true });
                        if(!(reference is ScriptableObject child)) continue;
                        if(report.Edges++>=16384) { report.Truncated=true; break; }
                        children.Add(child); if(!reverse.TryGetValue(child,out var parents)) { parents=new List<ScriptableObject>(); reverse.Add(child,parents); } parents.Add(asset);
                        if(seen.Add(child)) queue.Enqueue(child);
                    }
                }
            }
            var colors=new Dictionary<ScriptableObject,int>(); var stack=new List<Frame>(); var pathIndices=new Dictionary<ScriptableObject,int>(); var cyclic=new HashSet<ScriptableObject>();
            foreach(var root in report.Assets)
            {
                if(colors.ContainsKey(root)) continue; colors[root]=1; pathIndices[root]=0; stack.Add(new Frame { Asset=root });
                while(stack.Count>0)
                {
                    var index=stack.Count-1; var frame=stack[index]; graph.TryGetValue(frame.Asset,out var children);
                    if(children==null || frame.Next>=children.Count) { colors[frame.Asset]=2; pathIndices.Remove(frame.Asset); stack.RemoveAt(index); continue; }
                    var child=children[frame.Next++]; stack[index]=frame;
                    if(!colors.TryGetValue(child,out var color)) { colors[child]=1; pathIndices[child]=stack.Count; stack.Add(new Frame { Asset=child }); }
                    else if(color==1)
                    {
                        report.Cycles++; var start=pathIndices[child]; var names=new List<string>();
                        for(var i=start;i<stack.Count;i++) { cyclic.Add(stack[i].Asset); if(names.Count<12) names.Add(stack[i].Asset.name); }
                        names.Add(child.name);
                        if(report.Cycles<=64) report.Issues.Add(new CombatDiagnostic { Asset=child,Message="Цикл ссылок: "+string.Join(" → ",names)+". Проверь, является ли он намеренным; runtime-очереди ограничены отдельно." });
                    }
                }
            }
            // Avoid recursive IsValid calls for cycle members and every definition that depends on them.
            var unsafeValidation=new HashSet<ScriptableObject>(cyclic); queue.Clear(); foreach(var value in cyclic) queue.Enqueue(value);
            while(queue.Count>0) { var node=queue.Dequeue(); if(reverse.TryGetValue(node,out var parents)) foreach(var parent in parents) if(unsafeValidation.Add(parent)) queue.Enqueue(parent); }
            foreach(var asset in report.Assets)
            {
                if(unsafeValidation.Contains(asset) || report.Truncated) continue;
                try { foreach(var issue in CombatAssetLibrary.Issues(asset)) report.Issues.Add(new CombatDiagnostic { Asset=asset,Message=issue,Error=true }); }
                catch(Exception exception) { report.Issues.Add(new CombatDiagnostic { Asset=asset,Message="Ошибка валидации: "+exception.Message,Error=true }); }
            }
            if(report.Truncated) report.Issues.Add(new CombatDiagnostic { Message="Достигнут бюджет графа: 4096 определений / 16384 ссылок. Анализ неполный.",Error=true });
            return report;
        }
    }
    public sealed class CombatDiagnosticsWindow:EditorWindow
    {
        private ScriptableObject root;
        private CombatDependencyReport report;
        private Vector2 scroll;
        [MenuItem("EasyCombat/Tools/Definition Diagnostics")]
        public static void Open() { var window=GetWindow<CombatDiagnosticsWindow>(); window.titleContent=new GUIContent("Combat Diagnostics"); window.minSize=new Vector2(650,400); window.Show(); }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Диагностика определений",EditorStyles.boldLabel);
            root=(ScriptableObject)EditorGUILayout.ObjectField("Корневой SO (необязательно)",root,typeof(ScriptableObject),false);
            if(GUILayout.Button(root!=null ? "Проверить граф выбранного SO" : "Проверить библиотеку проекта"))
            {
                var roots=new List<ScriptableObject>(); if(root!=null) roots.Add(root); else foreach(var item in CombatAssetLibrary.Scan()) if(!(item.Asset is CombatDefinitionCatalog)) roots.Add(item.Asset);
                report=CombatDependencyAnalyzer.Analyze(roots);
            }
            if(report==null) return;
            EditorGUILayout.LabelField($"Определений: {report.Assets.Count} · Ссылок: {report.Edges} · Циклов: {report.Cycles} · Сообщений: {report.Issues.Count}");
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var issue in report.Issues)
            {
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.ObjectField(issue.Asset,typeof(ScriptableObject),false);
                    EditorGUILayout.HelpBox((string.IsNullOrEmpty(issue.Property) ? "" : issue.Property+"\n")+issue.Message,issue.Error ? MessageType.Error : MessageType.Warning);
                }
            }
            if(report.Issues.Count==0) EditorGUILayout.HelpBox("Проблем в проверенных определениях не найдено.",MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
