using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomPropertyDrawer(typeof(StatusDamageTerm))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class StatusDamageTermDrawer : PropertyDrawer
    {
        private static readonly string[] Bases = { "Фиксированная величина", "% исходного попадания", "% текущего HP цели", "% максимального HP цели", "% недостающего HP цели", "% характеристики источника" };
        private static readonly string[] Stages = { "До резистов", "После резистов (до отсечения по HP)", "Фактически снятое HP" };
        private static readonly string[] Sampling = { "Сохранить при наложении", "Пересчитывать каждый тик" };
        private static float Line => EditorGUIUtility.singleLineHeight + 3;
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var basis = (StatusDamageBasis)property.FindPropertyRelative("Basis").intValue;
            var lines = 2;
            if (basis == StatusDamageBasis.TriggeringHit) lines += property.FindPropertyRelative("FilterHitType").boolValue ? 3 : 2;
            if (basis == StatusDamageBasis.TargetMaximumHealth) lines++;
            if (basis == StatusDamageBasis.SourceStat) lines += 2;
            return lines * Line + 5;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y + 2, position.width, EditorGUIUtility.singleLineHeight);
            var basis = property.FindPropertyRelative("Basis");
            basis.intValue = EditorGUI.Popup(row, "База расчёта", basis.intValue, Bases); row.y += Line;
            var value = property.FindPropertyRelative("Value");
            var fixedValue = basis.intValue == (int)StatusDamageBasis.Fixed;
            value.floatValue = Mathf.Max(0, EditorGUI.FloatField(row, fixedValue ? "Величина" : "Доля (%)", value.floatValue * (fixedValue ? 1 : 100)) / (fixedValue ? 1 : 100)); row.y += Line;
            if (basis.intValue == (int)StatusDamageBasis.TriggeringHit)
            {
                var stage = property.FindPropertyRelative("HitStage");
                stage.intValue = EditorGUI.Popup(row, "Урон попадания", stage.intValue, Stages); row.y += Line;
                var filter = property.FindPropertyRelative("FilterHitType");
                EditorGUI.PropertyField(row, filter, new GUIContent("Только выбранный тип")); row.y += Line;
                if (filter.boolValue) EditorGUI.PropertyField(row, property.FindPropertyRelative("HitType"), new GUIContent("Тип (пусто = без типа)"));
            }
            else if (basis.intValue == (int)StatusDamageBasis.SourceStat || basis.intValue == (int)StatusDamageBasis.TargetMaximumHealth)
            {
                if (basis.intValue == (int)StatusDamageBasis.SourceStat)
                { EditorGUI.PropertyField(row, property.FindPropertyRelative("Stat"), new GUIContent("Ключ характеристики")); row.y += Line; }
                var sample = property.FindPropertyRelative("Sampling");
                sample.intValue = EditorGUI.Popup(row, "Значение", sample.intValue, Sampling);
            }
            EditorGUI.EndProperty();
        }
    }
}
