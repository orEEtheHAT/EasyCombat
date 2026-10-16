using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    public sealed class CombatCalculatorWindow:EditorWindow
    {
        [SerializeField] private CombatScenarioDefinition scenario;
        private UnityEditor.Editor inspector;
        private Vector2 settingsScroll,resultScroll;
        private CombatSimulationReport report;
        private bool showRecords;
        [MenuItem("EasyCombat/Tools/Battle Calculator")]
        public static void Open() { var window=GetWindow<CombatCalculatorWindow>(); window.titleContent=new GUIContent("Combat Calculator"); window.minSize=new Vector2(900,560); window.Show(); }
        public static void Open(CombatScenarioDefinition value) { Open(); var window=GetWindow<CombatCalculatorWindow>(); window.scenario=value; window.RebuildEditor(); }
        private void OnDisable() { if(inspector!=null) DestroyImmediate(inspector); }
        private void RebuildEditor() { if(inspector!=null) DestroyImmediate(inspector); inspector=scenario!=null ? UnityEditor.Editor.CreateEditor(scenario) : null; }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Калькулятор боя",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Прямые пакеты в одну цель: формулы, защита, статусы и реакции. Доставка основного оружия не моделируется. Расчёт идёт в отдельном PhysicsScene и не попадает в общий журнал сцены.",MessageType.Info);
            var next=(CombatScenarioDefinition)EditorGUILayout.ObjectField("Сценарий",scenario,typeof(CombatScenarioDefinition),false);
            if(next!=scenario) { scenario=next; RebuildEditor(); }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Создать сценарий"))
                {
                    var path=EditorUtility.SaveFilePanelInProject("Сценарий боя","BattleScenario","asset","Новый сценарий");
                    if(!string.IsNullOrEmpty(path)) { scenario=CreateInstance<CombatScenarioDefinition>(); AssetDatabase.CreateAsset(scenario,AssetDatabase.GenerateUniqueAssetPath(path)); Selection.activeObject=scenario; RebuildEditor(); }
                }
                using(new EditorGUI.DisabledScope(scenario==null || !scenario.IsValid || EditorApplication.isPlayingOrWillChangePlaymode))
                    if(GUILayout.Button("Рассчитать",GUILayout.Height(27))) { report=CombatBattleSimulation.Run(scenario); GUI.FocusControl(null); }
                using(new EditorGUI.DisabledScope(report==null || !report.Success))
                    if(GUILayout.Button("Копировать результат")) EditorGUIUtility.systemCopyBuffer=Format(report);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(position.width*.47f)))
                {
                    settingsScroll=EditorGUILayout.BeginScrollView(settingsScroll);
                    if(scenario!=null) { if(inspector==null || inspector.target!=scenario) RebuildEditor(); inspector.OnInspectorGUI(); }
                    else EditorGUILayout.LabelField("Создай или выбери SO-сценарий.",EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndScrollView();
                }
                using(new EditorGUILayout.VerticalScope())
                {
                    resultScroll=EditorGUILayout.BeginScrollView(resultScroll);
                    if(report!=null)
                    {
                        if(!report.Success) EditorGUILayout.HelpBox(report.Error ?? "Расчёт не выполнен.",MessageType.Error);
                        else
                        {
                            if(report.Truncated) EditorGUILayout.HelpBox("Достигнут бюджет вычисления; показан частичный результат.",MessageType.Warning);
                            if(report.OverdueStatusRecords>0) EditorGUILayout.HelpBox($"Остались догоняющие тики у {report.OverdueStatusRecords} записей статусов. Уменьши шаг расчёта или увеличь окно наблюдения.",MessageType.Warning);
                            EditorGUILayout.LabelField(Format(report),EditorStyles.wordWrappedLabel);
                            DrawTimeline(GUILayoutUtility.GetRect(200,155,GUILayout.ExpandWidth(true)),report);
                            EditorGUILayout.LabelField("Зелёный — HP, синий — щит",EditorStyles.miniLabel);
                            showRecords=EditorGUILayout.Foldout(showRecords,$"Попадания: {report.Records.Count} из {report.ResolvedHits}",true);
                            if(showRecords)
                            {
                                var start=Math.Max(0,report.Records.Count-100);
                                for(var i=start;i<report.Records.Count;i++)
                                {
                                    var record=report.Records[i]; var hit=record.Result;
                                    EditorGUILayout.LabelField($"{record.Time:0.###} с  {(hit.Damage.IsPeriodic ? "тик" : "удар")}  HP −{hit.AppliedAmount:0.##}  щит {hit.ShieldAbsorbed:0.##}  {hit.Rejection}",EditorStyles.miniLabel);
                                }
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        public static string Format(CombatSimulationReport value)
        {
            if(value==null) return ""; var text=new StringBuilder();
            text.AppendLine(value.Truncated ? "Частичный расчёт" : "Расчёт завершён");
            text.AppendLine($"Время: {value.Seconds:0.###} с · Выпущено ударов: {value.Shots} · Отказов: {value.RejectedHits}");
            text.AppendLine($"Потеря HP: {value.HealthDamage:0.##} · Щитом поглощено: {value.ShieldAbsorbed:0.##}");
            text.AppendLine($"Броня/блок: {value.ArmorPrevented:0.##} · Резисты (− = уязвимость): {value.ResistanceDelta:0.##}");
            text.AppendLine($"Правила жизни: {value.LifePrevented:0.##} · Перенаправлено: {value.Redirected:0.##} · Overkill: {value.Overkill:0.##}");
            text.AppendLine($"Осталось у цели: HP {value.TargetHealth:0.##}, щит {value.TargetShield:0.##} · HP источника: {value.SourceHealth:0.##}");
            if(value.Seconds>0) text.AppendLine("Средняя потеря HP/с: "+(value.HealthDamage/value.Seconds).ToString("0.##",CultureInfo.InvariantCulture));
            text.AppendLine(value.Killed ? $"Цель погибла на {value.TimeToKill:0.###} с" : "Цель жива"); return text.ToString();
        }
        private static void DrawTimeline(Rect rect,CombatSimulationReport report)
        {
            EditorGUI.DrawRect(rect,new Color(.1f,.12f,.15f)); if(Event.current.type!=EventType.Repaint || report.Timeline.Count<2) return;
            var maximum=1f; foreach(var point in report.Timeline) maximum=Mathf.Max(maximum,point.Health,point.Shield);
            var duration=Mathf.Max(.001f,(float)report.Seconds); var points=new Vector3[report.Timeline.Count];
            Handles.BeginGUI();
            for(var series=0;series<2;series++)
            {
                for(var i=0;i<points.Length;i++) { var point=report.Timeline[i]; points[i]=new Vector3(rect.x+rect.width*point.Time/duration,rect.yMax-rect.height*(series==0 ? point.Health : point.Shield)/maximum); }
                Handles.color=series==0 ? new Color(.3f,.9f,.6f) : new Color(.35f,.7f,1f); Handles.DrawAAPolyLine(2,points);
            }
            Handles.EndGUI();
        }
    }
    [CustomEditor(typeof(CombatScenarioDefinition))]
    public sealed class CombatScenarioEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Shot Interval заменяет RPM оружия для этого опыта. Один фиксированный seed даёт воспроизводимый сценарий, не статистическое среднее всех исходов. Allow Custom Behaviors включает пользовательский код, который обязан работать только с переданными участниками/контекстом.",MessageType.Info);
            if(!((CombatScenarioDefinition)target).IsValid) EditorGUILayout.HelpBox("Нужны корректные Attack, Target Health и сроки.",MessageType.Error);
        }
    }
}
