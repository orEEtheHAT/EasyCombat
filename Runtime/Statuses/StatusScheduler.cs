using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>One Update for the world; only actors with active statuses are visited.</summary>
    [AddComponentMenu("")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "GamesFactory.Combat", "GamesFactory.Combat")]
    public sealed class StatusScheduler : MonoBehaviour
    {
        private static StatusScheduler instance;
        private static readonly List<StatusController> active = new List<StatusController>(128);
        private static bool updating, quitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (var i = 0; i < active.Count; i++)
                if (active[i] != null) active[i].SchedulerIndex = -1;
            active.Clear();
            instance = null;
            updating = quitting = false;
        }
        internal static void Register(StatusController controller)
        {
            if (!Application.isPlaying || quitting || !controller.NeedsTick) return;
            if (instance == null)
            {
                var root = new GameObject("Combat Status Scheduler");
                instance = root.AddComponent<StatusScheduler>();
                DontDestroyOnLoad(root);
            }
            if (controller.SchedulerIndex >= 0) return;
            controller.SchedulerIndex = active.Count;
            active.Add(controller);
        }
        internal static void Unregister(StatusController controller)
        {
            if (controller.SchedulerIndex < 0 || updating) return;
            RemoveAt(controller.SchedulerIndex);
        }
        private static void RemoveAt(int index)
        {
            var removed = active[index];
            var last = active[active.Count - 1];
            active[index] = last;
            if (last != null) last.SchedulerIndex = index;
            active.RemoveAt(active.Count - 1);
            if (removed != null) removed.SchedulerIndex = -1;
        }
        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            updating = true;
            try
            {
                for (var i = active.Count - 1; i >= 0; i--)
                {
                    var controller = active[i];
                    if (controller == null || !controller.NeedsTick) { RemoveAt(i); continue; }
                    controller.Advance(Time.deltaTime);
                    if (controller == null || !controller.NeedsTick) RemoveAt(i);
                }
            }
            finally { updating = false; }
        }
        private void OnApplicationQuit() => quitting = true;
        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            for (var i = 0; i < active.Count; i++)
                if (active[i] != null) active[i].SchedulerIndex = -1;
            active.Clear();
        }
    }
}
