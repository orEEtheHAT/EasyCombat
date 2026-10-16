using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(AttackDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class AttackDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Field("DisplayName", "Название");
            Field("Tags", "Теги атаки (до 8)");
            Field("Delivery", "Способ попадания");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Урон", EditorStyles.boldLabel);
            Field("Protection", "Пробивание и обход защиты");
            Field("StatusOnly", "Только статусы, без урона");
            var channels = serializedObject.FindProperty("DamageChannels");
            if (!serializedObject.FindProperty("StatusOnly").boolValue)
            {
                Field("Formula", "Общая формула (SO)");
                if (serializedObject.FindProperty("Formula").objectReferenceValue != null)
                    EditorGUILayout.HelpBox("SO формулы заменяет числовые каналы. У каждой части свой тип, лимит и участие в крите. Доли HP вычисляются при попадании по конкретной цели. Взрыв и прямой урон используют одну формулу атаки.", MessageType.Info);
                else if (channels.arraySize == 0)
                {
                    Field("DamageType", "Тип урона"); Field("MinimumDamage", "Минимум"); Field("MaximumDamage", "Максимум");
                    if (GUILayout.Button("Перевести в составной урон"))
                    {
                        channels.arraySize = 1;
                        var first = channels.GetArrayElementAtIndex(0);
                        first.FindPropertyRelative("Type").objectReferenceValue = serializedObject.FindProperty("DamageType").objectReferenceValue;
                        first.FindPropertyRelative("Minimum").floatValue = serializedObject.FindProperty("MinimumDamage").floatValue;
                        first.FindPropertyRelative("Maximum").floatValue = serializedObject.FindProperty("MaximumDamage").floatValue;
                    }
                }
                else
                {
                    Field("DamageChannels", "Состав урона (до 8 частей)");
                    EditorGUILayout.HelpBox("Каждая часть независимо выбирает урон из своего диапазона и проходит свой резист. Крит общий на выстрел. Статусы накладываются один раз на цель.", MessageType.Info);
                }
            }
            var chance = serializedObject.FindProperty("CriticalChance");
            chance.floatValue = EditorGUILayout.Slider("Шанс крита (%)", chance.floatValue * 100f, 0f, 100f) / 100f;
            Field("CriticalMultiplier", "Множитель крита");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Стрельба", EditorStyles.boldLabel);
            Field("RoundsPerMinute", "Выстрелов в минуту");
            Field("Range", "Дальность (м)");
            Field("HitMask", "Слои попаданий");
            Field("Triggers", "Триггеры");
            var delivery = (DamageDelivery)serializedObject.FindProperty("Delivery").enumValueIndex;
            if (delivery != DamageDelivery.Area)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Геометрия и контакт", EditorStyles.boldLabel);
                Field(delivery == DamageDelivery.Projectile ? "ProjectileRadius" : "HitscanRadius", "Радиус попадания (м)");
                var behavior = serializedObject.FindProperty("ContactBehavior");
                behavior.enumValueIndex = EditorGUILayout.Popup("При столкновении", behavior.enumValueIndex,
                    new[] { "Остановиться", "Пробивать цели" });
                if (behavior.enumValueIndex == (int)ContactBehavior.PierceTargets)
                {
                    Field("MaximumTargets", "Всего целей (0 = без лимита)");
                    EditorGUILayout.HelpBox("Порядок — от ближней цели к дальней. Один контакт на получателя за выстрел, даже при нескольких Hurtbox. Иммунитет тоже расходует контакт. Стены останавливают атаку.", MessageType.Info);
                    if (serializedObject.FindProperty("Area").objectReferenceValue != null)
                        EditorGUILayout.HelpBox("Каждый новый контакт вызывает отдельный взрыв вместо прямого урона. Перекрывающиеся взрывы могут повредить одну цель несколько раз. Лимит считает цели на траектории, а не жертв взрыва.", MessageType.Info);
                }
                EditorGUILayout.HelpBox("Радиус 0 — тонкая линия, диаметр = 2 × радиус. Геометрия проверяется по всему пути. Размер визуального префаба настраивается отдельно; широкая атака может задеть пол или стену краем.", MessageType.None);
            }
            if (delivery == DamageDelivery.Projectile)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Снаряд", EditorStyles.boldLabel);
                Field("ProjectilePrefab", "Префаб");
                Field("ProjectileSpeed", "Скорость (м/с)");
                Field("ProjectileLifetime", "Время жизни (с)");
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("OnHitStatuses"), new GUIContent("Статусы при попадании"), true);
            if (!serializedObject.FindProperty("StatusOnly").boolValue)
            {
                var policy = serializedObject.FindProperty("StatusPolicy");
                policy.intValue = EditorGUILayout.Popup("Статусы после попадания", policy.intValue,
                    new[] { "Только при потере HP", "При разрешённом контакте", "При потере HP или щита" });
                EditorGUILayout.HelpBox("Запрет команды, уклонение и неуязвимость всегда отменяют статусы. Разрешённый контакт допускает их при броне / резисте / щите; остальные режимы требуют соответствующей потери ресурса.", MessageType.None);
            }
            Field("Area", "Область воздействия / взрыв");
            if (delivery != DamageDelivery.Area)
            {
                Field("ImpactArea", "Создать область после контакта");
                if (serializedObject.FindProperty("ImpactArea").objectReferenceValue != null)
                    EditorGUILayout.HelpBox("Создаваемая область имеет отдельные урон, статусы и время жизни. Назначь DamageAreaPool в компоненте DamageEmitter. Лужи не возникают при истечении времени или дальности снаряда.", MessageType.Info);
            }
            Field("DamageModifierStat", "Ключ модификатора урона");
            Field("FireRateModifierStat", "Ключ скорострельности");
            serializedObject.ApplyModifiedProperties();
            var weapon = (AttackDefinition)target;
            if (!weapon.IsValid)
                EditorGUILayout.HelpBox("Проверь диапазон урона, множитель крита и положительные параметры атаки. Некорректная атака не стреляет.", MessageType.Error);
            else if (weapon.UsesFormula)
                EditorGUILayout.HelpBox($"Урон определяется формулой {weapon.Formula.name} для каждой цели.\nСлагаемых: {weapon.ChannelCount} · Интервал выстрелов: {weapon.Cooldown:0.###} с.\nТочный DPS зависит от HP цели, лимитов и участия частей в крите.", MessageType.Info);
            else
            {
                var average = (weapon.TotalMinimum + weapon.TotalMaximum) * 0.5f
                    * (1f + weapon.CriticalChance * (weapon.CriticalMultiplier - 1f));
                EditorGUILayout.HelpBox($"Суммарный средний урон: {average:0.##}\nКрит: {weapon.TotalMinimum * weapon.CriticalMultiplier:0.##}–{weapon.TotalMaximum * weapon.CriticalMultiplier:0.##}"
                    + (weapon.RoundsPerMinute > 0 ? $"\nИнтервал: {weapon.Cooldown:0.###} с  ·  DPS: {average * weapon.RoundsPerMinute / 60f:0.##}" : "\nБез задержки между выстрелами")
                    + "\nДо резистов, зон попадания и статусов.", MessageType.Info);
            }
        }
        private void Field(string property, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label));
    }
}
