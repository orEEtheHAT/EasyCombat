using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("")]
    public sealed class AbilityScheduler:MonoBehaviour
    {
        private static readonly List<CombatAbilities> active=new List<CombatAbilities>(64);
        private static AbilityScheduler instance;
        private static bool updating,quitting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        { foreach(var item in active) if(item!=null) item.SchedulerIndex=-1; active.Clear(); instance=null; updating=quitting=false; }
        internal static void Register(CombatAbilities item)
        {
            if(!Application.isPlaying || quitting || item.SchedulerIndex>=0 || !item.NeedsTick) return;
            if(instance==null) { instance=new GameObject("Combat Ability Scheduler").AddComponent<AbilityScheduler>(); DontDestroyOnLoad(instance.gameObject); }
            item.SchedulerIndex=active.Count; active.Add(item);
        }
        internal static void Unregister(CombatAbilities item) { if(!updating && item.SchedulerIndex>=0) Remove(item.SchedulerIndex); }
        private static void Remove(int index)
        { var old=active[index]; var last=active[active.Count-1]; active[index]=last; if(last!=null) last.SchedulerIndex=index; active.RemoveAt(active.Count-1); if(old!=null) old.SchedulerIndex=-1; }
        private void Update()
        {
            if(Time.deltaTime<=0) return; updating=true;
            try
            { for(var i=active.Count-1;i>=0;i--) { var item=active[i]; if(item!=null && item.NeedsTick) item.Advance(Time.deltaTime); if(item==null || !item.NeedsTick) Remove(i); } }
            finally { updating=false; }
        }
        private void OnApplicationQuit()=>quitting=true;
        private void OnDestroy() { if(instance!=this) return; foreach(var item in active) if(item!=null) item.SchedulerIndex=-1; active.Clear(); instance=null; }
    }
}
