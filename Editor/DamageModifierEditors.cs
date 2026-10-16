using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(DamageProtectionPolicy))]
    public sealed class DamageProtectionPolicyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("BypassArmor"),new GUIContent("Обойти броню / блок"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("BypassShields"),new GUIContent("Обойти щиты"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("BypassResistance"),new GUIContent("Обойти резисты и уязвимости"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("NonLethal"),new GUIContent("Несмертельный урон"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("IgnoreMinimumHealth"),new GUIContent("Игнорировать нижний предел HP"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("PreventDownedState"),new GUIContent("Добить / миновать падение"));
            if (!serializedObject.FindProperty("BypassResistance").boolValue)
            {
                Percent("PercentResistancePenetration","Доля пробитого резиста, %");
                Percent("FlatResistancePenetration","Вычесть процентных пунктов");
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Положительный резист: max(0, резист × (1 − доля) − пункты). Например, 80%, пробивание 50% и ещё 10 п.п. → 30%. Отрицательная уязвимость сохраняется. Обход защиты не отменяет команды, уклонение и неуязвимость.",MessageType.Info);
        }
        private void Percent(string property,string label)
        { var p=serializedObject.FindProperty(property); p.floatValue=EditorGUILayout.Slider(label,p.floatValue*100,0,100)/100; }
    }
    [CustomEditor(typeof(DamageModifierProfile))]
    public sealed class DamageModifierProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var rules=serializedObject.FindProperty("Rules");
            for (var i=0;i<rules.arraySize;i++)
            {
                var rule=rules.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("Правило "+(i+1),EditorStyles.boldLabel);
                if (GUILayout.Button("Удалить",GUILayout.Width(70))) { rules.DeleteArrayElementAtIndex(i); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
                EditorGUILayout.EndHorizontal();
                Property(rule,"Kind","Прямой / периодический"); Property(rule,"Type","Тип (пусто = любой)");
                Property(rule,"RequiredTag","Обязательный тег"); Property(rule,"ExcludedTag","Запрещённый тег");
                Property(rule,"Multiplier","Множитель"); Property(rule,"ConvertType","Преобразовать тип");
                if (rule.FindPropertyRelative("ConvertType").boolValue) Property(rule,"DestinationType","Новый тип");
                EditorGUILayout.EndVertical();
            }
            using (new EditorGUI.DisabledScope(rules.arraySize>=16))
                if (GUILayout.Button("+ Правило"))
                {
                    var i=rules.arraySize++; var rule=rules.GetArrayElementAtIndex(i);
                    rule.FindPropertyRelative("Multiplier").floatValue=1;
                    rule.FindPropertyRelative("Kind").enumValueIndex=0;
                    rule.FindPropertyRelative("ConvertType").boolValue=false;
                    foreach (var name in new[] { "Type","RequiredTag","ExcludedTag","DestinationType" }) rule.FindPropertyRelative(name).objectReferenceValue=null;
                }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("Правила выполняются сверху вниз; после конверсии следующее правило видит новый тип. 1 = без изменения, 0.5 = половина урона, 2 = удвоение. Исходящие модификаторы фиксируются при выстреле/наложении стака, входящие читаются при попадании. Лимиты формулы применяются после множителей.",MessageType.Info);
            if (!((DamageModifierProfile)target).IsValid) EditorGUILayout.HelpBox("Проверь множители, типы конверсии и лимит 16 правил.",MessageType.Error);
        }
        private static void Property(SerializedProperty rule,string name,string label) => EditorGUILayout.PropertyField(rule.FindPropertyRelative(name),new GUIContent(label));
    }
}
