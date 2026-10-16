using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EasyCombat.Editor
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class CombatBenchmarkRow
    {
        public int projectiles, steps, queriesPerStep;
        public double meanMilliseconds, p95Milliseconds;
        public long allocatedBytes;
    }
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public sealed class CombatBenchmarkReport
    {
        public string unityVersion, device, utcTime, scope;
        public List<CombatBenchmarkRow> rows = new List<CombatBenchmarkRow>();
    }
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat.Editor", "GamesFactory.Combat.Editor")]
    public static class CombatSimulationBenchmark
    {
        public const string ReportPath = "Temp/combat-projectile-benchmark.json";
        public static CombatBenchmarkReport LastReport { get; private set; }
        public static CombatBenchmarkReport Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Run the diagnostic outside Play mode after compilation.");
            var report = new CombatBenchmarkReport
            {
                unityVersion = Application.unityVersion, device = SystemInfo.processorType,
                utcTime = DateTime.UtcNow.ToString("O"),
                scope = "Editor, manual 1/60 simulation steps, sparse static collision scene, no rendering/input/effects. Not frame time or a WebGL benchmark."
            };
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var roots = new List<GameObject>();
            AttackDefinition attack = null;
            try
            {
                SceneManager.SetActiveScene(scene);
                attack = ScriptableObject.CreateInstance<AttackDefinition>();
                attack.Delivery = DamageDelivery.Projectile;
                attack.ProjectileSpeed = 8;
                attack.ProjectileLifetime = 10000;
                attack.Range = 1000000;
                attack.ProjectileRadius = .05f;
                // Keep this fixture far from the user's scene, in the same default physics world.
                var offset = new Vector3(0, -10000, 0);
                for (var i = 0; i < 32; i++)
                {
                    var wall = new GameObject("Benchmark obstacle");
                    roots.Add(wall);
                    wall.transform.position = offset + new Vector3(i % 2 == 0 ? -8f : 8f, 0, i * 2);
                    wall.AddComponent<BoxCollider>().size = new Vector3(1, 3, 1);
                }
                Physics.SyncTransforms();
                foreach (var count in new[] { 100, 500, 1000 })
                {
                    var shots = new DamageProjectile[count];
                    for (var i = 0; i < count; i++)
                    {
                        var go = new GameObject("Benchmark projectile") { hideFlags = HideFlags.DontSave };
                        roots.Add(go);
                        var projectile = go.AddComponent<DamageProjectile>();
                        projectile.Launch(offset + new Vector3((i % 40 - 20) * .2f, 0, -(i / 40) * .2f),
                            Vector3.forward, new DamageInfo(1), null, attack, null, IgnoreRelease);
                        shots[i] = projectile;
                    }
                    for (var warmup = 0; warmup < 30; warmup++)
                        for (var i = 0; i < shots.Length; i++) shots[i].Simulate(1f / 60f);
                    const int steps = 120;
                    var samples = new double[steps];
                    var totalTicks = 0L;
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    for (var step = 0; step < steps; step++)
                    {
                        var start = Stopwatch.GetTimestamp();
                        for (var i = 0; i < shots.Length; i++) shots[i].Simulate(1f / 60f);
                        var elapsed = Stopwatch.GetTimestamp() - start;
                        samples[step] = elapsed * 1000d / Stopwatch.Frequency;
                        totalTicks += elapsed;
                    }
                    var allocation = GC.GetAllocatedBytesForCurrentThread() - before;
                    foreach (var shot in shots)
                        if (!shot.IsFlying) throw new InvalidOperationException("An unexpected collider intersected the diagnostic area. Measurement discarded.");
                    Array.Sort(samples);
                    report.rows.Add(new CombatBenchmarkRow
                    {
                        projectiles = count, steps = steps, queriesPerStep = count * 2,
                        meanMilliseconds = totalTicks * 1000d / Stopwatch.Frequency / steps,
                        p95Milliseconds = samples[(int)Math.Ceiling(steps * .95d) - 1], allocatedBytes = allocation
                    });
                    foreach (var shot in shots)
                    {
                        Object.DestroyImmediate(shot.gameObject);
                    }
                }
            }
            finally
            {
                foreach (var root in roots)
                    if (root != null) Object.DestroyImmediate(root);
                if (attack != null) Object.DestroyImmediate(attack);
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            LastReport = report;
            Directory.CreateDirectory("Temp");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            return report;
        }
        // Temporary projectiles are disposed explicitly with DestroyImmediate in the fixture.
        private static void IgnoreRelease(DamageProjectile _) { }
    }
}
