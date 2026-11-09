using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    // Contacts are spatial/transient. Never serialize them as persistent stat modifiers.
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Environment/Movement Influences")]
    public sealed class MovementInfluences : MonoBehaviour,ICombatTransient
    {
        [Tooltip("Optional timed Slow status key, multiplied once with the strongest spatial slow.")]
        public CombatStatDefinition MovementSpeedStat;
        private readonly Dictionary<Component,float> sources=new Dictionary<Component,float>(4);
        private StatusController statuses;
        private Health health;
        private long life;
        private float zone=1;
        public int SourceCount { get { CheckLife(); return sources.Count; } }
        public float Multiplier
        {
            get
            {
                if(!isActiveAndEnabled) return 1;
                CheckLife();
                return zone*(MovementSpeedStat && statuses && statuses.isActiveAndEnabled ? statuses.GetStatMultiplier(MovementSpeedStat) : 1);
            }
        }
        private void OnEnable() { statuses=GetComponent<StatusController>(); health=GetComponent<Health>(); CheckLife(); }
        private void CheckLife() { if(health && life!=health.Identity.Id) { life=health.Identity.Id; CancelForRestore(); } }
        public void Set(Component source,MovementInfluenceDefinition profile)
        {
            if(!source || !isActiveAndEnabled || !profile || !profile.IsValid) return;
            CheckLife(); float value=profile.SpeedMultiplier;
            if(sources.TryGetValue(source,out float previous) && previous==value) return;
            sources[source]=value; Recalculate();
        }
        public void Release(Component source) { if(!ReferenceEquals(source,null) && sources.Remove(source)) Recalculate(); }
        private void Recalculate() { zone=1; foreach(float value in sources.Values) zone=Mathf.Min(zone,value); }
        private void OnDisable()=>CancelForRestore();
        public void CancelForRestore() { sources.Clear(); zone=1; }
    }
}
