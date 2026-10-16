using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    public sealed class CombatRuntimeInspectorWindow:EditorWindow
    {
        private Health target,bound;
        private CombatStatDefinition stat;
        private DamageTypeDefinition type;
        private CombatRecord latest;
        private bool hasHit;
        private Vector2 scroll;
        private double nextRepaint;
        [MenuItem("EasyCombat/Tools/Runtime Explanation")]
        public static void Open() { var window=GetWindow<CombatRuntimeInspectorWindow>(); window.titleContent=new GUIContent("Combat Runtime"); window.minSize=new Vector2(570,470); window.Show(); }
        private void OnEnable()=>EditorApplication.update+=Tick;
        private void OnDisable() { EditorApplication.update-=Tick; Bind(null); }
        private void Tick() { if(EditorApplication.timeSinceStartup<nextRepaint) return; nextRepaint=EditorApplication.timeSinceStartup+.1; Repaint(); }
        private void Bind(Health actor)
        { if(bound!=null) bound.Resolved-=Resolved; bound=actor; if(bound!=null) bound.Resolved+=Resolved; hasHit=false; }
        private void Resolved(CombatRecord record) { if(bound!=null && record.Target.Id==bound.Identity.Id) { latest=record; hasHit=true; } }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Состояние и причины результата",EditorStyles.boldLabel);
            target=(Health)EditorGUILayout.ObjectField("Участник",target,typeof(Health),true);
            if(GUILayout.Button("Взять из выделения") && Selection.activeGameObject!=null) target=Selection.activeGameObject.GetComponentInParent<Health>();
            if(target!=bound) Bind(target); if(target==null) return;
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField($"{target.LifeState} · HP {target.Current:0.##}/{target.Maximum:0.##} · Щит {target.ShieldCurrent:0.##}/{target.ShieldMaximum:0.##}");
            EditorGUILayout.LabelField($"Жизнь #{target.Identity.Id} · Неуязвимость: {target.Invulnerable}, таймер {target.InvulnerabilityRemaining:0.###} с");
            type=(DamageTypeDefinition)EditorGUILayout.ObjectField("Проверить сопротивление",type,typeof(DamageTypeDefinition),false);
            if(type!=null) EditorGUILayout.LabelField($"Итоговое сопротивление: {target.GetResistance(type)*100:0.##}% (отрицательное = уязвимость)");
            stat=(CombatStatDefinition)EditorGUILayout.ObjectField("Объяснить характеристику",stat,typeof(CombatStatDefinition),false);
            var stats=target.GetComponent<CombatStats>();
            if(stat!=null && stats!=null && stats.TryExplain(stat,out var value))
                EditorGUILayout.HelpBox($"max(0, {value.Base:0.###} + {value.Flat:0.###}) × max(0, 1 + {value.AdditivePercent:0.###}) × {value.Multiplier:0.###} × статусы {value.StatusMultiplier:0.###} = {value.Value:0.###}",MessageType.Info);
            var resources=target.GetComponent<CombatResources>();
            if(resources!=null) for(var i=0;i<resources.Count;i++) { var resource=resources.GetResource(i); EditorGUILayout.LabelField($"{(resource.Definition!=null ? resource.Definition.name : "Утраченный ресурс")}: {resource.Current:0.##}/{resource.Maximum:0.##} · задержка {resource.RechargeDelay:0.##}"); }
            if(target.Defense!=null) for(var i=0;i<target.Defense.Count;i++) { var state=target.Defense.GetState(i); var layer=target.Defense.GetLayer(i); EditorGUILayout.LabelField($"Слой {i}: {(layer!=null ? layer.name : "утрачен")} · {state.Current:0.##}/{state.Maximum:0.##} · таймер {state.RechargeDelay:0.##} · {state.Enabled}"); }
            var statuses=target.GetComponent<StatusController>();
            if(statuses!=null)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField($"Статусы: {statuses.Count} · Догоняющих записей: {statuses.OverdueTickRecords} · Ограничения: {statuses.Restrictions}",EditorStyles.boldLabel);
                for(var i=0;i<statuses.Count;i++)
                {
                    var view=statuses.GetStatus(i); EditorGUILayout.LabelField($"{(view.Definition!=null ? view.Definition.Label : "Утраченный статус")}: {view.Stacks} · ещё {view.Remaining:0.##} с · ближайший стак {view.NextStackExpiresIn:0.##} с · догоняющих записей {view.PendingStacks}");
                    if(view.Stacks>0 && statuses.TryGetStackDamage(view.Definition,0,0,out var damage)) EditorGUILayout.LabelField($"Первый стак / первый эффект: следующий урон до защиты {damage:0.###}",EditorStyles.miniLabel);
                }
            }
            var abilities=target.GetComponent<CombatAbilities>();
            if(abilities!=null) for(var i=0;i<abilities.Count;i++) { var ability=abilities.GetAbility(i); EditorGUILayout.LabelField($"{(ability.Definition!=null ? ability.Definition.name : "Утраченная способность")}: заряды {ability.Charges}, CD {ability.Cooldown:0.##}, заряд через {ability.RechargeRemaining:0.##}"); }
            var buildup=target.GetComponent<CombatBuildup>();
            if(buildup!=null) for(var i=0;i<buildup.Count;i++) { var meter=buildup.GetMeter(i); EditorGUILayout.LabelField($"{(meter.Definition!=null ? meter.Definition.name : "Утраченная шкала")}: {meter.Value:0.##}/{meter.Threshold:0.##} · CD {meter.Cooldown:0.##}"); }
            if(hasHit)
            {
                var result=latest.Result; EditorGUILayout.Space(); EditorGUILayout.LabelField("Последнее попадание",EditorStyles.boldLabel);
                EditorGUILayout.HelpBox($"{result.Rejection} · вход {result.Damage.Amount:0.##} → HP −{result.AppliedAmount:0.##}\nЩит {result.ShieldAbsorbed:0.##}, броня {result.ArmorPrevented:0.##}, правила жизни {result.LifePrevented:0.##}, перенаправление {result.Redirected:0.##}, overkill {result.Overkill:0.##}\nКрит: {result.Damage.IsCritical} · тик: {result.Damage.IsPeriodic} · статусов применено: {result.StatusesApplied}",MessageType.Info);
                for(var i=0;i<result.Breakdown.Count;i++) { var part=result.Breakdown[i]; EditorGUILayout.LabelField($"{(part.Type!=null ? part.Type.Label : "Без типа")}: вход {part.Incoming:0.##}, после резистов {part.Effective:0.##}, снято HP {part.Applied:0.##}"); }
                for(var i=0;i<result.Defenses.Count;i++) { var layer=result.Defenses[i]; EditorGUILayout.LabelField($"{layer.Name}: предотвратил {layer.Prevented:0.##}, остаток слоя {layer.Remaining:0.##}"); }
                EditorGUILayout.LabelField($"Root {result.Damage.Origin.Action.RootId} · parent {result.Damage.Origin.Action.ParentId} · reaction depth {result.Damage.Origin.Action.ReactionDepth}",EditorStyles.miniLabel);
            }
            else EditorGUILayout.HelpBox("Окно слушает только выбранного участника. Новое попадание покажет пошаговый результат.",MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
    }
}
