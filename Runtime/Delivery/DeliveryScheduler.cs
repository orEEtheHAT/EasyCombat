using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    internal interface ICombatDeliveryTick
    {
        bool WantsAutomaticTick { get; }
        int TickIndex { get; set; }
        void Advance(float deltaTime);
    }
    [AddComponentMenu("")]
    public sealed class DeliveryScheduler:MonoBehaviour
    {
        private static readonly List<MonoBehaviour> active=new List<MonoBehaviour>(64);
        private static DeliveryScheduler instance;
        private static bool updating,quitting;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        { foreach(var item in active) if(item!=null) ((ICombatDeliveryTick)item).TickIndex=-1; active.Clear(); instance=null; updating=quitting=false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var item in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)) if(item is ICombatDeliveryTick tick && tick.WantsAutomaticTick) Register(item); }
        internal static void Register(MonoBehaviour component)
        {
            var item=(ICombatDeliveryTick)component;
            if(!Application.isPlaying || quitting || item.TickIndex>=0 || !item.WantsAutomaticTick) return;
            if(instance==null) { instance=new GameObject("Combat Delivery Scheduler").AddComponent<DeliveryScheduler>(); DontDestroyOnLoad(instance.gameObject); }
            item.TickIndex=active.Count; active.Add(component);
        }
        internal static void Unregister(MonoBehaviour component) { var item=(ICombatDeliveryTick)component; if(!updating && item.TickIndex>=0) Remove(item.TickIndex); }
        private static void Remove(int index)
        { var old=active[index]; var last=active[active.Count-1]; active[index]=last; if(last!=null) ((ICombatDeliveryTick)last).TickIndex=index; active.RemoveAt(active.Count-1); if(old!=null) ((ICombatDeliveryTick)old).TickIndex=-1; }
        private void Update()
        {
            if(Time.deltaTime<=0) return; updating=true;
            try
            {
                for(var i=active.Count-1;i>=0;i--)
                { var component=active[i]; var item=component!=null ? (ICombatDeliveryTick)component : null; if(item!=null && item.WantsAutomaticTick) item.Advance(Time.deltaTime); if(component==null || !item.WantsAutomaticTick) Remove(i); }
            }
            finally { updating=false; }
        }
        private void OnApplicationQuit()=>quitting=true;
        private void OnDestroy() { if(instance!=this) return; foreach(var item in active) if(item!=null) ((ICombatDeliveryTick)item).TickIndex=-1; active.Clear(); instance=null; }
    }
}
