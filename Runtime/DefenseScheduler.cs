using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class DefenseScheduler : MonoBehaviour
    {
        private static DefenseScheduler instance;
        private static readonly List<Health> active = new List<Health>(64);
        private static bool updating, quitting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        { foreach (var health in active) if (health != null) health.DefenseSchedulerIndex = -1; active.Clear(); instance = null; updating = quitting = false; }
        internal static void Register(Health health)
        {
            if (!Application.isPlaying || quitting || !health.NeedsDefenseTick || health.DefenseSchedulerIndex >= 0) return;
            if (instance == null) { instance = new GameObject("Combat Defense Scheduler").AddComponent<DefenseScheduler>(); DontDestroyOnLoad(instance.gameObject); }
            health.DefenseSchedulerIndex = active.Count; active.Add(health);
        }
        internal static void Unregister(Health health) { if (!updating && health.DefenseSchedulerIndex >= 0) Remove(health.DefenseSchedulerIndex); }
        private static void Remove(int i)
        {
            var old = active[i]; var last = active[active.Count - 1]; active[i] = last;
            if (last != null) last.DefenseSchedulerIndex = i;
            active.RemoveAt(active.Count - 1); if (old != null) old.DefenseSchedulerIndex = -1;
        }
        private void Update()
        {
            if (Time.deltaTime <= 0) return;
            updating = true;
            try { for (var i = active.Count - 1; i >= 0; i--)
                { var health = active[i]; if (health != null && health.NeedsDefenseTick) health.AdvanceDefense(Time.deltaTime);
                  if (health == null || !health.NeedsDefenseTick) Remove(i); } }
            finally { updating = false; }
        }
        private void OnApplicationQuit() => quitting = true;
        private void OnDestroy() { if (instance != this) return; foreach (var health in active) if (health != null) health.DefenseSchedulerIndex = -1; active.Clear(); instance = null; }
    }
}
