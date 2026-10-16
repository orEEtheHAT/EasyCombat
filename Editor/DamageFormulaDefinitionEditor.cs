using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(DamageFormulaDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class DamageFormulaDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Общая формула урона", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Terms"), new GUIContent("Слагаемые (1–8)"), true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Слагаемые суммируются в одно попадание. Каждое имеет свой тип, диапазон, лимит и участие в крите. Проценты: 5 = 5%. Все доли HP берутся до этого попадания. Лимит каждой части применяется после крита и множителей источника / зоны / AOE, перед резистом.", MessageType.Info);
            EditorGUILayout.HelpBox("В атаках HP всегда читается у конкретной цели при попадании. В статусах текущее / недостающее HP читается на тике, а макс. HP и характеристику можно сохранить. «За всю длительность» сохраняет всё. Лимит статуса задан на один стак в его единицах: за тик, секунду или всю длительность.", MessageType.None);
            var formula = (DamageFormulaDefinition)target;
            if (!formula.IsValid) EditorGUILayout.HelpBox("Нужны 1–8 корректных слагаемых: максимум ≥ минимума, конечные числа, ключ для характеристики.", MessageType.Error);
            else if (!formula.IsValidForAttack) EditorGUILayout.HelpBox("Формула от исходного попадания предназначена для статуса через OnHitStatuses / ApplyFromHit. Оружие не может рассчитывать свой урон от своего же результата.", MessageType.Warning);
        }
    }

    [CustomPropertyDrawer(typeof(DamageFormulaTerm))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class DamageFormulaTermDrawer : PropertyDrawer
    {
        private static readonly string[] Bases = { "Фиксированная величина", "% исходного попадания", "% текущего HP цели", "% максимального HP цели", "% недостающего HP цели", "% характеристики источника" };
        private static readonly string[] Stages = { "До резистов", "После резистов", "Фактически снятое HP" };
        private static readonly string[] Sampling = { "Сохранить", "Пересчитывать" };
        private static float Line => EditorGUIUtility.singleLineHeight + 4;
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var basis = (StatusDamageBasis)property.FindPropertyRelative("Basis").intValue;
            var lines = 6;
            if (basis == StatusDamageBasis.TriggeringHit) lines += property.FindPropertyRelative("FilterHitType").boolValue ? 3 : 2;
            if (basis == StatusDamageBasis.TargetMaximumHealth) lines++;
            if (basis == StatusDamageBasis.SourceStat) lines += 2;
            return lines * Line + 8;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y + 4, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("Type"), new GUIContent("Тип урона")); row.y += Line;
            var basis = property.FindPropertyRelative("Basis");
            basis.intValue = EditorGUI.Popup(row, "База расчёта", basis.intValue, Bases); row.y += Line;
            var scale = basis.intValue == (int)StatusDamageBasis.Fixed ? 1 : 100;
            var min = property.FindPropertyRelative("Minimum"); var max = property.FindPropertyRelative("Maximum");
            min.floatValue = Mathf.Max(0, EditorGUI.FloatField(row, scale == 1 ? "Минимум" : "Минимум (%)", min.floatValue * scale) / scale); row.y += Line;
            max.floatValue = Mathf.Max(min.floatValue, EditorGUI.FloatField(row, scale == 1 ? "Максимум" : "Максимум (%)", max.floatValue * scale) / scale); row.y += Line;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("MaximumDamage"), new GUIContent("Лимит (0 = без лимита)")); row.y += Line;
            var ignore = property.FindPropertyRelative("IgnoreCritical");
            ignore.boolValue = !EditorGUI.Toggle(row, "Крит влияет на эту часть", !ignore.boolValue); row.y += Line;
            if (basis.intValue == (int)StatusDamageBasis.TriggeringHit)
            {
                var stage = property.FindPropertyRelative("HitStage"); stage.intValue = EditorGUI.Popup(row, "Урон попадания", stage.intValue, Stages); row.y += Line;
                var filter = property.FindPropertyRelative("FilterHitType"); EditorGUI.PropertyField(row, filter, new GUIContent("Только выбранный тип")); row.y += Line;
                if (filter.boolValue) EditorGUI.PropertyField(row, property.FindPropertyRelative("HitType"), new GUIContent("Тип исходного урона"));
            }
            else if (basis.intValue == (int)StatusDamageBasis.SourceStat || basis.intValue == (int)StatusDamageBasis.TargetMaximumHealth)
            {
                var stat = basis.intValue == (int)StatusDamageBasis.SourceStat;
                if (stat) { EditorGUI.PropertyField(row, property.FindPropertyRelative("Stat"), new GUIContent("Характеристика")); row.y += Line; }
                var sampling = property.FindPropertyRelative("Sampling");
                sampling.intValue = EditorGUI.Popup(row, stat ? "Значение источника" : "Макс. HP в статусах", sampling.intValue, Sampling);
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomEditor(typeof(AreaOverlapGroupDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class AreaOverlapGroupDefinitionEditor : UnityEditor.Editor
    {
        private static readonly string[] Modes = { "Суммировать все лужи", "Одна лужа на цель", "Одна лужа от каждого источника" };
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var mode = serializedObject.FindProperty("Mode"); mode.intValue = EditorGUILayout.Popup("Пересечение", mode.intValue, Modes);
            if (mode.intValue != (int)AreaOverlapMode.Stack)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Interval"), new GUIContent("Общий интервал (с)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("ApplyImmediately"), new GUIContent("Первый тик группы сразу"));
                EditorGUILayout.HelpBox("На каждом тике для каждой цели выбирается доступная лужа с наибольшим приоритетом; при равенстве — созданная раньше. Только она наносит урон и проверяет шанс статуса. Подмена лужи сохраняет общий ритм группы. Новая лужа подключается к нему; отдельного мгновенного тика у неё нет.", MessageType.Info);
            }
            EditorGUILayout.HelpBox("Правило действует только между лужами с одной и той же ссылкой на этот SO. Разные группы независимы. Источник определяется по жизни актёра, даже после его уничтожения. Уже наложенные статусы продолжают жить по собственным правилам стаков.", MessageType.None);
            serializedObject.ApplyModifiedProperties();
            if (!((AreaOverlapGroupDefinition)target).IsValid) EditorGUILayout.HelpBox("Интервал должен быть конечным и ≥ 0.01 с.", MessageType.Error);
        }
    }
}
