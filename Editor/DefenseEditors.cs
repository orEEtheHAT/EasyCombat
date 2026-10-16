using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(HitRulesDefinition))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class HitRulesDefinitionEditor : UnityEditor.Editor
    {
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Кому разрешено попадание", EditorStyles.boldLabel);
            Field("AllowSelf", "От самого себя"); Field("AllowAllies", "От союзника"); Field("AllowEnemies", "От противника");
            Field("AllowNeutralActors", "От актёра без общей системы команд"); Field("AllowEnvironment", "От окружения (без источника)");
            EditorGUILayout.HelpBox("Сначала определяется самоурон; затем команды. Если команда отсутствует у одной из сторон, это нейтральное взаимодействие. Команда стрелка сохраняется при выстреле, команда цели читается при попадании. Запрет действует и на статусы. Луч всё равно встречает коллайдер запрещённой цели: это не режим прострела союзников.", MessageType.Info);
            Field("Invulnerable", "Постоянная неуязвимость профиля");
            var chance = serializedObject.FindProperty("DodgeChance"); chance.floatValue = EditorGUILayout.Slider("Уклонение (%)", chance.floatValue * 100, 0, 100) / 100;
            Field("DodgePeriodic", "Уклоняться от тиков статусов");
            Field("InvulnerabilityAfterHit", "Неуязвимость после потери HP/щита (с)");
            Field("HitInvulnerabilityAffectsPeriodic", "Учитывать тики для этой неуязвимости");
            EditorGUILayout.HelpBox("Один бросок уклонения на пакет. По умолчанию тики продолжают работать во время неуязвимости после попадания и сами её не запускают. Ручная GrantInvulnerability защищает от всего. Таймеры используют игровое время.", MessageType.None);
            serializedObject.ApplyModifiedProperties();
            if (!((HitRulesDefinition)target).IsValid) EditorGUILayout.HelpBox("Проверь вероятность и длительность.", MessageType.Error);
        }
    }
    [CustomEditor(typeof(DefenseProfile))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class DefenseProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("Layers"), new GUIContent("Слои защиты (до 8)"), true);
            serializedObject.ApplyModifiedProperties();
            var profile = (DefenseProfile)target;
            EditorGUILayout.LabelField("Порядок обработки", EditorStyles.boldLabel);
            for (var stage = 0; stage < 2; stage++)
            {
                if (stage == 1) EditorGUILayout.LabelField("↓ РЕЗИСТЫ ЗДОРОВЬЯ ↓", EditorStyles.boldLabel);
                if (profile.Layers == null) continue;
                for (var i = 0; i < profile.Layers.Length; i++)
                {
                    var slot = profile.Layers[i]; if ((int)slot.Stage != stage || slot.Layer == null) continue;
                    EditorGUILayout.LabelField($"{i}: {slot.Layer.name}" + (slot.DisabledAtStart ? " (изначально выключен)" : ""));
                }
            }
            EditorGUILayout.LabelField("↓ ЗДОРОВЬЕ → СТАТУСЫ → ЖУРНАЛ");
            EditorGUILayout.HelpBox("Внутри каждой стадии порядок соответствует списку. Несколько щитов расходуются последовательно. Каждый экземпляр персонажа хранит свой запас; один SO слоя можно использовать повторно. Перестановку и конфигурацию делай вне боя; в бою переключай слои через Health.SetDefenseLayerEnabled.", MessageType.Info);
            if (!profile.IsValid) EditorGUILayout.HelpBox("Нужно не более 8 валидных слоёв без пустых ссылок.", MessageType.Error);
        }
    }
    [CustomPropertyDrawer(typeof(DefenseSlot))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class DefenseSlotDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => (EditorGUIUtility.singleLineHeight + 3) * 3 + 4;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y + 2, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(row, property.FindPropertyRelative("Layer"), label); row.y += EditorGUIUtility.singleLineHeight + 3;
            var stage = property.FindPropertyRelative("Stage"); stage.intValue = EditorGUI.Popup(row, "Когда работает", stage.intValue, new[] { "До резистов", "После резистов" }); row.y += EditorGUIUtility.singleLineHeight + 3;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("DisabledAtStart"), new GUIContent("Изначально выключен"));
            EditorGUI.EndProperty();
        }
    }
    [CustomEditor(typeof(DefenseLayerDefinition), true)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class DefenseLayerEditor : UnityEditor.Editor
    {
        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
        private void Percent(string name, string label)
        { var p = serializedObject.FindProperty(name); p.floatValue = EditorGUILayout.Slider(label, p.floatValue * 100, 0, 100) / 100; }
        public override void OnInspectorGUI()
        {
            serializedObject.Update(); Field("DisplayName", "Название в журнале");
            var kinds = serializedObject.FindProperty("HitKinds"); kinds.intValue = EditorGUILayout.Popup("Какие попадания", kinds.intValue, new[] { "Все", "Прямые (включая тики луж)", "Тики статусов" });
            Field("FilterTypes", "Только выбранные типы урона");
            if (serializedObject.FindProperty("FilterTypes").boolValue)
            { Field("Types", "Типы (null = без типа)"); EditorGUILayout.HelpBox("Пустой список с включённым фильтром не защищает ни от чего.", MessageType.None); }
            EditorGUILayout.Space();
            if (target is ShieldLayer)
            {
                Field("Capacity", "Максимум щита"); Percent("InitialFraction", "Заполнение при сбросе (%)"); Percent("Absorption", "Доля поглощения (%)");
                Field("RechargePerSecond", "Восстановление в секунду"); Field("DelayAfterAbsorption", "Задержка после поглощения (с)");
                Field("DamageCosts","Расход щита по типам урона (1 = обычный)");
                EditorGUILayout.HelpBox("Стоимость должна быть больше нуля. Пустой список использует 1 единицу щита на 1 урона; 2 расходует щит вдвое быстрее.",MessageType.None);
                EditorGUILayout.HelpBox("Остаток, который не помещается в щит, проходит дальше. 50% поглощения пропускает половину даже при полном щите. Задержку сбрасывает только реально поглощённый урон. Выключенный слой сохраняет запас и приостанавливает восстановление.", MessageType.Info);
            }
            else if(target is TemporaryShieldLayer)
            {
                Field("Capacity","Ёмкость временного щита"); Field("Duration","Длительность, с"); Field("DecayPerSecond","Уменьшение в секунду"); Percent("Absorption","Поглощение (%)");
                EditorGUILayout.HelpBox("Попадание не продлевает срок. При истечении слой выключается. ResetDefenseLayer заново выдаёт щит и начинает его срок.",MessageType.Info);
            }
            else if(target is ChargeProtectionLayer)
            {
                Field("Charges","Число зарядов"); Percent("Reduction","Поглощение зарядом (%)"); Field("RechargeInterval","Один заряд каждые, с (0 = без восстановления)");
                EditorGUILayout.HelpBox("Один заряд на подходящее попадание целиком. Несколько каналов не расходуют несколько зарядов.",MessageType.Info);
            }
            else if(target is DamageCapLayer)
            { Field("MaximumDamage","Предел урона за попадание (0 = выключен)"); Percent("MaximumHealthFraction","Предел от максимума HP (%)"); }
            else if (target is FlatArmorLayer)
            { Field("Reduction", "Вычесть из одного попадания"); EditorGUILayout.HelpBox("Вычитание одно на весь пакет, пропорционально подходящим частям. Разбиение огня на несколько строк не умножает броню.", MessageType.Info); }
            else if (target is ReductionLayer) Percent("Reduction", "Снижение урона (%)");
            else if (target is BlockLayer)
            {
                Percent("Chance", "Шанс блока (%)"); Percent("Reduction", "Поглощение блоком (%)"); Field("Arc", "Сектор спереди (°)");
                Field("Costs","Цена успешного блока");
                EditorGUILayout.HelpBox("Смотрит по transform.forward цели и направлению входящего урона. Один бросок на пакет. 360° — всесторонний блок; при меньшем секторе урон без направления не блокируется. Для активного блока настрой слой изначально выключенным и включай на время удержания кнопки.", MessageType.Info);
            }
            else DrawPropertiesExcluding(serializedObject, "m_Script", "DisplayName", "HitKinds", "FilterTypes", "Types");
            serializedObject.ApplyModifiedProperties();
            if (!((DefenseLayerDefinition)target).IsValid) EditorGUILayout.HelpBox("Некорректные параметры слоя.", MessageType.Error);
        }
    }
}
