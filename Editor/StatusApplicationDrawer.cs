using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomPropertyDrawer(typeof(StatusApplication))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class StatusApplicationDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight * 2f + 6f;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("Status"), label);
            row.y += EditorGUIUtility.singleLineHeight + 3;
            var chance = property.FindPropertyRelative("Chance");
            chance.floatValue = EditorGUI.Slider(row, "Шанс наложения (%)", chance.floatValue * 100f, 0f, 100f) / 100f;
            EditorGUI.EndProperty();
        }
    }
}
