using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(ResistanceProfile))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class ResistanceProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("0% — обычный урон, 100% — иммунитет, отрицательное значение — уязвимость. Без записи тип получает 0% защиты.", MessageType.Info);
            var entries = serializedObject.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("Type"), GUIContent.none);
                var remove = GUILayout.Button("×", GUILayout.Width(26));
                EditorGUILayout.EndHorizontal();
                var reduction = entry.FindPropertyRelative("Reduction");
                reduction.floatValue = EditorGUILayout.Slider("Сопротивление (%)", reduction.floatValue * 100f, -100f, 100f) / 100f;
                var type = entry.FindPropertyRelative("Type").objectReferenceValue;
                for (var j = 0; j < i; j++)
                    if (type != null && entries.GetArrayElementAtIndex(j).FindPropertyRelative("Type").objectReferenceValue == type)
                        EditorGUILayout.HelpBox("Тип повторяется. Используется последняя запись.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                if (remove) { entries.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("Добавить тип"))
            {
                entries.arraySize++;
                var item = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                item.FindPropertyRelative("Type").objectReferenceValue = null;
                item.FindPropertyRelative("Reduction").floatValue = 0f;
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
