using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>One scaled clock per overlap group (and, optionally, source life). Selects before applying any hit.</summary>
    [AddComponentMenu("")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class AreaOverlapScheduler : MonoBehaviour
    {
        private struct Lease
        {
            public DamageArea Area;
            public long Id;
            public int Priority;
            public double Start, End;
            public bool Active => Area != null && Area.IsRunning && Area.UsesSharedClock && Area.OverlapLeaseId == Id;
        }
        private struct Winner { public Lease Lease; public AreaCandidate Candidate; }
        private sealed class Group
        {
            public AreaOverlapGroupDefinition Definition;
            public AreaOverlapMode Mode;
            public long Source;
            public double Next, Interval;
            public readonly List<Lease> Leases = new List<Lease>(16);
            public readonly List<Winner> Winners = new List<Winner>(32);
            public readonly Dictionary<MonoBehaviour, int> Indices = new Dictionary<MonoBehaviour, int>(32);
            public void Clear() { Definition = null; Leases.Clear(); Winners.Clear(); Indices.Clear(); }
        }
        private static AreaOverlapScheduler instance;
        private static bool quitting;
        private readonly List<Group> groups = new List<Group>(8);
        private readonly Stack<Group> pool = new Stack<Group>(8);
        private double clock;
        private long sequence;
        private bool advancing, clearing;
        private const double Epsilon = 1e-7;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; quitting = false; }
        private void OnApplicationQuit() => quitting = true;

        internal static bool Register(DamageArea area, in AreaPulseSnapshot snapshot)
        {
            if (quitting) return false;
            if (instance == null)
            {
                var host = new GameObject("Combat Area Overlap Scheduler") { hideFlags = HideFlags.DontSave };
                instance = host.AddComponent<AreaOverlapScheduler>();
                if (Application.isPlaying) DontDestroyOnLoad(host);
            }
            if (instance.clearing) return false;
            var source = snapshot.OverlapMode == AreaOverlapMode.OnePerSource ? snapshot.Payload.Origin.Actor.Id : 0;
            Group group = null;
            foreach (var item in instance.groups)
                if (item.Definition == snapshot.OverlapGroup && item.Mode == snapshot.OverlapMode && item.Source == source)
                { group = item; break; }
            if (group == null)
            {
                group = instance.pool.Count > 0 ? instance.pool.Pop() : new Group();
                group.Definition = snapshot.OverlapGroup; group.Mode = snapshot.OverlapMode; group.Source = source;
                group.Interval = snapshot.Interval;
                group.Next = instance.clock + (snapshot.ApplyImmediately ? 0 : snapshot.Interval);
                instance.groups.Add(group);
            }
            area.OverlapLeaseId = ++instance.sequence;
            group.Leases.Add(new Lease { Area = area, Id = area.OverlapLeaseId, Priority = snapshot.Priority,
                Start = instance.clock, End = instance.clock + snapshot.Duration });
            return true;
        }
        private void LateUpdate() { if (Time.deltaTime > 0) AdvanceSimulation(Time.deltaTime); }

        /// <summary>For manual simulations advance all exclusive areas once, rather than advancing each DamageArea.</summary>
        public static void AdvanceSimulation(float deltaTime)
        {
            if (instance == null || instance.advancing || instance.clearing || !CombatMath.Finite(deltaTime) || deltaTime < 0) return;
            instance.Advance(deltaTime);
        }
        private void Advance(float deltaTime)
        {
            advancing = true;
            clock += deltaTime;
            // Registrations from damage callbacks are considered on the next call.
            var count = groups.Count;
            var lastLease = sequence;
            try
            {
                for (var g = 0; g < count && !clearing; g++)
                {
                    var group = groups[g];
                    for (var i = group.Leases.Count - 1; i >= 0; i--)
                        if (!group.Leases[i].Active) group.Leases.RemoveAt(i);
                    var ticks = 0;
                    while (!clearing && group.Leases.Count > 0 && group.Next <= clock + Epsilon && ticks++ < 8)
                    {
                        var at = group.Next;
                        group.Next += group.Interval; // Commit before callbacks, including Stop/Begin and pooled reuse.
                        Pulse(group, at, lastLease);
                    }
                    if (clearing) return;
                    for (var i = group.Leases.Count - 1; i >= 0; i--)
                    {
                        var lease = group.Leases[i];
                        if (!lease.Active) { group.Leases.RemoveAt(i); continue; }
                        lease.Area.SetOverlapAge(clock - lease.Start);
                        if (clock + Epsilon >= lease.End && group.Next > lease.End + Epsilon)
                        {
                            group.Leases.RemoveAt(i);
                            lease.Area.Stop();
                        }
                    }
                }
                // Retain an empty group's clock until its next tick, so re-entering cannot restart immediate damage.
                for (var g = groups.Count - 1; g >= 0; g--)
                    if (groups[g].Leases.Count == 0 && groups[g].Next <= clock + Epsilon)
                    {
                        var group = groups[g]; groups.RemoveAt(g); group.Clear(); pool.Push(group);
                    }
            }
            finally { advancing = false; }
        }
        private static void Pulse(Group group, double at, long lastLease)
        {
            group.Winners.Clear(); group.Indices.Clear();
            try
            {
                for (var i = 0; i < group.Leases.Count; i++)
                {
                    var lease = group.Leases[i];
                    if (!lease.Active || lease.Id > lastLease || lease.Start > at + Epsilon || lease.End + Epsilon < at) continue;
                    try
                    {
                        var count = lease.Area.SelectOverlapCandidates();
                        for (var c = 0; c < count; c++)
                        {
                            var candidate = lease.Area.GetOverlapCandidate(c);
                            if (!lease.Area.AllowsOverlapCandidate(candidate)) continue;
                            var winner = new Winner { Lease = lease, Candidate = candidate };
                            if (group.Indices.TryGetValue(candidate.Group, out var index))
                            {
                                var old = group.Winners[index].Lease;
                                if (lease.Priority > old.Priority || lease.Priority == old.Priority && lease.Id < old.Id)
                                    group.Winners[index] = winner;
                            }
                            else { group.Indices.Add(candidate.Group, group.Winners.Count); group.Winners.Add(winner); }
                        }
                    }
                    finally { if (lease.Area != null) lease.Area.ClearOverlapSelection(); }
                }
                for (var i = 0; i < group.Winners.Count; i++)
                {
                    var winner = group.Winners[i];
                    if (winner.Lease.Active) winner.Lease.Area.ApplyOverlapCandidate(winner.Candidate, winner.Lease.Id);
                }
            }
            finally { group.Winners.Clear(); group.Indices.Clear(); }
        }
        private void OnDestroy()
        {
            clearing = true;
            foreach (var group in groups)
                foreach (var lease in group.Leases) if (lease.Active) lease.Area.Stop();
            groups.Clear(); pool.Clear();
            if (instance == this) instance = null;
        }
    }
}
