using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(BodyPartDefinition))]
    public sealed class BodyPartDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Health задаёт прочность части; Core Damage Fraction — долю урона, переданную основному HP после локальной защиты. Share Before Local HP Clamp сохраняет избыточный урон. Group With Core объединяет части в одну цель для пробивания и одного взрыва. Слои защиты части и тела независимы.",MessageType.Info);
            EditorGUILayout.HelpBox("Проценты по умолчанию считаются от HP тела. Статус передаётся телу один раз, локально не накладывается. Для попаданий используй Hurtbox/CombatBodyPart; прямое обращение к LocalHealth обходит общие правила тела.",MessageType.Info);
            if(!((BodyPartDefinition)target).IsValid) EditorGUILayout.HelpBox("Нужен корректный Health без Downed; доля передачи от 0 до 1.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(BuildupDefinition))]
    public sealed class BuildupDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Прирост = Flat Per Hit + реальные потерянные HP × Per Health Damage + поглощённый щит × Per Shield Damage. Фильтр типа учитывает только соответствующий канал. Порог запускает Actions после завершения удара; источником остаётся атакующий.",MessageType.Info);
            EditorGUILayout.HelpBox("Один порог на попадание. Keep Overflow сохраняет остаток максимум до одного порога. При активном кулдауне шкала заполняется, но повторная проверка порога происходит на следующем приросте. Смерть/Reset/Restore обнуляют шкалы.",MessageType.Info);
            if(!((BuildupDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь положительный порог, неотрицательные числа и 1–8 действий.",MessageType.Error);
        }
    }
}
