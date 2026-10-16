using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(StatusCombinationDefinition))]
    public sealed class StatusCombinationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Обмен атомарный: расходуются живые стаки Consume и накладывается Apply. Если результат отклонён, исходные стаки, их сила и таймеры сохраняются. Запускай через TryCombine или действие реакции Combine Statuses.",MessageType.Info);
            if(!((StatusCombinationDefinition)target).IsValid) EditorGUILayout.HelpBox("Выбери два разных статуса, 1–32 стака и валидный результат.",MessageType.Error);
        }
    }
}
