using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomPropertyDrawer(typeof(DamageChannel))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class DamageChannelDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight * 2 + 8;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, property.FindPropertyRelative("Type"), new GUIContent("Тип урона"));
            line.y += line.height + 4;
            var oldWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40;
            line.width = (position.width - 12) * .5f;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("Minimum"), new GUIContent("Мин."));
            line.x += line.width + 12;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("Maximum"), new GUIContent("Макс."));
            EditorGUIUtility.labelWidth = oldWidth;
            EditorGUI.EndProperty();
        }
    }
}
