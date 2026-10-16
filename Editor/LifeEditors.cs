using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(LifeRulesDefinition))]
    public sealed class LifeRulesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update(); Field("MinimumHealth","Нижний предел HP"); Field("AllowDowned","Падение перед смертью");
            if(serializedObject.FindProperty("AllowDowned").boolValue)
            { Field("FinishDownedOnDamage","Обычный урон добивает"); Field("PeriodicCanFinish","Периодический урон добивает"); Field("DownedRestrictions","Запреты в состоянии Downed"); }
            Field("ReviveHealthFraction","Доля HP при возрождении"); Field("ReviveInvulnerability","Неуязвимость после возрождения, с"); Field("ClearStatusesOnRevive","Очистить статусы при возрождении");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Downed — ноль HP без события убийства. Heal не поднимает; используй Revive. NonLethal в политике урона не даёт добить. Добивание всё ещё должно пройти общие правила попадания и защиту.",MessageType.Info);
            if(!((LifeRulesDefinition)target).IsValid) EditorGUILayout.HelpBox("Некорректные параметры жизни.",MessageType.Error);
        }
        private void Field(string name,string label)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label));
    }
    [CustomEditor(typeof(AttributionDefinition))]
    public sealed class AttributionDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("LastHit — последний удар; HighestDamage — максимальный вклад в окне учёта. Учитываются реально снятые HP и, по настройке, поглощённый щит. Overkill не повышает вклад. До восьми помощников, награда один раз на идентификатор жизни.",MessageType.Info);
            if(!((AttributionDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь окно, долю помощи и число участников.",MessageType.Error);
        }
    }
}
