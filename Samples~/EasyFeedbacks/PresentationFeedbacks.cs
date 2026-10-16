using System;
using EasyFeedbacks;
using UnityEngine;

namespace EasyCombat.Integrations
{
    /// <summary>Optional output adapter. Import this sample only with EasyFeedbacks installed.</summary>
    [AddComponentMenu("EasyCombat/Integrations/Presentation Feedbacks")]
    public sealed class PresentationFeedbacks:MonoBehaviour
    {
        [Serializable] public struct Binding
        {
            public CombatPresentationKind Kind;
            public FeedbackPreset Preset;
            [Tooltip("Optional attack/status/ability filter.")] public ScriptableObject Definition;
            public bool CriticalOnly;
            [Min(0),Tooltip("Per binding, across all actors. Independent of number coalescing.")] public float MinimumInterval;
            [Min(0),Tooltip("Zero uses intensity 1; otherwise amount / reference, clamped to 0.1–3.")] public float ReferenceAmount;
        }
        public CombatPresentationHub Hub;
        public Binding[] Bindings=Array.Empty<Binding>();
        private Binding[] captured=Array.Empty<Binding>();
        private double[] next=Array.Empty<double>();
        private CombatPresentationHub bound;
        private double lastTime;
        private void OnEnable()=>Rebind();
        /// <summary>Call after assigning/changing Hub or Bindings at runtime.</summary>
        public void Rebind()
        {
            OnDisable();
            if(!isActiveAndEnabled) return;
            if(Hub==null) Hub=GetComponent<CombatPresentationHub>();
            captured=Bindings!=null ? (Binding[])Bindings.Clone() : Array.Empty<Binding>(); next=new double[captured.Length]; lastTime=0;
            foreach(var entry in captured) if(entry.Preset!=null) FeedbackService.Prewarm(entry.Preset);
            bound=Hub; if(bound!=null) bound.Emitted+=Play;
        }
        private void OnDisable() { if(bound!=null) bound.Emitted-=Play; bound=null; }
        private void Play(CombatPresentationEvent value)
        {
            var now=Time.unscaledTimeAsDouble;
            if(now<lastTime) Array.Clear(next,0,next.Length); lastTime=now;
            for(var i=0;i<captured.Length;i++)
            {
                var entry=captured[i];
                if(entry.Preset==null || entry.Kind!=value.Kind || entry.Definition!=null && entry.Definition!=value.Definition
                    || entry.CriticalOnly && !value.Critical || now<next[i]) continue;
                var interval=float.IsNaN(entry.MinimumInterval) || float.IsInfinity(entry.MinimumInterval) ? 0 : Mathf.Max(0,entry.MinimumInterval);
                next[i]=now+interval;
                var intensity=entry.ReferenceAmount>0 && value.Amount>0 ? Mathf.Clamp(value.Amount/entry.ReferenceAmount,.1f,3f) : 1;
                if(float.IsNaN(intensity)) intensity=1;
                var rotation=value.Direction.sqrMagnitude>.000001f ? Quaternion.LookRotation(value.Direction) : Quaternion.identity;
                // Cached world point; never follow a pooled actor after it has become another life.
                FeedbackService.Play(entry.Preset,new FeedbackContext(value.Position,rotation,null,null,intensity));
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var bridge in FindObjectsByType<PresentationFeedbacks>(FindObjectsSortMode.None)) if(bridge.isActiveAndEnabled) bridge.Rebind(); }
    }
}
