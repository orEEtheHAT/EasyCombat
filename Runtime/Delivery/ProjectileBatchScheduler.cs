using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace EasyCombat
{
    // Only independent flight data crosses the Burst job boundary. Damage callbacks stay ordered.
    internal struct ProjectileFlight
    {
        public Vector3 Direction;
        public float Speed, Range, Lifetime;

        public ProjectileStep Plan(float deltaTime)
        {
            float distance = Mathf.Min(Range, Speed * Mathf.Min(deltaTime, Lifetime));
            return new ProjectileStep
            {
                Distance = distance,
                Offset = Direction * distance,
                RangeLeft = Range - distance,
                LifetimeLeft = Lifetime - deltaTime
            };
        }
    }

    internal struct ProjectileStep
    {
        public float Distance, RangeLeft, LifetimeLeft;
        public Vector3 Offset;
    }

    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct ProjectileFlightJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ProjectileFlight> Flights;
        [WriteOnly] public NativeArray<ProjectileStep> Steps;
        public NativeArray<OverlapCapsuleCommand> Probes;
        public bool PrepareProbes;
        public float DeltaTime;
        public void Execute(int index)
        {
            var step = Flights[index].Plan(DeltaTime);
            Steps[index] = step;
            if (!PrepareProbes) return;
            var probe = Probes[index];
            probe.point1 = probe.point0 + step.Offset;
            Probes[index] = probe;
        }
    }

    internal interface IBatchedProjectile
    {
        int BatchIndex { get; set; }
        bool TryCaptureFlight(out ProjectileFlight flight);
        void CommitFlight(in ProjectileStep step);
        void Simulate(float deltaTime);
    }

    [AddComponentMenu("")]
    public sealed class ProjectileBatchScheduler : MonoBehaviour
    {
        private struct Entry
        {
            internal IBatchedProjectile Shot;
            internal int PlanIndex;
        }

        private static ProjectileBatchScheduler instance;
        private static bool quitting;
        private readonly List<Entry> entries = new List<Entry>(128);
        private NativeArray<ProjectileFlight> flights;
        private NativeArray<ProjectileStep> steps;
        private NativeArray<OverlapCapsuleCommand> probes;
        private NativeArray<ColliderHit> probeHits;
        private bool advancing, needsCompaction;
        // A scheduling hint only. Results from a previous physics query are never used to resolve a hit.
        private int livePrefix;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            instance = null;
            quitting = false;
        }

        internal static void Register(IBatchedProjectile shot)
        {
            if (!Application.isPlaying || quitting || shot.BatchIndex >= 0) return;
            if (instance == null)
            {
                var go = new GameObject("EasyCombat Projectile Scheduler");
                instance = go.AddComponent<ProjectileBatchScheduler>();
                DontDestroyOnLoad(go);
            }
            shot.BatchIndex = instance.entries.Count;
            instance.entries.Add(new Entry { Shot = shot, PlanIndex = -1 });
        }

        internal static void Unregister(IBatchedProjectile shot)
        {
            int index = shot.BatchIndex;
            shot.BatchIndex = -1;
            if (index < 0 || instance == null) return;
            instance.entries[index] = default;
            instance.needsCompaction = true;
        }

        // A contact listener may explicitly advance another shot before its turn.
        internal static void InvalidatePlan(IBatchedProjectile shot)
        {
            int index = shot.BatchIndex;
            if (index >= 0 && instance != null && instance.advancing)
                instance.entries[index] = new Entry { Shot = shot, PlanIndex = -1 };
        }

        private void Update()
        {
            if (entries.Count != 0) Advance(Time.deltaTime, ProjectileExecutionSettings.EffectiveMode);
        }

        internal void Advance(float deltaTime, ProjectileExecutionMode mode)
        {
#if EASYCOMBAT_CPU_CAPTURE
            using var sample = CombatCpuMarkers.ProjectileBatch.Auto();
#endif
            if (advancing || !CombatMath.Finite(deltaTime) || deltaTime < 0) return;
            Compact();
            int count = entries.Count;
            if (count == 0) { livePrefix = 0; return; }
            advancing = true;
            try
            {
                bool parallelFlight = mode == ProjectileExecutionMode.Parallel && deltaTime > 0;
                int first = parallelFlight ? AdvanceLeadingShots(count, deltaTime) : 0;
                if (first == count) return;
                // Re-read scene settings after any callbacks from the leading shots.
#pragma warning disable CS0618
                bool canProbe = parallelFlight && !Physics.autoSyncTransforms;
#pragma warning restore CS0618
                EnsureCapacity(count - first);
                int planned = 0;
                int runStart = first, probeStart = first, probeCount = 0;
                var scene = CombatPhysics.Scene;
                for (int i = first; i < count; i++)
                {
                    var entry = entries[i];
                    if (entry.Shot == null) { runStart = i + 1; continue; }
                    entry.PlanIndex = -1;
                    if (deltaTime > 0 && entry.Shot.TryCaptureFlight(out var flight))
                    {
                        entry.PlanIndex = planned;
                        if (canProbe && entry.Shot is DamageProjectile shot)
                        {
                            int runLength = i - runStart + 1;
                            if (runLength > probeCount) { probeStart = runStart; probeCount = runLength; }
                            if (runStart == first) probes[planned] = shot.CreateProbe(scene);
                        }
                        else runStart = i + 1;
                        flights[planned++] = flight;
                    }
                    else runStart = i + 1;
                    entries[i] = entry;
                }
                bool useProbes = probeCount > 0 && probeCount == count - first;
                bool probeMixedRun = !useProbes && probeCount >= 32;
                if (planned > 0)
                {
                    var job = new ProjectileFlightJob { Flights = flights, Steps = steps, DeltaTime = deltaTime, Probes = probes, PrepareProbes = useProbes };
                    if (mode == ProjectileExecutionMode.Parallel)
                    {
                        var commands = probes.GetSubArray(0, planned);
                        var results = probeHits.GetSubArray(0, planned);
                        var handle = job.Schedule(planned, 64);
                        try
                        {
                            if (useProbes)
                                handle = OverlapCapsuleCommand.ScheduleBatch(commands, results, 32, 1, handle);
                        }
                        finally { handle.Complete(); }
                    }
                    else job.Run(planned);
                }
                // Process in launch order. Removals leave tombstones and newly launched shots wait one tick.
                for (int i = first; i < count; i++)
                {
                    // Capture the longest straight run only after older guided shots and their callbacks.
                    // A live contact inside the run invalidates the remaining speculative results.
                    if (probeMixedRun && i == probeStart)
                    {
                        probeCount = ProbeStraightRun(probeStart, probeCount);
                        useProbes = probeCount > 0;
                    }
                    if (i >= probeStart + probeCount) useProbes = false;
                    var entry = entries[i];
                    if (entry.Shot == null) continue;
                    try
                    {
                        int probeIndex = i - probeStart;
                        if (useProbes && entry.PlanIndex >= 0 && probeHits[probeIndex].instanceID == 0
                            && entry.Shot is DamageProjectile emptyShot && emptyShot.IsAtProbeStart(probes[probeIndex].point0))
                        {
                            useProbes = emptyShot.CommitEmpty(steps[entry.PlanIndex], probes[probeIndex].point0);
                            continue;
                        }
                        // From the first live resolution on, a callback can move, spawn or recycle any collider.
                        // Requery the remaining shots against the live scene, including initial overlaps.
                        if (useProbes) RememberContactRegion(probeStart, probeCount);
                        useProbes = false;
                        if (entry.PlanIndex >= 0) entry.Shot.CommitFlight(steps[entry.PlanIndex]);
                        else entry.Shot.Simulate(deltaTime);
                    }
                    catch (Exception e) { useProbes = false; Debug.LogException(e, entry.Shot as UnityEngine.Object); }
                }
            }
            finally
            {
                advancing = false;
                Compact();
                if (!isActiveAndEnabled) ReleaseBuffers();
            }
        }

        private int ProbeStraightRun(int start, int length)
        {
#pragma warning disable CS0618
            if (Physics.autoSyncTransforms) return 0;
#pragma warning restore CS0618
            var scene = CombatPhysics.Scene;
            int count = 0;
            for (; count < length; count++)
            {
                var entry = entries[start + count];
                if (entry.PlanIndex < 0 || !(entry.Shot is DamageProjectile shot)) break;
                var command = shot.CreateProbe(scene);
                command.point1 = command.point0 + steps[entry.PlanIndex].Offset;
                probes[count] = command;
            }
            if (count < 32) return 0;
            OverlapCapsuleCommand.ScheduleBatch(probes.GetSubArray(0,count), probeHits.GetSubArray(0,count), 32, 1).Complete();
            return count;
        }

        private void RememberContactRegion(int first, int planned)
        {
            for (int i = planned - 1; i >= 0; i--)
            {
                if (probeHits[i].instanceID == 0) continue;
                livePrefix = first + i + 1;
                return;
            }
        }

        // The oldest shots often arrive together. Finish their callbacks before sampling the
        // physics scene for the remaining volley. Try last step's contact region and follow
        // consecutive retirements. If firing stops or the targets move away the prefix shrinks again.
        private int AdvanceLeadingShots(int count, float deltaTime)
        {
            int first = 0;
            int predicted = livePrefix;
            livePrefix = 0;
            while (first < count)
            {
                int index = first++;
                var shot = entries[index].Shot;
                if (shot == null) continue;
                if (!(shot is DamageProjectile)) return index;
                try { shot.Simulate(deltaTime); }
                catch (Exception e) { Debug.LogException(e, shot as UnityEngine.Object); }
                if (entries[index].Shot == null) livePrefix = first;
                else if (first >= predicted) break;
            }
            return first;
        }

        private void Compact()
        {
            if (!needsCompaction) return;
            int write = 0;
            for (int read = 0; read < entries.Count; read++)
            {
                var entry = entries[read];
                if (entry.Shot == null) continue;
                entry.Shot.BatchIndex = write;
                if (read != write) entries[write] = entry;
                write++;
            }
            entries.RemoveRange(write, entries.Count - write);
            if (write == 0) livePrefix = 0;
            needsCompaction = false;
        }

        private void EnsureCapacity(int count)
        {
            if (flights.IsCreated && flights.Length >= count) return;
            ReleaseBuffers();
            int capacity = Mathf.NextPowerOfTwo(Mathf.Max(64, count));
            flights = new NativeArray<ProjectileFlight>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            steps = new NativeArray<ProjectileStep>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            probes = new NativeArray<OverlapCapsuleCommand>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            probeHits = new NativeArray<ColliderHit>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private void ReleaseBuffers()
        {
            if (flights.IsCreated) flights.Dispose();
            if (steps.IsCreated) steps.Dispose();
            if (probes.IsCreated) probes.Dispose();
            if (probeHits.IsCreated) probeHits.Dispose();
        }

        private void OnApplicationQuit() => quitting = true;
        private void OnDisable()
        {
            if (!advancing) ReleaseBuffers();
        }
        private void OnDestroy()
        {
            if (instance != this) return;
            foreach (var entry in entries)
                if (entry.Shot != null) entry.Shot.BatchIndex = -1;
            instance = null;
        }
    }
}
