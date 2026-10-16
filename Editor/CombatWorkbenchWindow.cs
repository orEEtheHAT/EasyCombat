using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace EasyCombat.Editor
{
    /// <summary>Authoring UI only; no dependency from the player build to this window.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class CombatWorkbenchWindow : EditorWindow
    {
        [SerializeField] private ScriptableObject selected;
        [SerializeField] private int category = -1;
        [SerializeField] private int page;
        [SerializeField] private string search = "";
        private List<CombatAssetRecord> library = new List<CombatAssetRecord>();
        private readonly List<CombatAssetRecord> filtered = new List<CombatAssetRecord>();
        private UnityEditor.Editor inspector;
        private VisualElement body, detail, navigation, summary, links;
        private Label detailTitle;
        private Label countLabel, notice;
        private ListView list;
        private bool refreshQueued;
        // Unity hot reload serializes private serializable classes too, turning a null inline
        // report into an empty object. Load this ephemeral cache from the diagnostic JSON instead.
        [NonSerialized] private CombatBenchmarkReport report;
        private CombatJournal journal;
        private long journalRevision = -1;
        private ListView journalView;
        private Label journalSummary;
        private readonly List<string> journalRows = new List<string>();

        [MenuItem("EasyCombat/Combat Studio", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<CombatWorkbenchWindow>();
            window.titleContent = new GUIContent("EasyCombat Studio");
            window.minSize = new Vector2(1000, 640);
            window.Show();
        }
        private void OnEnable()
        {
            EditorApplication.projectChanged += QueueRefresh;
            Undo.undoRedoPerformed += QueueRefresh;
        }
        private void OnDisable()
        {
            EditorApplication.projectChanged -= QueueRefresh;
            Undo.undoRedoPerformed -= QueueRefresh;
            EditorApplication.delayCall -= Refresh;
            refreshQueued = false;
            ReleaseInspector();
        }
        public void CreateGUI()
        {
            ReleaseInspector();
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("studio");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.oreethehat.easy-combat/Editor/UI/CombatWorkbench.uss");
            if (sheet == null) sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/GamesFactory/Combat/Editor/UI/CombatWorkbench.uss");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            var header = Box(root, "header row");
            var brand = Box(header, "brand");
            Text(brand, "EASYCOMBAT  /  AUTHORING", "eyebrow");
            Text(brand, "EasyCombat Studio", "brand-title");
            Text(brand, "Оружие, защита и эффекты — в одной библиотеке.", "muted");
            var tabs = Box(header, "tabs row");
            string[] names = { "Библиотека", "Как устроено", "Нагрузка", "Журнал" };
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                var tab = Button(tabs, names[i], () => { page = index; CreateGUI(); }, "tab");
                tab.EnableInClassList("active", page == i);
            }
            var toolbar = Box(root, "toolbar row");
            Button(toolbar, "+ Создать", CreateMenu, "primary");
            Button(toolbar, "Сортировать демо", () => Act(() => { CombatAssetLibrary.OrganizeDemoAssets(); Refresh(); }, "Демо рассортировано. Ссылки сохранены."));
            Button(toolbar, "Открыть полигон", () => Act(() =>
            {
                if (!EditorApplication.ExecuteMenuItem("EasyCombat/Samples/Open Sandbox"))
                    EditorUtility.DisplayDialog("EasyCombat", "Импортируй пример через Package Manager → EasyCombat → Samples.", "OK");
            }));
            var spacer = Box(toolbar, "grow");
            Button(toolbar, "Сохранить SO", () => Act(AssetDatabase.SaveAssets, "Настройки сохранены."));
            notice = Text(root, "Изменения общих SO влияют на все объекты, которые их используют. Ctrl+Z — отмена.", "notice");
            body = Box(root, "body grow");
            library = CombatAssetLibrary.Scan();
            if (page == 0) BuildLibrary();
            else if (page == 1) BuildGuide();
            else if (page == 2) BuildPerformance();
            else BuildJournal();
        }
        private void QueueRefresh()
        {
            if (refreshQueued) return;
            refreshQueued = true;
            EditorApplication.delayCall += Refresh;
        }
        public void Refresh()
        {
            EditorApplication.delayCall -= Refresh;
            refreshQueued = false;
            if (body == null) return;
            library = CombatAssetLibrary.Scan();
            if (page == 0)
            {
                BuildNavigation(); Filter();
                // Keep the IMGUI editor alive during typing and dragging controls.
                if (selected != null && inspector != null && inspector.target == selected) RefreshMetadata();
                else ShowDetail();
            }
        }
        private void BuildLibrary()
        {
            body.AddToClassList("row");
            navigation = new ScrollView(ScrollViewMode.Vertical); navigation.AddToClassList("navigation");
            navigation.style.flexGrow = 0; navigation.style.flexShrink = 0; body.Add(navigation);
            var browser = Box(body, "browser");
            var top = Box(browser, "browser-top");
            Text(top, "БИБЛИОТЕКА", "eyebrow");
            var field = new ToolbarSearchField { name = "asset-search", value = search };
            field.RegisterValueChangedCallback(e => { search = e.newValue; Filter(); });
            top.Add(field);
            countLabel = Text(top, "", "muted");
            list = new ListView { name = "asset-list", fixedItemHeight = 64, selectionType = SelectionType.Single };
            list.AddToClassList("grow");
            list.makeItem = () =>
            {
                var row = new VisualElement(); row.AddToClassList("asset-row");
                Text(row, "", "asset-title").name = "title";
                Text(row, "", "asset-meta").name = "meta";
                return row;
            };
            list.bindItem = (element, index) =>
            {
                var record = filtered[index];
                element.Q<Label>("title").text = record.Title;
                element.Q<Label>("meta").text = CombatAssetLibrary.Codes[record.Category] + "  /  " + record.Asset.name;
                element.style.borderLeftColor = CombatAssetLibrary.Colors[record.Category];
                element.tooltip = record.Path;
            };
            list.selectionChanged += values =>
            {
                foreach (var item in values)
                    if (item is CombatAssetRecord record) { selected = record.Asset; ShowDetail(); break; }
            };
            browser.Add(list);
            detail = Box(body, "detail grow");
            BuildNavigation(); Filter();
            if (selected == null && filtered.Count > 0) SelectAsset(filtered[0].Asset);
            else ShowDetail();
        }
        private void BuildNavigation()
        {
            if (navigation == null) return;
            navigation.Clear();
            Text(navigation, "КОЛЛЕКЦИИ", "eyebrow");
            CategoryButton(-1, "Все настройки", library.Count);
            for (var i = 0; i < CombatAssetLibrary.Types.Length; i++)
            {
                var total = 0;
                foreach (var item in library) if (item.Category == i) total++;
                CategoryButton(i, CombatAssetLibrary.Titles[i], total);
            }
            Box(navigation, "grow");
            Text(navigation, "SO = общий рецепт\nКомпонент = состояние\nПолигон = проверка", "sidebar-note");
        }
        private void CategoryButton(int index, string title, int count)
        {
            var button = Button(navigation, title + "   " + count, () => { category = index; BuildNavigation(); Filter(); }, "category");
            button.EnableInClassList("selected", category == index);
        }
        private void Filter()
        {
            filtered.Clear();
            var query = (search ?? "").Trim();
            foreach (var record in library)
                if ((category < 0 || category == record.Category) &&
                    (query.Length == 0 || (record.Title + " " + record.Asset.name + " " + record.Path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                    filtered.Add(record);
            list.itemsSource = filtered;
            list.Rebuild();
            var index = filtered.FindIndex(x => x.Asset == selected);
            if (index >= 0) list.SetSelectionWithoutNotify(new[] { index });
            else list.ClearSelection();
            countLabel.text = filtered.Count == 0 ? "Ничего не найдено" : $"Показано {filtered.Count} из {library.Count}";
        }
        public void SelectAsset(ScriptableObject asset)
        {
            if (asset == null || CombatAssetLibrary.Category(asset.GetType()) < 0) return;
            selected = asset; category = CombatAssetLibrary.Category(asset.GetType()); search = "";
            if (page != 0) { page = 0; CreateGUI(); return; }
            rootVisualElement.Q<ToolbarSearchField>("asset-search")?.SetValueWithoutNotify("");
            BuildNavigation(); Filter(); ShowDetail();
        }
        private void ReleaseInspector()
        {
            if (inspector != null) DestroyImmediate(inspector);
            inspector = null;
        }
        private void ShowDetail()
        {
            if (detail == null) return;
            ReleaseInspector(); detail.Clear();
            if (selected == null) { Text(detail, "Выбери настройку в библиотеке или создай новую.", "empty"); return; }
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("grow"); detail.Add(scroll);
            var card = Box(scroll, "detail-header");
            var type = CombatAssetLibrary.Category(selected.GetType());
            var label = Text(card, CombatAssetLibrary.Titles[type].ToUpperInvariant() + "  /  SCRIPTABLE OBJECT", "eyebrow");
            label.style.color = CombatAssetLibrary.Colors[type];
            detailTitle = Text(card, CombatAssetLibrary.DisplayName(selected), "detail-title");
            Text(card, AssetDatabase.GetAssetPath(selected), "path");
            var actions = Box(card, "row actions");
            Button(actions, "Показать в Project", () => { Selection.activeObject = selected; EditorGUIUtility.PingObject(selected); });
            Button(actions, "Создать вариант", () => Act(() => { var copy = CombatAssetLibrary.Duplicate(selected); library = CombatAssetLibrary.Scan(); SelectAsset(copy); }, "Вариант создан. Ссылки на общие статусы и типы сохранены."));
            summary = Box(scroll, "summary");
            UpdateSummary();
            var foldout = new Foldout { text = "Связи с другими настройками", value = true }; foldout.AddToClassList("links"); scroll.Add(foldout);
            links = new VisualElement(); foldout.Add(links);
            AddLinks(links, "ИСПОЛЬЗУЕТ", false);
            AddLinks(links, "ИСПОЛЬЗУЕТСЯ В SO", true);
            Text(scroll, "ПАРАМЕТРЫ", "section-title");
            inspector = UnityEditor.Editor.CreateEditor(selected);
            var editor = new IMGUIContainer(() =>
            {
                if (inspector == null || inspector.target == null) return;
                var width = EditorGUIUtility.labelWidth;
                try
                {
                    EditorGUIUtility.labelWidth = Mathf.Min(220, Mathf.Max(145, detail.resolvedStyle.width * .38f));
                    EditorGUI.BeginChangeCheck();
                    inspector.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck()) QueueRefresh();
                }
                finally { EditorGUIUtility.labelWidth = width; }
            });
            editor.AddToClassList("inspector"); scroll.Add(editor);
        }
        private void RefreshMetadata()
        {
            detailTitle.text = CombatAssetLibrary.DisplayName(selected);
            UpdateSummary(); links.Clear();
            AddLinks(links, "ИСПОЛЬЗУЕТ", false);
            AddLinks(links, "ИСПОЛЬЗУЕТСЯ В SO", true);
        }
        private void UpdateSummary()
        {
            summary.Clear();
            var metrics = Box(summary, "row metrics");
            if (selected is AttackDefinition weapon)
            {
                var average = (weapon.TotalMinimum + weapon.TotalMaximum) * .5f * (1 + weapon.CriticalChance * (weapon.CriticalMultiplier - 1));
                Metric(metrics, "УРОН", weapon.StatusOnly ? "статусы" : weapon.UsesFormula ? "по формуле" : $"{weapon.TotalMinimum:0.#}–{weapon.TotalMaximum:0.#}");
                Metric(metrics, "ВЫСТР./МИН", weapon.RoundsPerMinute > 0 ? $"{weapon.RoundsPerMinute:0.#}" : "без лимита");
                Metric(metrics, "СРЕДНИЙ DPS", weapon.UsesFormula ? "зависит от цели" : weapon.RoundsPerMinute > 0 ? $"{average * weapon.RoundsPerMinute / 60:0.#}" : "—");
                Text(summary, "DPS на одну цель до защиты, зон, ослабления AOE и тиков; при непрерывной стрельбе.", "muted wrap");
                if (weapon.Delivery != DamageDelivery.Area)
                    Text(summary, $"Диаметр попадания: {weapon.HitRadius * 2:0.##} м · "
                        + (weapon.ContactBehavior == ContactBehavior.StopOnFirstHit ? "остановка при контакте"
                            : weapon.MaximumTargets == 0 ? "пробивание без лимита целей" : $"до {weapon.MaximumTargets} целей")
                        + ". Стены останавливают атаку.", "muted wrap");
                if (!weapon.UsesFormula && weapon.ChannelCount > 1)
                    for (var i = 0; i < weapon.ChannelCount; i++)
                    {
                        var part = weapon.GetChannel(i);
                        Text(summary, $"{part.Type?.Label ?? "Без типа"}: {part.Minimum:0.##}–{part.Maximum:0.##}", "muted wrap");
                    }
            }
            else if (selected is PeriodicDamageEffect periodic)
            {
                Metric(metrics, "СЛАГАЕМЫХ", periodic.Formula != null ? (periodic.Formula.Terms?.Length ?? 0).ToString() : periodic.Terms != null && periodic.Terms.Length > 0 ? periodic.Terms.Length.ToString() : "1 (фикс.)");
                Metric(metrics, "ЕДИНИЦЫ", periodic.Unit == StatusDamageUnit.PerTick ? "за тик" : periodic.Unit == StatusDamageUnit.PerSecond ? "в секунду" : "за длительность");
                Text(summary, "Сохранённые значения принадлежат каждому стаку. Проценты от текущего/недостающего HP пересчитываются; общий бюджет фиксируется целиком.", "muted wrap");
            }
            else if (selected is DamageFormulaDefinition formula)
            {
                Metric(metrics, "СЛАГАЕМЫХ", (formula.Terms?.Length ?? 0).ToString());
                Text(summary, "Один SO для оружия, взрыва, лужи и статуса. Лимиты — после множителей, до резистов. HP вычисляется отдельно для каждой цели.", "muted wrap");
            }
            else if (selected is AreaOverlapGroupDefinition group)
            {
                Metric(metrics, "ПЕРЕСЕЧЕНИЕ", group.Mode == AreaOverlapMode.Stack ? "сумма" : group.Mode == AreaOverlapMode.OnePerTarget ? "одна на цель" : "по источникам");
                if (group.Mode != AreaOverlapMode.Stack) Metric(metrics, "ОБЩИЙ ТИК", $"{group.Interval:0.###} с");
            }
            else if (selected is StatusDefinition status)
            {
                Metric(metrics, "ДЛИТЕЛЬНОСТЬ", $"{status.Duration:0.##} с");
                Metric(metrics, "ТИК", $"{status.TickInterval:0.##} с");
                Metric(metrics, "МАКС. СТАКОВ", status.Stacking == StatusStacking.StackAndRefresh || status.Stacking == StatusStacking.Independent ? status.MaximumStacks.ToString() : "1");
                Text(summary, status.Stacking == StatusStacking.Independent
                    ? "Каждый стак имеет собственные тики, срок жизни и источник." : "Этот режим использует общий таймер статуса.", "muted wrap");
            }
            else if (selected is HealthDefinition health)
            {
                Metric(metrics, "МАКС. HP", $"{health.MaximumHealth:0.##}");
                Metric(metrics, "ПРОФИЛЬ ЗАЩИТЫ", health.Resistances != null ? health.Resistances.name : "нет");
            }
            else if (selected is DefenseProfile defense)
            {
                Metric(metrics, "СЛОЁВ", (defense.Layers?.Length ?? 0).ToString());
                Text(summary, "Сначала слои до резистов, затем резисты здоровья, затем остальные слои. В каждой стадии сохраняется порядок списка.", "muted wrap");
            }
            else if (selected is ShieldLayer shield)
            {
                Metric(metrics, "ЁМКОСТЬ", shield.Capacity.ToString("0.##"));
                Metric(metrics, "ВОССТ./С", shield.RechargePerSecond.ToString("0.##"));
            }
            foreach (var issue in CombatAssetLibrary.Issues(selected)) Text(summary, issue, "warning wrap");
        }
        private void AddLinks(VisualElement parent, string title, bool reverse)
        {
            var targets = CombatAssetLibrary.Dependencies(selected, reverse, library);
            var row = Box(parent, "dependency-row"); Text(row, title, "eyebrow");
            var chips = Box(row, "row chips");
            if (targets.Count == 0) Text(chips, reverse ? "Нет прямых ссылок из других SO" : "Нет связанных SO", "muted");
            foreach (var target in targets) Button(chips, CombatAssetLibrary.DisplayName(target) + "  ↗", () => SelectAsset(target), "chip");
        }
        private void CreateMenu()
        {
            var menu = new GenericMenu();
            for (var i = 0; i < CombatAssetLibrary.Types.Length; i++)
            {
                var type = CombatAssetLibrary.Types[i];
                if (!type.IsAbstract) menu.AddItem(new GUIContent(CombatAssetLibrary.Titles[i]), false, () => CreateAsset(type));
            }
            foreach (var candidate in TypeCache.GetTypesDerivedFrom<StatusTickEffect>())
            {
                if (candidate.IsAbstract || candidate.ContainsGenericParameters) continue;
                var type = candidate;
                var title = type == typeof(PeriodicDamageEffect) ? "Периодический урон" : type == typeof(PeriodicHealingEffect) ? "Периодическое лечение" : type.Name;
                menu.AddItem(new GUIContent("Действие тика/" + title), false, () => CreateAsset(type));
            }
            foreach (var candidate in TypeCache.GetTypesDerivedFrom<DefenseLayerDefinition>())
            {
                if (!candidate.IsVisible || candidate.IsAbstract || candidate.ContainsGenericParameters) continue;
                var type = candidate;
                var title = type == typeof(ShieldLayer) ? "Щит" : type == typeof(FlatArmorLayer) ? "Плоская броня"
                    : type == typeof(ReductionLayer) ? "Процентное снижение" : type == typeof(BlockLayer) ? "Направленный блок" : type.Name;
                menu.AddItem(new GUIContent("Слой защиты/" + title), false, () => CreateAsset(type));
            }
            menu.ShowAsContext();
        }
        private void CreateAsset(Type type)
        {
            Act(() =>
            {
                CombatAssetLibrary.EnsureFolder(CombatAssetLibrary.Folder(type));
                var path = EditorUtility.SaveFilePanelInProject("Создать настройку", type.Name, "asset", "Выбери имя и папку.", CombatAssetLibrary.Folder(type));
                if (string.IsNullOrEmpty(path)) return;
                var asset = CombatAssetLibrary.Create(type, path);
                library = CombatAssetLibrary.Scan(); SelectAsset(asset);
            });
        }
        private void BuildGuide()
        {
            var scroll = new ScrollView(); scroll.AddToClassList("page"); body.Add(scroll);
            Text(scroll, "От рецепта к попаданию", "page-title");
            Text(scroll, "Один SO можно использовать у сотни врагов. HP, таймеры и стаки у каждого свои.", "lead");
            var flow = Box(scroll, "row flow");
            GuideCard(flow, "01 / РЕЦЕПТ", "AttackDefinition", "До 8 частей урона, крит, темп, доставка, радиус, пробивание, AOE и статусы.");
            GuideCard(flow, "02 / ВЫСТРЕЛ", "DamageEmitter", "ЛКМ или AI вызывает TryFire. Луч или снаряд ищет попадание.");
            GuideCard(flow, "03 / ЦЕЛЬ", "Hurtbox → Health", "Зона × множитель, затем сопротивление. HP уменьшается.");
            GuideCard(flow, "04 / ПОСЛЕДСТВИЯ", "StatusController", "Тики, стаки и усиления. События здоровья запускают Easy Feedbacks.");
            var recipe = Box(scroll, "guide-card recipe");
            Text(recipe, "Пример: огненное оружие", "card-title");
            Text(recipe, "1. В «Типах урона» создай Fire.\n2. Создай «Периодический урон»: тип Fire, урон за стак 3.\n3. Создай Burning: 4 секунды, тик 0.5 с, действие из шага 2.\n4. В оружии выбери Fire и добавь Burning в статусы попадания.\n5. На цели нужны Health + StatusController и Collider / Hurtbox.\n6. В Health Profile задай HP и Resistance Profile. Назначь его компоненту Health.", "guide-text");
            var links = Box(recipe, "row chips");
            foreach (var record in library)
                if (record.Asset.name == "FireLauncher" || record.Asset.name == "Burning" || record.Asset.name == "FireResistances")
                { var asset = record.Asset; Button(links, record.Title + "  ↗", () => SelectAsset(asset), "chip"); }
            var notes = Box(scroll, "row flow");
            GuideCard(notes, "ДАННЫЕ И ЛОГИКА", "Что можно без кода", "Новые типы урона, оружие, резисты, яд, горение и регенерация — новые SO. Новый алгоритм эффекта — подкласс StatusTickEffect. Новый ключ характеристики требует потребителя в коде.");
            GuideCard(notes, "СВЯЗИ", "Вариант или общий рецепт", "«Создать вариант» копирует выбранный SO. Вложенные типы, статусы и профили остаются общими. Для независимого статуса создай и назначь его отдельный вариант.");
            GuideCard(notes, "ЗАЩИТА", "100 × (1 − 0.5) = 50", "Каждая часть проходит свой резист. По умолчанию статусы требуют урона. OnContact и «Только статусы» работают без потери HP. Мёртвые цели не получают статусы; тики не запускают их повторно.");
            var composition = Box(scroll, "guide-card recipe");
            Text(composition, "Составной урон, AOE и журнал", "card-title");
            Text(composition, "В оружии нажми «Перевести в составной урон» и добавь строки Fire 2–4 / Poison 7–9. Каждая строка получает отдельный случайный урон; крит общий. Один удар запускает статусы и смерть один раз.\nДля взрыва назначь AreaDefinition оружию. Для непосредственной области выбери Delivery = Area. Длительные поля используют DamageArea + AreaPulseDefinition.\nCombatJournal хранит ограниченную историю. Вкладка «Журнал» показывает причину, части урона, иммунитеты, криты и убийства. Источник периодического урона сохраняется после уничтожения стрелка.", "guide-text");
            Text(scroll, "ПРОВЕРКА В ИГРЕ", "section-title");
            Text(scroll, "Полигон: 1–7 — оружие · 5 — огонь + яд · 6 — граната · 7 — конус статуса · T — периодическая область · H — лечение · R — сброс.\nHealth не уничтожает врага при смерти: реакцию подключает игровая логика. Изменения SO в Play Mode могут сохраняться в ассете — временные усиления храни в runtime.", "guide-text");
            Button(scroll, "Открыть подробное руководство", () => AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath("Assets/GamesFactory/Combat/README.md")), "guide-link");
        }
        private void BuildPerformance()
        {
            var scroll = new ScrollView(); scroll.AddToClassList("page"); body.Add(scroll);
            Text(scroll, "Плотный огонь требует отдельного бюджета", "page-title");
            Text(scroll, "Боевые правила подходят для Bullet Hell. Текущая доставка снарядов — базовый вариант, предел нагрузки ещё не подтверждён в WebGL.", "lead");
            var cards = Box(scroll, "row flow");
            GuideCard(cards, "УЖЕ ЕСТЬ", "Повторное использование", "Пул снарядов, буферы физических запросов, кэш резистов и один планировщик активных статусов.");
            GuideCard(cards, "ТЕКУЩАЯ ЦЕНА", "GO + Update на снаряд", "До двух 3D Physics-запросов за шаг: overlap и sweep. Лимит пула по умолчанию 128 активных снарядов; его можно настроить.");
            GuideCard(cards, "ДЛЯ ТЫСЯЧ ПУЛЬ", "Следующий слой доставки", "Общая симуляция, простые проверки попадания и пакетный рендер. Паттерны, кривые траектории и Physics2D пока не реализованы.");
            var panel = Box(scroll, "guide-card recipe");
            Text(panel, "Диагностика CPU в Editor", "card-title");
            Text(panel, "100 / 500 / 1000 снарядов, 30 шагов прогрева + 120 измерений на вариант. Редкая сцена с препятствиями без попаданий. Измеряется ручной вызов Simulate: без отрисовки, стоимости Unity Update, спавна, урона, VFX и UI. Это не FPS и не результат WebGL.", "guide-text");
            Button(panel, "Запустить замер симуляции", () => Act(() => { report = CombatSimulationBenchmark.Run(); body.Clear(); BuildPerformance(); }, "Замер готов. Отчёт: " + CombatSimulationBenchmark.ReportPath), "primary");
            if ((report == null || report.rows == null || report.rows.Count == 0) && File.Exists(CombatSimulationBenchmark.ReportPath))
            {
                try { report = JsonUtility.FromJson<CombatBenchmarkReport>(File.ReadAllText(CombatSimulationBenchmark.ReportPath)); }
                catch (Exception) { /* An old diagnostic should not prevent authoring. */ }
            }
            if (report != null && report.rows != null && report.rows.Count > 0)
            {
                Text(panel, report.device + "  ·  Unity " + report.unityVersion + "  ·  " + report.utcTime, "muted wrap");
                ResultRow(panel, "Снаряды", "Среднее / шаг", "P95 / шаг", "GC / 120 шагов");
                foreach (var row in report.rows)
                    ResultRow(panel, row.projectiles.ToString(), $"{row.meanMilliseconds:0.###} мс", $"{row.p95Milliseconds:0.###} мс", $"{row.allocatedBytes} B");
            }
            Text(scroll, "Для 60 FPS весь кадр должен уложиться в 16.67 мс. Проверяй реальную WebGL-сборку на целевых устройствах с рендером, столкновениями, звуком и эффектами. Нулевой GC в этом замере не означает нулевой GC всей игры.", "guide-text");
        }
        private void BuildJournal()
        {
            var panel = Box(body, "page grow");
            Text(panel, "Журнал боевых событий", "page-title");
            Text(panel, "Одна запись на цель за попадание. Части: входящий урон → после резиста → снятые HP. Переполненный буфер заменяет самые старые записи.", "lead");
            var controls = Box(panel, "row");
            var field = new ObjectField("Источник журнала") { objectType = typeof(CombatJournal), allowSceneObjects = true, value = journal };
            field.style.flexGrow = 1;
            field.RegisterValueChangedCallback(e => { journal = e.newValue as CombatJournal; journalRevision = -1; RefreshJournal(); });
            controls.Add(field);
            Button(controls, "Очистить буфер", () => { if (journal != null) journal.Clear(); RefreshJournal(); });
            journalSummary = Text(panel, "Запусти полигон: CombatJournal уже добавлен в сцену.", "muted wrap");
            journalView = new ListView { fixedItemHeight = 78, virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None, itemsSource = journalRows };
            journalView.AddToClassList("grow");
            journalView.makeItem = () => { var label = new Label(); label.AddToClassList("log-row"); return label; };
            journalView.bindItem = (element, index) => { ((Label)element).text = journalRows[index]; element.tooltip = journalRows[index]; };
            panel.Add(journalView);
            journalRevision = -1;
            panel.schedule.Execute(() =>
            {
                if (journal == null) { journal = UnityEngine.Object.FindFirstObjectByType<CombatJournal>(); field.SetValueWithoutNotify(journal); }
                RefreshJournal();
            }).Every(200);
        }
        private void RefreshJournal()
        {
            if (journalView == null || journalSummary == null) return;
            if (journal == null) { journalRows.Clear(); journalView.Rebuild(); journalSummary.text = "Добавь CombatJournal в сцену и запусти игру."; return; }
            if (journalRevision == journal.Revision) return;
            journalRevision = journal.Revision; journalRows.Clear();
            var damage = 0f; var shieldDamage = 0f; var kills = 0;
            for (var i = 0; i < journal.Count; i++)
            {
                var record = journal.GetNewest(i); damage += record.Result.AppliedAmount;
                shieldDamage += record.Result.ShieldAbsorbed;
                if (record.Result.Killed) kills++;
                journalRows.Add(EasyCombat.Presentation.CombatLogText.Format(record));
            }
            journalSummary.text = $"В буфере: {journal.Count}/{journal.Capacity}  ·  Снято HP: {damage:0.##}  ·  Щиты: {shieldDamage:0.##}  ·  Убийств: {kills}  ·  Обновление до 5 раз/с";
            journalView.Rebuild();
        }
        private void Act(Action action, string message = null)
        {
            try { action(); if (message != null) notice.text = message; notice.RemoveFromClassList("error"); }
            catch (Exception e) { notice.text = e.Message; notice.AddToClassList("error"); Debug.LogException(e); }
        }
        private static VisualElement Box(VisualElement parent, string classes)
        {
            var element = new VisualElement(); foreach (var item in classes.Split(' ')) element.AddToClassList(item); parent.Add(element); return element;
        }
        private static Label Text(VisualElement parent, string text, string classes)
        {
            var label = new Label(text); foreach (var item in classes.Split(' ')) label.AddToClassList(item); parent.Add(label); return label;
        }
        private static Button Button(VisualElement parent, string title, Action action, string classes = "")
        {
            var button = new Button(action) { text = title }; foreach (var item in classes.Split(' ')) if (item.Length > 0) button.AddToClassList(item); parent.Add(button); return button;
        }
        private static void Metric(VisualElement parent, string title, string value)
        {
            var metric = Box(parent, "metric grow"); Text(metric, title, "eyebrow"); Text(metric, value, "metric-value");
        }
        private static void GuideCard(VisualElement parent, string kicker, string title, string text)
        {
            var card = Box(parent, "guide-card grow"); Text(card, kicker, "eyebrow"); Text(card, title, "card-title"); Text(card, text, "guide-text");
        }
        private static void ResultRow(VisualElement parent, params string[] values)
        {
            var row = Box(parent, "row result-row"); foreach (var value in values) Text(row, value, "grow result-cell");
        }
    }
}
