using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    [CustomEditor(typeof(Health))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class HealthEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var health = (Health)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying)) DrawDefaultInspector();
            if (health.Definition != null)
                EditorGUILayout.HelpBox("Начальное здоровье и резисты берутся из Health Profile. Runtime-изменения здоровья не изменяют SO.", MessageType.Info);
            if (!Application.isPlaying) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", $"{health.Current:0.##} / {health.Maximum:0.##}");
            EditorGUILayout.LabelField("State", health.IsAlive ? "Alive" : "Dead");
            EditorGUILayout.LabelField("Неуязвимость (с)", health.InvulnerabilityRemaining.ToString("0.##"));
            health.Invulnerable = EditorGUILayout.Toggle("Принудительная неуязвимость", health.Invulnerable);
            if (health.Defense != null)
            {
                EditorGUILayout.LabelField("Щиты", $"{health.ShieldCurrent:0.##} / {health.ShieldMaximum:0.##}");
                for (var i = 0; i < health.Defense.Count; i++)
                {
                    var layer = health.Defense.GetLayer(i); var state = health.Defense.GetState(i);
                    if (layer == null) continue;
                    var enabled = EditorGUILayout.ToggleLeft($"{i}: {layer.name}" + (layer.IsShield ? $" · {state.Current:0.##}/{state.Maximum:0.##} · задержка {state.RechargeDelay:0.##} с" : ""), state.Enabled);
                    if (enabled != state.Enabled) health.SetDefenseLayerEnabled(i, enabled);
                    if (layer.IsShield && GUILayout.Button($"Восстановить щит {i}")) health.RestoreShield(i, state.Maximum);
                }
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Damage 25")) health.ApplyDamage(new DamageInfo(25, null, health.transform.position));
            if (GUILayout.Button("Heal 25")) health.Heal(25);
            if (GUILayout.Button("Reset")) health.ResetToFull();
            EditorGUILayout.EndHorizontal();
            Repaint();
        }
    }
}
