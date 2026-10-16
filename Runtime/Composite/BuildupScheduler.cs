using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("")]
    public sealed class BuildupScheduler : MonoBehaviour
    {
        private static readonly List<CombatBuildup> active=new List<CombatBuildup>(64);
        private static BuildupScheduler instance;
        private static bool updating,quitting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        { foreach(var meter in active) if(meter!=null) meter.SchedulerIndex=-1; active.Clear(); instance=null; updating=quitting=false; }
        internal static void Register(CombatBuildup meter)
        {
            if(!Application.isPlaying || quitting || meter.SchedulerIndex>=0 || !meter.NeedsTick) return;
            if(instance==null) { instance=new GameObject("Combat Buildup Scheduler").AddComponent<BuildupScheduler>(); DontDestroyOnLoad(instance.gameObject); }
            meter.SchedulerIndex=active.Count; active.Add(meter);
        }
        internal static void Unregister(CombatBuildup meter) { if(!updating && meter.SchedulerIndex>=0) Remove(meter.SchedulerIndex); }
        private static void Remove(int index)
        { var old=active[index]; var last=active[active.Count-1]; active[index]=last; if(last!=null) last.SchedulerIndex=index; active.RemoveAt(active.Count-1); if(old!=null) old.SchedulerIndex=-1; }
        private void Update()
        {
            if(Time.deltaTime<=0) return; updating=true;
            try
            {
                for(var i=active.Count-1;i>=0;i--)
                { var meter=active[i]; if(meter!=null && meter.NeedsTick) meter.Advance(Time.deltaTime); if(meter==null || !meter.NeedsTick) Remove(i); }
            }
            finally { updating=false; }
        }
        private void OnApplicationQuit()=>quitting=true;
        private void OnDestroy() { if(instance!=this) return; foreach(var meter in active) if(meter!=null) meter.SchedulerIndex=-1; active.Clear(); instance=null; }
    }
}
