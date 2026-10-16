using System;
using System.Collections.Generic;

namespace EasyCombat
{
    /// <summary>Bounded presentation-only queue. No gameplay or attribution depends on delivery/coalescing/drop policy.</summary>
    public sealed class CombatPresentationBuffer
    {
        private readonly struct Key:IEquatable<Key>
        {
            private readonly CombatPresentationKind kind;
            private readonly long subject,source;
            private readonly UnityEngine.ScriptableObject definition;
            private readonly bool critical;
            internal Key(in CombatPresentationEvent value,bool separate)
            { kind=value.Kind; subject=value.Subject.Id; source=separate ? value.Source.Id : 0; definition=value.Definition; critical=value.Critical; }
            public bool Equals(Key other)=>kind==other.kind && subject==other.subject && source==other.source && ReferenceEquals(definition,other.definition) && critical==other.critical;
            public override bool Equals(object value)=>value is Key other && Equals(other);
            public override int GetHashCode()=>(((int)kind*397 ^ subject.GetHashCode())*397 ^ source.GetHashCode())*397
                ^ (ReferenceEquals(definition,null) ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(definition)) ^ (critical ? 1 : 0);
        }
        private struct Pending { public Key Key; public CombatPresentationEvent Event; public double Due; public long Sequence; public bool Mergeable; }
        private struct Rate { public Key Key; public double Until; public int Count; }
        private readonly Pending[] pending;
        private readonly Rate[] rates;
        private readonly Dictionary<Key,int> mergeIndices,rateIndices;
        private readonly CombatPresentationRule[] rules=new CombatPresentationRule[CombatPresentationDefinition.KindCount];
        private readonly int defaultBudget,globalMaximum;
        private readonly CombatSignal<CombatPresentationEvent> emitted=new CombatSignal<CombatPresentationEvent>();
        private int count,rateCount,globalCount;
        private long sequence;
        private double lastTime=double.NegativeInfinity,globalUntil;
        private bool flushing,clearRequested;
        public int Count=>count;
        public long Merged { get; private set; }
        public long DroppedCapacity { get; private set; }
        public long DroppedRate { get; private set; }
        public long DroppedStale { get; private set; }
        public long Suppressed { get; private set; }
        public event Action<CombatPresentationEvent> Emitted { add=>emitted.Add(value); remove=>emitted.Remove(value); }
        public CombatPresentationBuffer(CombatPresentationDefinition definition)
        {
            if(definition==null || !definition.IsValid) throw new ArgumentException("A valid presentation definition is required.");
            pending=new Pending[definition.Capacity]; rates=new Rate[definition.RateKeyCapacity];
            mergeIndices=new Dictionary<Key,int>(pending.Length); rateIndices=new Dictionary<Key,int>(rates.Length);
            for(var i=0;i<rules.Length;i++) rules[i]=new CombatPresentationRule((CombatPresentationKind)i);
            foreach(var rule in definition.Rules) rules[(int)rule.Kind]=rule;
            defaultBudget=definition.MaximumPerFrame; globalMaximum=definition.GlobalMaximumPerSecond;
        }
        private bool Time(double now)
        {
            if(double.IsNaN(now) || double.IsInfinity(now) || now<0) return false;
            if(now<lastTime) { if(flushing) { clearRequested=true; return false; } ClearNow(); }
            lastTime=now; return true;
        }
        public bool Submit(in CombatPresentationEvent value,double now)
        {
            if(CombatSaveSystem.IsRestoring || !value.IsValid || clearRequested || !Time(now)) return false;
            var rule=rules[(int)value.Kind]; if(!rule.Enabled) { Suppressed++; return false; }
            var key=new Key(in value,rule.SeparateSources);
            if(rule.MergeWindow>0 && mergeIndices.TryGetValue(key,out var index))
            {
                if(pending[index].Due>now)
                { pending[index].Event=pending[index].Event.Merge(in value); Merged++; return true; }
                mergeIndices.Remove(key);
            }
            if(count>=pending.Length) { DroppedCapacity++; return false; }
            pending[count]=new Pending { Key=key,Event=value,Due=now+rule.MergeWindow,Sequence=++sequence,Mergeable=rule.MergeWindow>0 };
            if(rule.MergeWindow>0) mergeIndices[key]=count;
            count++; return true;
        }
        public int Flush(double now,int maximum=0)
        {
            if(flushing || CombatSaveSystem.IsRestoring || !Time(now)) return 0;
            var budget=maximum>0 ? Math.Min(maximum,defaultBudget) : defaultBudget; var initial=count; var lastSequence=sequence; var sent=0; var visited=0; var index=0;
            if(count==0) return 0;
            ExpireRates(now); flushing=true;
            try
            {
                while(index<count && visited++<initial && sent<budget && !clearRequested)
                {
                    var item=pending[index]; if(item.Sequence>lastSequence || item.Due>now) { index++; continue; }
                    RemovePending(index); var rule=rules[(int)item.Event.Kind];
                    if(rule.DiscardAfterRespawn && item.Event.SubjectHealth!=null && item.Event.SubjectHealth.Identity.Id!=item.Event.Subject.Id) { DroppedStale++; continue; }
                    if(!Allow(in item.Key,rule.MaximumPerSecond,now)) { DroppedRate++; continue; }
                    sent++; emitted.Publish(item.Event);
                }
            }
            finally { flushing=false; if(clearRequested) ClearNow(); }
            return sent;
        }
        private bool Allow(in Key key,int maximum,double now)
        {
            if(now>=globalUntil) { globalUntil=now+1; globalCount=0; }
            if(globalMaximum>0 && globalCount>=globalMaximum) return false;
            if(maximum>0)
            {
                if(!rateIndices.TryGetValue(key,out var index))
                {
                    if(rateCount>=rates.Length) return false;
                    index=rateCount++; rateIndices.Add(key,index); rates[index]=new Rate { Key=key,Until=now+1 };
                }
                if(rates[index].Count>=maximum) return false;
                rates[index].Count++;
            }
            globalCount++; return true;
        }
        private void ExpireRates(double now)
        {
            for(var i=rateCount-1;i>=0;i--) if(rates[i].Until<=now)
            {
                rateIndices.Remove(rates[i].Key); var last=--rateCount; if(i<last) { rates[i]=rates[last]; rateIndices[rates[i].Key]=i; } rates[last]=default;
            }
        }
        private void RemovePending(int index)
        {
            var item=pending[index]; if(item.Mergeable && mergeIndices.TryGetValue(item.Key,out var mapped) && mapped==index) mergeIndices.Remove(item.Key);
            var last=--count;
            if(index<last)
            {
                pending[index]=pending[last]; var moved=pending[index];
                if(moved.Mergeable && mergeIndices.TryGetValue(moved.Key,out mapped) && mapped==last) mergeIndices[moved.Key]=index;
            }
            pending[last]=default;
        }
        public void Clear() { if(flushing) clearRequested=true; else ClearNow(); }
        private void ClearNow()
        {
            Array.Clear(pending,0,count); Array.Clear(rates,0,rateCount); count=rateCount=globalCount=0; globalUntil=0;
            mergeIndices.Clear(); rateIndices.Clear(); clearRequested=false; lastTime=double.NegativeInfinity;
        }
    }
}
