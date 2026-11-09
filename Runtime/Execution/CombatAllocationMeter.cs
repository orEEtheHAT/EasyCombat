using System;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
using UnityEngine.Profiling;
#endif

namespace EasyCombat
{
    // Internal diagnostic used only by tests and the Studio benchmark. Counts allocation events, not bytes.
    internal static class CombatAllocationMeter
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly object[] keep=new object[4];
        private static readonly Action empty=()=>{};
        private static readonly Action positive=()=>{ for(int i=0;i<keep.Length;i++) keep[i]=new byte[256+i]; };
        private static long Sample(ProfilerRecorder recorder,Action action)
        {
            recorder.Reset(); recorder.Start();
            try { action(); } finally { recorder.Stop(); }
            return recorder.Count>0 ? recorder.GetSample(0).Count : 0;
        }
#endif
        internal static bool TryMeasure(Action work,out long events)
        {
            if(work==null) throw new ArgumentNullException(nameof(work));
            events=-1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var old=Profiler.enabled;
            try
            {
                Profiler.enabled=true;
                var opts=ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame;
#if !UNITY_WEBGL || UNITY_EDITOR
                opts|=ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
#endif
                using(var r=new ProfilerRecorder(ProfilerCategory.Memory,"GC.Alloc",1,opts))
                {
                    if(!r.Valid) return false;
                    Sample(r,positive); // warm the diagnostic itself
                    if(Sample(r,empty)!=0 || Sample(r,positive)!=keep.Length) return false;
                    events=Sample(r,work); return true;
                }
            }
            finally { Array.Clear(keep,0,keep.Length); Profiler.enabled=old; }
#else
            return false;
#endif
        }
    }
}
