using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(HealthDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class HealthDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MaximumHealth"), new GUIContent("Максимум HP"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Resistances"), new GUIContent("Профиль резистов"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("HitRules"), new GUIContent("Общие правила попадания"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Defense"), new GUIContent("Слои защиты"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("StatusImmunities"), new GUIContent("Иммунитет к статусам"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("StatusDefense"), new GUIContent("Защита от статусов"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("PreventHealing"), new GUIContent("Запретить лечение"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ReceivedHealingMultiplierStat"), new GUIContent("Множитель входящего лечения"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Один профиль резистов содержит любое число разных типов урона. Защита от огня и защита от яда рассчитываются независимо. Иммунитет к статусу — отдельная настройка.", MessageType.Info);
            EditorGUILayout.HelpBox("Пустые правила и защита сохраняют базовое поведение. Щиты и таймеры принадлежат персонажу; SO общие. Порядок слоёв задаётся профилем защиты.", MessageType.None);
            if (!((HealthDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь HP, правила попадания и профиль защиты.", MessageType.Error);
        }
    }
}
