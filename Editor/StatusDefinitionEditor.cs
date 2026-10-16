using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(StatusDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class StatusDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Field("DisplayName", "Название"); Field("Color", "Цвет"); Field("Icon", "Иконка");
            Field("Tags", "Теги / категории"); Field("Restrictions", "Запреты действий");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Время и повторное наложение", EditorStyles.boldLabel);
            Field("Duration", "Длительность (с)"); Field("TickInterval", "Интервал тика (с)"); Field("Stacking", "Повторное наложение");
            var mode = (StatusStacking)serializedObject.FindProperty("Stacking").intValue;
            if (mode == StatusStacking.StackAndRefresh || mode == StatusStacking.Independent)
                Field("MaximumStacks", "Максимум стаков");
            if (mode == StatusStacking.Independent)
            {
                Field("StackOverflow", "При достижении лимита");
                EditorGUILayout.HelpBox("У каждого стака свои длительность, тики и источник. Новое наложение не продлевает остальные. Ignore New (по умолчанию) отклоняет новое наложение на лимите. Replace Soonest Expiring заменяет стак с ближайшим окончанием.", MessageType.Info);
                if (serializedObject.FindProperty("StackOverflow").intValue == (int)StatusStackOverflow.ReplaceSoonestExpiring)
                    EditorGUILayout.HelpBox("Замена запускает срок и первый тик нового стака заново. При очень частом наложении стаки могут заменяться до первого тика. Ignore New позволяет уже наложенным стакам отработать свой срок.", MessageType.Info);
            }
            EditorGUILayout.Space();
            Field("TickEffects", "Действия каждого тика"); Field("StatModifiers", "Множители характеристик"); Field("ResistanceModifiers", "Прибавки к резистам");
            serializedObject.ApplyModifiedProperties();
            foreach (var issue in CombatAssetLibrary.Issues((StatusDefinition)target)) EditorGUILayout.HelpBox(issue, MessageType.Warning);
            EditorGUILayout.HelpBox("Первый тик — после интервала. В Independent действие вызывается отдельно для каждого стака (Stacks = 1); модификаторы зависят от числа ещё не истёкших стаков. Общие режимы сохраняют прежнюю суммарную обработку.", MessageType.Info);
        }
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
    }
}
