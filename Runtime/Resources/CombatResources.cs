using System;
using UnityEngine;

namespace EasyCombat
{
    internal struct ResourceSpendReceipt
    {
        internal CombatResources Owner;
        internal ResourceChange[] Changes;
        internal int Count;
        internal long Life;
        internal void NotifyAndRelease()
        {
            var changes=Changes; if(changes==null) return;
            var owner=Owner; var count=Count; var life=Life; Changes=null; Owner=null; Count=0; Life=0;
            try { if(owner!=null) owner.NotifyDeferred(changes,count,life); }
            finally { System.Buffers.ArrayPool<ResourceChange>.Shared.Return(changes,true); }
        }
    }
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Combat Resources")]
    public sealed class CombatResources : MonoBehaviour
    {
        public const int Capacity = 32;
        [SerializeField] private ResourceProfile profile;
        [SerializeField] private bool automaticAdvance = true;
        private struct State
        {
            public ResourceDefinition Key;
            public CombatStatDefinition MaximumStat;
            public float Current, Maximum, InitialFraction, Regen, SpendDelay, Delay;
            public ResourceView View => new ResourceView(Key,Current,Maximum,Delay);
        }
        private State[] states = Array.Empty<State>();
        private readonly CombatSignal<ResourceChange> changed = new CombatSignal<ResourceChange>();
        private CombatStats stats;
        private bool mutating;
        public int Count => states.Length;
        public bool AutomaticAdvance { get => automaticAdvance; set => automaticAdvance = value; }
        public event Action<ResourceChange> Changed { add => changed.Add(value); remove => changed.Remove(value); }
        private void Awake() { if (profile != null) Configure(profile); }
        private void Update() { if (automaticAdvance) Advance(Time.deltaTime); }
        public bool Configure(ResourceProfile definition, CombatStats attributes = null)
        {
            if (mutating || definition == null || !definition.IsValid) return false;
            var source = attributes != null ? attributes : GetComponent<CombatStats>();
            var next = new State[definition.Resources.Length];
            for (var i = 0; i < next.Length; i++)
            {
                var key = definition.Resources[i]; var max = key.Maximum;
                if (key.MaximumStat != null && (source == null || !source.TryGetValue(key.MaximumStat,out max) || !CombatMath.Positive(max))) return false;
                next[i] = new State { Key = key, MaximumStat = key.MaximumStat, Maximum = max, Current = max * key.InitialFraction,
                    InitialFraction = key.InitialFraction, Regen = key.RegenerationPerSecond, SpendDelay = key.DelayAfterSpend };
            }
            states = next; stats = source; profile = definition; return true;
        }
        private int Find(ResourceDefinition key)
        { if (key != null) for (var i = 0; i < states.Length; i++) if (states[i].Key == key) return i; return -1; }
        public ResourceView GetResource(int index) => states[index].View;
        public bool TryGet(ResourceDefinition key, out ResourceView value)
        { var index = Find(key); value = index >= 0 ? states[index].View : default; return index >= 0; }
        public bool CanSpend(ReadOnlySpan<ResourceCost> costs)
        { Span<float> totals = stackalloc float[Capacity]; return !mutating && isActiveAndEnabled && PrepareCosts(costs,totals); }
        private bool PrepareCosts(ReadOnlySpan<ResourceCost> costs, Span<float> totals)
        {
            totals.Clear();
            for (var i = 0; i < costs.Length; i++)
            {
                var cost = costs[i]; if (!cost.IsValid) return false;
                var index = Find(cost.Resource); if (index < 0) return false;
                totals[index] += cost.Amount;
                if (!CombatMath.Finite(totals[index]) || totals[index] > states[index].Current) return false;
            }
            return true;
        }
        /// <summary>Duplicate entries are summed. Every resource is committed before any observer is called.</summary>
        public bool TrySpend(ReadOnlySpan<ResourceCost> costs)
        {
            if (mutating || !isActiveAndEnabled) return false;
            Span<float> totals = stackalloc float[Capacity];
            if (!PrepareCosts(costs,totals)) return false;
            mutating = true;
            try
            {
                Span<float> previous = stackalloc float[Capacity];
                for (var i = 0; i < states.Length; i++)
                {
                    previous[i] = states[i].Current;
                    states[i].Current -= totals[i];
                    if (totals[i] > 0) states[i].Delay = states[i].SpendDelay;
                }
                for (var i = 0; i < states.Length; i++) if (totals[i] > 0) Publish(i,previous[i],ResourceChangeReason.Spend);
                return true;
            }
            finally { mutating = false; }
        }
        // Used by defenses only after every reduction has validated. No callbacks run until the hit has committed.
        internal bool TrySpendSilently(ReadOnlySpan<ResourceCost> costs,out ResourceSpendReceipt receipt)
        {
            receipt=default;
            if(mutating || !isActiveAndEnabled) return false;
            Span<float> totals=stackalloc float[Capacity]; if(!PrepareCosts(costs,totals)) return false;
            var changes=System.Buffers.ArrayPool<ResourceChange>.Shared.Rent(Mathf.Max(1,states.Length)); var count=0;
            mutating=true;
            try
            {
                for(var i=0;i<states.Length;i++)
                {
                    if(totals[i]<=0) continue;
                    var before=states[i].Current; states[i].Current-=totals[i]; states[i].Delay=states[i].SpendDelay;
                    changes[count++]=new ResourceChange(states[i].View,before,ResourceChangeReason.Spend);
                }
                var health=GetComponent<Health>();
                receipt=new ResourceSpendReceipt { Owner=this,Changes=changes,Count=count,Life=health!=null ? health.Identity.Id : 0 };
                return true;
            }
            finally { mutating=false; }
        }
        internal void NotifyDeferred(ResourceChange[] changes,int count,long life)
        {
            var health=GetComponent<Health>();
            if(life!=0 && (health==null || health.Identity.Id!=life)) return;
            mutating=true;
            try { for(var i=0;i<count;i++) changed.Publish(changes[i]); }
            finally { mutating=false; }
        }
        public float Restore(ResourceDefinition key, float amount)
        {
            if (mutating || !isActiveAndEnabled || !CombatMath.Positive(amount)) return 0;
            var i = Find(key); if (i < 0) return 0;
            var before = states[i].Current;
            var restored = Mathf.Min(amount,states[i].Maximum-before); if (restored <= 0) return 0;
            mutating = true;
            try { states[i].Current += restored; Publish(i,before,ResourceChangeReason.Restore); return restored; }
            finally { mutating = false; }
        }
        /// <summary>Explicitly refresh stat-driven capacities. An invalid stat rejects the entire refresh.</summary>
        public bool RefreshMaximums(bool preserveRatio = false)
        {
            if (mutating) return false;
            Span<float> maxima = stackalloc float[Capacity];
            Span<float> previous = stackalloc float[Capacity];
            Span<bool> modified = stackalloc bool[Capacity]; modified.Clear();
            for (var i = 0; i < states.Length; i++)
            {
                maxima[i] = states[i].Maximum;
                if (states[i].MaximumStat != null && (stats == null || !stats.TryGetValue(states[i].MaximumStat,out maxima[i]) || !CombatMath.Positive(maxima[i]))) return false;
            }
            mutating = true;
            try
            {
                for (var i = 0; i < states.Length; i++)
                {
                    previous[i] = states[i].Current; modified[i] = maxima[i] != states[i].Maximum;
                    states[i].Current = preserveRatio ? states[i].Current/states[i].Maximum*maxima[i] : Mathf.Min(states[i].Current,maxima[i]);
                    states[i].Maximum = maxima[i];
                }
                for (var i = 0; i < states.Length; i++) if (modified[i]) Publish(i,previous[i],ResourceChangeReason.MaximumChanged);
                return true;
            }
            finally { mutating = false; }
        }
        public void Advance(float deltaTime)
        {
            if (mutating || !isActiveAndEnabled || !CombatMath.Positive(deltaTime)) return;
            mutating = true;
            try
            {
                for (var i = 0; i < states.Length; i++)
                {
                    var before = states[i].Current;
                    var regenTime = Mathf.Max(0,deltaTime-states[i].Delay);
                    states[i].Delay = Mathf.Max(0,states[i].Delay-deltaTime);
                    states[i].Current = (float)Math.Min(states[i].Maximum, before + (double)states[i].Regen*regenTime);
                    if (states[i].Current != before) Publish(i,before,ResourceChangeReason.Regeneration);
                }
            }
            finally { mutating = false; }
        }
        public void ResetResources()
        {
            if (mutating) return;
            mutating = true;
            try
            {
                Span<float> previous = stackalloc float[Capacity];
                for (var i = 0; i < states.Length; i++)
                { previous[i] = states[i].Current; states[i].Current = states[i].Maximum*states[i].InitialFraction; states[i].Delay = 0; }
                for (var i = 0; i < states.Length; i++) Publish(i,previous[i],ResourceChangeReason.Reset);
            }
            finally { mutating = false; }
        }
        private void Publish(int index, float previous, ResourceChangeReason reason)
            => changed.Publish(new ResourceChange(states[index].View,previous,reason));
    }
}
