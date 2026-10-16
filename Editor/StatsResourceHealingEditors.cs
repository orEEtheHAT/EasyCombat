using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(StatModifierDefinition))]
    public sealed class StatModifierDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("(База + сумма Flat) × (1 + сумма AdditivePercent) × произведение Multiply × статусы.\nAdditivePercent: 0.2 = +20%. Multiply: 1.2 = ×1.2. Набор применяется и снимается целиком по своему handle.",MessageType.Info);
            if (!((StatModifierDefinition)target).IsValid) EditorGUILayout.HelpBox("Добавь хотя бы одну корректную строку с ключом характеристики.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(ResourceDefinition))]
    public sealed class ResourceDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Текущее значение принадлежит персонажу. Maximum Stat заменяет фиксированную ёмкость; после изменения характеристики вызови RefreshMaximums(). Положительный расход запускает задержку регенерации.",MessageType.Info);
            if (!((ResourceDefinition)target).IsValid) EditorGUILayout.HelpBox("Некорректные параметры ресурса.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(ResourceProfile))]
    public sealed class ResourceProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("До 32 разных ресурсов. TrySpend проверяет весь список стоимостей перед списанием. Повторные строки одного ресурса суммируются.",MessageType.Info);
            if (!((ResourceProfile)target).IsValid) EditorGUILayout.HelpBox("Проверь пустые ссылки, повторения и параметры ресурсов.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(HealingDefinition))]
    public sealed class HealingDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Проценты задаются долями: 0.05 = 5%. Triggering Health Damage берёт реально потерянные HP без overkill; Including Shield добавляет поглощение щитом. Лимит применяется после крита и множителей. Избыток может пополнить выбранный существующий щит.",MessageType.Info);
            if (!((HealingDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь 1–8 слагаемых, шанс/множитель крита, лимит и индекс щита.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(CombatResources))]
    public sealed class CombatResourcesEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            var resources = (CombatResources)target;
            for (var i = 0; i < resources.Count; i++)
            {
                var view = resources.GetResource(i);
                EditorGUILayout.LabelField(view.Definition != null ? view.Definition.name : "Missing",$"{view.Current:0.##} / {view.Maximum:0.##} | delay {view.RechargeDelay:0.##}");
            }
            if (GUILayout.Button("Обновить ёмкости из статов")) resources.RefreshMaximums();
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }
}
