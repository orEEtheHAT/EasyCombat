using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(AreaSpawnDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class AreaSpawnDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Field("Pulses", "Рецепт тиков"); Field("Prefab", "Префаб области");
            var trigger = serializedObject.FindProperty("Trigger");
            trigger.intValue = EditorGUILayout.Popup("Когда создавать", trigger.intValue, new[] { "На завершающем контакте", "На каждом новом контакте" });
            var placement = serializedObject.FindProperty("Placement");
            placement.intValue = EditorGUILayout.Popup("Размещение", placement.intValue, new[] { "На поверхности контакта", "На земле под контактом" });
            if (placement.intValue == (int)AreaSpawnPlacement.GroundBelow)
            {
                Field("GroundMask", "Слои поверхности"); Field("ProbeHeight", "Начало поиска выше (м)");
                Field("GroundDistance", "Поиск вниз от контакта (м)"); Field("MaximumSlope", "Максимальный уклон (°)");
                EditorGUILayout.HelpBox("Поиск идёт вниз по мировой оси Y. Коллайдеры персонажей / Hurtbox и триггеры пропускаются. Если поверхности нет — область не создаётся.", MessageType.Info);
            }
            Field("SurfaceOffset", "Отступ от поверхности (м)");
            serializedObject.ApplyModifiedProperties();
            var recipe = (AreaSpawnDefinition)target;
            if (!recipe.IsValid) EditorGUILayout.HelpBox("Нужны префаб DamageArea и валидный рецепт с атакой Area. Проверь параметры размещения.", MessageType.Error);
            EditorGUILayout.HelpBox("Одна область на контакт, независимо от числа жертв взрыва. Нужен DamageAreaPool в DamageEmitter. Размер визуала можно связать с радиусом через DamageAreaVisual.", MessageType.Info);
        }
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
    }

    [CustomEditor(typeof(AreaPulseDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class AreaPulseDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Attack"), new GUIContent("Атака каждого тика (Area)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Duration"), new GUIContent("Длительность (с)"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OverlapGroup"), new GUIContent("Группа пересечений (SO)"));
            var group = serializedObject.FindProperty("OverlapGroup").objectReferenceValue as AreaOverlapGroupDefinition;
            var shared = group != null && group.Mode != AreaOverlapMode.Stack;
            if (shared)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Priority"), new GUIContent("Приоритет в группе"));
                EditorGUILayout.HelpBox($"Общий ритм из {group.name}: раз в {group.Interval:0.###} с. Режим: {group.Mode}. Больший приоритет побеждает; при равенстве — более ранняя лужа. Подавленная лужа не наносит урон и не накладывает статус.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Interval"), new GUIContent("Интервал тиков (с)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ApplyImmediately"), new GUIContent("Первый тик сразу"));
                EditorGUILayout.HelpBox("Каждая лужа независимо наносит урон и накладывает статус. Для выбора одной назначь группу с одиночным режимом.", MessageType.None);
            }
            serializedObject.ApplyModifiedProperties();
            var recipe = (AreaPulseDefinition)target;
            if (!recipe.IsValid) EditorGUILayout.HelpBox("Нужна валидная атака Area, положительная длительность и интервал ≥ 0.01 с.", MessageType.Error);
            else if (!shared)
            {
                var ticks = System.Math.Floor(((double)recipe.Duration + 1e-7) / recipe.Interval) + (recipe.ApplyImmediately ? 1 : 0);
                EditorGUILayout.HelpBox($"Импульсов за полный срок: {ticks:0}. Каждый заново выбирает цели и применяет урон / статусы атаки. При мгновенном первом тике взрыв и область могут сразу наложить по одному стакy.", MessageType.Info);
            }
        }
    }
}
