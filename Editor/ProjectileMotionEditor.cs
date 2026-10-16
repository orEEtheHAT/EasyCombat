using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(ProjectileMotionDefinition))]
    public sealed class ProjectileMotionEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Профиль захватывается при запуске. Homing поворачивает к явной цели или ищет её через Search Area. Рикошет относится к стенам; Chain Jumps — к новым целям после попадания. Return After Seconds возвращает к прежнему владельцу. Для бумеранга обычно нужен Pierce Targets, иначе первый контакт завершит полёт.",MessageType.Info);
            EditorGUILayout.HelpBox("Range и Lifetime ограничивают весь путь, включая возврат. Hit Again On Return очищает память попаданий на обратный путь. До восьми подшагов за Simulate; остаток большого dt сохраняется. Обычный снаряд без профиля не выполняет этот дополнительный расчёт.",MessageType.Info);
            if(!((ProjectileMotionDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь Search Area для автонаведения/цепочки, числа и множители 0–1.",MessageType.Error);
        }
    }
}
