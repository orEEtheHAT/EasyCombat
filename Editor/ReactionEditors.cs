using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(ReactionDefinition))]
    public sealed class ReactionDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Реакция ставит действия в очередь после завершения попадания. По умолчанию периодический урон и потомки других реакций исключены. Target cooldown относится к другому участнику боя, даже если действие лечит владельца. Лимит проков действует на одно разрешение удара/AOE вместе с цепочкой реакций.",MessageType.Info);
            if (!((ReactionDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь условия и добавь 1–8 валидных действий.",MessageType.Error);
        }
    }
    [CustomEditor(typeof(CombatReactionAction),true)]
    public sealed class ReactionActionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (target is ReactionDamageAction)
                EditorGUILayout.HelpBox("Attack задаёт готовый пакет урона/формулу и заменяет Fixed/Fraction. Его способ доставки здесь не используется: действие поражает выбранную цель напрямую или взрывает Area вокруг неё. Без Attack можно отразить долю реально нанесённого урона с собственным лимитом.",MessageType.Info);
            if (!((CombatReactionAction)target).IsValid) EditorGUILayout.HelpBox("Заполни параметры действия.",MessageType.Error);
        }
    }
}
