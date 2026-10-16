using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(AreaDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class AreaDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Field("Shape", "Форма"); Field("Radius", "Радиус (м)");
            if (serializedObject.FindProperty("Shape").enumValueIndex == (int)AreaShape.Cone) Field("ConeAngle", "Угол конуса");
            Field("DistanceFalloff", "Ослабление к краям");
            if (serializedObject.FindProperty("DistanceFalloff").boolValue) Field("EdgeMultiplier", "Множитель на краю");
            Field("TargetMask", "Слои целей"); Field("Triggers", "Триггеры"); Field("Targets", "Фильтр команд");
            Field("AffectOwner", "Затрагивает владельца"); Field("RequireLineOfSight", "Стены блокируют");
            if (serializedObject.FindProperty("RequireLineOfSight").boolValue) Field("ObstacleMask", "Слои препятствий");
            Field("UseHurtboxMultiplier", "Множитель ближайшей зоны");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Один конечный получатель получает один удар за импульс, независимо от числа коллайдеров. Дистанция измеряется до ближайшей подходящей поверхности цели. Команды задаёт CombatTeam; объекты без команды считаются чужими для Enemies.", MessageType.Info);
            if (!((AreaDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь радиус, угол и множитель края.", MessageType.Error);
        }
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label));
    }
}
