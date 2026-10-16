using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(PeriodicDamageEffect))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class PeriodicDamageEffectEditor : UnityEditor.Editor
    {
        private static readonly string[] Units = { "За один тик", "В секунду", "За всю длительность" };
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Field("Formula", "Общая формула (SO)");
            var shared = serializedObject.FindProperty("Formula").objectReferenceValue != null;
            if (!shared) Field("DamageType", "Тип урона тика");
            var unit = serializedObject.FindProperty("Unit");
            unit.intValue = EditorGUILayout.Popup("Величина урона задана", unit.intValue, Units);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Формула одного стака", EditorStyles.boldLabel);
            var terms = serializedObject.FindProperty("Terms");
            if (shared)
            {
                var chance = serializedObject.FindProperty("CriticalChance");
                chance.floatValue = EditorGUILayout.Slider("Шанс крита стака (%)", chance.floatValue * 100, 0, 100) / 100;
                Field("CriticalMultiplier", "Множитель крита стака");
                EditorGUILayout.HelpBox("Типы урона, диапазоны, лимиты и участие частей в крите заданы в SO формулы. Крит и случайные коэффициенты сохраняются при наложении для каждого стака. Лимит каждой части применяется в выбранных единицах до перевода в урон тика. Крит исходного попадания уже содержится в его уроне; этот шанс — отдельный крит самого статуса.", MessageType.Info);
            }
            else if (terms.arraySize == 0)
            {
                Field("DamagePerStack", "Фиксированная величина");
                if (GUILayout.Button("Перевести в составную формулу"))
                {
                    terms.arraySize = 1;
                    var first = terms.GetArrayElementAtIndex(0);
                    first.FindPropertyRelative("Basis").intValue = (int)StatusDamageBasis.Fixed;
                    first.FindPropertyRelative("Value").floatValue = serializedObject.FindProperty("DamagePerStack").floatValue;
                    first.FindPropertyRelative("HitStage").intValue = (int)HitDamageStage.HealthLost;
                }
            }
            else
            {
                Field("Terms", "Слагаемые (суммируются)");
                EditorGUILayout.HelpBox("Непустая формула заменяет старую фиксированную величину. Проценты вводятся как 20 = 20%. Тип урона тика независим от типа исходного попадания.", MessageType.Info);
            }
            EditorGUILayout.Space();
            if (!shared)
            {
                Field("MinimumAmount", "Минимум на стак"); Field("MaximumAmount", "Максимум (0 = без лимита)");
                EditorGUILayout.HelpBox("Ограничения применяются к сумме в выбранных единицах, затем она переводится в урон за тик. Резисты учитываются после этого.", MessageType.None);
            }
            if (unit.intValue == (int)StatusDamageUnit.OverDuration)
                EditorGUILayout.HelpBox("Вся формула фиксируется при наложении, включая HP и характеристики. Бюджет делится на число запланированных тиков. Досрочное снятие теряет остаток. При общей длительности повторное наложение заменяет оставшийся бюджет новым.", MessageType.Info);
            else
                EditorGUILayout.HelpBox("Исходное попадание всегда сохраняется в конкретном стаке. Текущее/недостающее HP читается перед тиком. Макс. HP и характеристику можно сохранить или пересчитывать. При исчезновении источника его динамическая характеристика даёт 0.", MessageType.Info);
            EditorGUILayout.HelpBox("Для формулы от попадания нужен OnHitStatuses или ApplyFromHit; ручной Apply без попадания отклоняется. Для характеристики нужен Combat Stats на объекте источника и соответствующий ключ.", MessageType.None);
            serializedObject.ApplyModifiedProperties();
            if (!((PeriodicDamageEffect)target).IsValid) EditorGUILayout.HelpBox("Проверь числа, ключи характеристик и ограничения. Некорректная формула отклоняет наложение статуса.", MessageType.Error);
        }
        private void Field(string property, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label), true);
    }
}
