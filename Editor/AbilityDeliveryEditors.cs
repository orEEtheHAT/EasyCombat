using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(AbilityDefinition))]
    public sealed class AbilityDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Стоимость и заряд тратятся при выпуске после повторной проверки цели. Отмена подготовки бесплатна. Maximum Charges = 0 оставляет только кулдаун. Урон, лечение и другие действия составляются ниже; SO хранит конфигурацию, таймеры находятся в CombatAbilities.",MessageType.Info);
            if(!((AbilityDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь числа, стоимость и 1–8 действий.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(CombatAbilityAction),true)]
    public sealed class AbilityActionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if(target is AbilityEffectAction) EditorGUILayout.HelpBox("Переиспользует эффект реакций без исходного попадания: hit-relative и lifesteal базы равны нулю. Обычное лечение/статусы/защита используют свои правила.",MessageType.Info);
            if(!((CombatAbilityAction)target).IsValid) EditorGUILayout.HelpBox("Назначь корректный рецепт действия.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(MeleeDefinition))]
    public sealed class MeleeDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Окна в секундах, упорядочены и не пересекаются. До восьми точек на клинке разделяют память попаданий. Проверяется линейный отрезок между отсчётами; для большой дуги используй несколько точек/более частые отсчёты анимации. Attack задаёт только пакет урона.",MessageType.Info);
            if(!((MeleeDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь Attack, радиус и окна в пределах Duration.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(BeamDefinition))]
    public sealed class BeamDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Attack должен быть Hitscan. Damage Per Second масштабирует урон на Interval; шанс статуса остаётся на тик. Pulse Immediately добавляет тик в начале. Pulse Costs оплачиваются перед каждым тиком; при нехватке луч останавливается.",MessageType.Info);
            if(!((BeamDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь hitscan Attack, время, интервал и стоимость тика.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(AreaTriggerDefinition))]
    public sealed class AreaTriggerDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Отдельные пакеты на вход, пребывание и выход. Набор целей определяется при каждом отсчёте; моментальное пересечение между отсчётами не восстанавливается. Выход использует прямой пакет без повторной дальности/falloff. Duration = 0 означает до Stop.",MessageType.Info);
            if(!((AreaTriggerDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь геометрию, пакеты и интервал.",MessageType.Error);
        }
    }
}
