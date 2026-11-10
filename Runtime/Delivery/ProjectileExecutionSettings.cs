using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace EasyCombat
{
    public enum ProjectileExecutionMode { Parallel = 0, SingleThread = 1 }

    public sealed class ProjectileExecutionSettings : ScriptableObject
    {
        internal const string ResourceName = "EasyCombatProjectileExecution";
        [SerializeField] internal ProjectileExecutionMode mode = ProjectileExecutionMode.Parallel;
        private static ProjectileExecutionSettings settings;
        private static bool loaded;

        public static ProjectileExecutionMode RequestedMode
        {
            get
            {
                if (!loaded)
                {
                    settings = Resources.Load<ProjectileExecutionSettings>(ResourceName);
                    loaded = true;
                }
                return settings != null ? settings.mode : ProjectileExecutionMode.Parallel;
            }
        }

        public static ProjectileExecutionMode EffectiveMode => Resolve(RequestedMode);

        internal static ProjectileExecutionMode Resolve(ProjectileExecutionMode requested)
        {
#if UNITY_WEBGL && !UNITY_EDITOR && !UNITY_6000_4_OR_NEWER
            return ProjectileExecutionMode.SingleThread;
#else
            return requested == ProjectileExecutionMode.Parallel && JobsUtility.JobWorkerCount > 0
                ? ProjectileExecutionMode.Parallel : ProjectileExecutionMode.SingleThread;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reload()
        {
            loaded = false;
            settings = null;
        }
    }
}
