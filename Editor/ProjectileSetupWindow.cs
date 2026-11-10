using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EasyCombat.Editor
{
    internal sealed class ProjectileSetupWindow : EditorWindow
    {
        private const string Folder = "Assets/EasyCombat/Resources";
        internal const string SettingsPath = Folder + "/" + ProjectileExecutionSettings.ResourceName + ".asset";
        private const string WelcomeSeen = "UserSettings/EasyCombatProjectileWelcome.seen";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!Application.isBatchMode && !AssetDatabase.IsAssetImportWorkerProcess())
                EditorApplication.update += ShowOnFirstImport;
        }

        private static void ShowOnFirstImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            EditorApplication.update -= ShowOnFirstImport;
            if (File.Exists(WelcomeSeen)) return;
            // An explicit team setting travels with the project.
            if (Resources.Load<ProjectileExecutionSettings>(ProjectileExecutionSettings.ResourceName) == null)
                Open();
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText(WelcomeSeen, "EasyCombat projectile setup has been shown in this project.");
        }

        [MenuItem("EasyCombat/Projectile Settings")]
        private static void Open()
        {
            var window = GetWindow<ProjectileSetupWindow>(true, "EasyCombat | Projectile Settings");
            window.minSize = new Vector2(444, 285);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(12);
            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(16, 16, 0, 12) });
            GUILayout.Label("Welcome to EasyCombat", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Choose how this project calculates projectile flight.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);
            DrawOption(ProjectileExecutionMode.Parallel, "Parallel",
                "Process straight projectile movement in batches across available job workers. Scheduling adds overhead to small volleys. Players without worker support use a single thread.");
            GUILayout.Space(6);
            DrawOption(ProjectileExecutionMode.SingleThread, "Single Thread",
                "Run the same flight calculations on the main thread. Useful for debugging and comparing CPU costs. Large volleys share frame time with gameplay.");
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Closing keeps the current mode. New projects default to Parallel. Reopen from EasyCombat → Projectile Settings.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawOption(ProjectileExecutionMode mode, string title, string description)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool current = ProjectileExecutionSettings.RequestedMode == mode;
                    GUILayout.Label(current ? title + " (current)" : title, EditorStyles.boldLabel);
                    if (GUILayout.Button("Use " + title, GUILayout.Width(132)))
                    {
                        SaveChoice(mode);
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            }
        }

        internal static void SaveChoice(ProjectileExecutionMode mode)
        {
            var settings = Resources.Load<ProjectileExecutionSettings>(ProjectileExecutionSettings.ResourceName);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/EasyCombat"))
                    AssetDatabase.CreateFolder("Assets", "EasyCombat");
                if (!AssetDatabase.IsValidFolder(Folder))
                    AssetDatabase.CreateFolder("Assets/EasyCombat", "Resources");
                if (AssetDatabase.LoadMainAssetAtPath(SettingsPath) != null)
                    throw new InvalidOperationException("EasyCombat projectile settings path is already occupied: " + SettingsPath);
                settings = CreateInstance<ProjectileExecutionSettings>();
                settings.mode = mode;
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.mode = mode;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            ProjectileExecutionSettings.Reload();
        }
    }
}
