using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/Presentation/Presentation Hub")]
    public sealed class CombatPresentationHub:MonoBehaviour
    {
        [SerializeField] private CombatPresentationDefinition definition;
        [SerializeField] private bool unscaledTime=true;
        [SerializeField] private bool automaticFlush=true;
        private CombatPresentationBuffer buffer;
        private readonly CombatSignal<CombatPresentationEvent> emitted=new CombatSignal<CombatPresentationEvent>();
        public CombatPresentationBuffer Buffer=>buffer;
        public bool AutomaticFlush { get=>automaticFlush; set=>automaticFlush=value; }
        public double Now=>unscaledTime ? Time.unscaledTimeAsDouble : CombatClock.Now;
        public event Action<CombatPresentationEvent> Emitted { add=>emitted.Add(value); remove=>emitted.Remove(value); }
        public bool Configure(CombatPresentationDefinition profile)
        {
            if(profile==null || !profile.IsValid) return false;
            if(buffer!=null) buffer.Emitted-=Publish; definition=profile; buffer=new CombatPresentationBuffer(profile); buffer.Emitted+=Publish; return true;
        }
        private void Publish(CombatPresentationEvent value)=>emitted.Publish(value);
        private void OnEnable()
        { if(buffer==null && definition!=null) Configure(definition); CombatSaveSystem.Restored-=Restored; CombatSaveSystem.Restored+=Restored; }
        private void OnDisable() { CombatSaveSystem.Restored-=Restored; buffer?.Clear(); }
        private void OnDestroy()=>OnDisable();
        private void Restored(CombatRestoreResult _)=>buffer?.Clear();
        private void LateUpdate() { if(automaticFlush) Flush(); }
        public bool Submit(in CombatPresentationEvent value)=>isActiveAndEnabled && buffer!=null && buffer.Submit(in value,Now);
        public int Flush()=>isActiveAndEnabled && buffer!=null ? buffer.Flush(Now) : 0;
        public void Clear()=>buffer?.Clear();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var hub in FindObjectsByType<CombatPresentationHub>(FindObjectsSortMode.None)) if(hub.isActiveAndEnabled) { if(hub.definition!=null) hub.Configure(hub.definition); hub.OnEnable(); } }
    }
}
