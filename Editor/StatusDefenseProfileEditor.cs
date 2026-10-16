using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(StatusDefenseProfile))]
    public sealed class StatusDefenseProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update(); var rules=serializedObject.FindProperty("Rules");
            for(var i=0;i<rules.arraySize;i++)
            {
                var rule=rules.GetArrayElementAtIndex(i); EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("Защита "+(i+1),EditorStyles.boldLabel);
                if(GUILayout.Button("Удалить",GUILayout.Width(70))) { rules.DeleteArrayElementAtIndex(i); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
                EditorGUILayout.EndHorizontal();
                Property(rule,"Status","Статус (пусто = любой)"); Property(rule,"Tag","Категория / тег");
                Property(rule,"ApplicationChanceMultiplier","Множитель шанса применения"); Property(rule,"DurationMultiplier","Множитель длительности");
                Property(rule,"PeriodicPowerMultiplier","Множитель силы тиков"); EditorGUILayout.EndVertical();
            }
            using(new EditorGUI.DisabledScope(rules.arraySize>=16)) if(GUILayout.Button("+ Правило"))
            {
                var i=rules.arraySize++; var rule=rules.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("Status").objectReferenceValue=null; rule.FindPropertyRelative("Tag").objectReferenceValue=null;
                foreach(var name in new[] { "ApplicationChanceMultiplier","DurationMultiplier","PeriodicPowerMultiplier" }) rule.FindPropertyRelative(name).floatValue=1;
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("1 = без изменений, 0.5 = половина. Шанс 0 блокирует наложение. Длительность и сила фиксируются отдельно для каждого стака. Сила тиков не меняет множители скорости/резистов или запреты действий статуса. Подходящие правила перемножаются.",MessageType.Info);
            if(!((StatusDefenseProfile)target).IsValid) EditorGUILayout.HelpBox("Проверь до 16 правил, конечные значения и шанс 0–1.",MessageType.Error);
        }
        private static void Property(SerializedProperty rule,string name,string label)=>EditorGUILayout.PropertyField(rule.FindPropertyRelative(name),new GUIContent(label));
    }
}
