using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(CombatPresentationDefinition))]
    public sealed class CombatPresentationDefinitionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Это ограничения только для звука, VFX и чисел. Урон, статусы и журнал всегда рассчитываются полностью. Отсутствующие в Rules типы событий включены без объединения и индивидуального лимита. Ноль в лимите частоты означает отсутствие лимита.",MessageType.Info);
            EditorGUILayout.HelpBox("Ключ объединения: тип события + жизнь цели + SO + крит + источник (если Separate Sources). Merge Window задерживает вывод до конца окна; Amount суммируется. Частота использует фиксированные секундные окна, не скользящие. Правила копируются при Configure.",MessageType.None);
            if(!((CombatPresentationDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь бюджеты, конечные неотрицательные значения и уникальность Kind.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(CombatPresentationHub))]
    public sealed class CombatPresentationHubEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); var buffer=((CombatPresentationHub)target).Buffer;
            if(buffer==null) return;
            EditorGUILayout.LabelField("В очереди",buffer.Count.ToString()); EditorGUILayout.LabelField("Объединено",buffer.Merged.ToString());
            EditorGUILayout.LabelField("Отброшено: очередь / частота / жизнь",$"{buffer.DroppedCapacity} / {buffer.DroppedRate} / {buffer.DroppedStale}");
            EditorGUILayout.LabelField("Отключено правилом",buffer.Suppressed.ToString());
            if(Application.isPlaying) Repaint();
        }
    }
}
