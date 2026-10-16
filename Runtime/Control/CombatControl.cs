using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [Flags]
    public enum CombatRestriction { None=0, Move=1, Turn=2, Attack=4, Cast=8, Jump=16, Interact=32, All=63 }
    public readonly struct ControlHandle
    {
        internal readonly CombatControl Owner;
        internal readonly long Id;
        internal ControlHandle(CombatControl owner,long id) { Owner=owner; Id=id; }
        public bool Remove() => Owner != null && Owner.Remove(this);
    }
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Control/Combat Control")]
    public sealed class CombatControl : MonoBehaviour
    {
        private struct Lease { public long Id; public CombatRestriction Restriction; }
        private readonly List<Lease> leases=new List<Lease>(8);
        private readonly CombatSignal<CombatRestriction> changed=new CombatSignal<CombatRestriction>();
        private CombatRestriction manual;
        public event Action<CombatRestriction> Changed { add=>changed.Add(value); remove=>changed.Remove(value); }
        public CombatRestriction Restrictions
        {
            get
            {
                if (!isActiveAndEnabled) return CombatRestriction.None;
                var statuses=GetComponent<StatusController>();
                return manual | LifeRestrictions(gameObject) | (statuses != null && statuses.isActiveAndEnabled ? statuses.Restrictions : CombatRestriction.None);
            }
        }
        public bool Allows(CombatRestriction action) => (Restrictions & action)==0;
        public bool TryRestrict(CombatRestriction restrictions,out ControlHandle handle)
        {
            handle=default;
            if (restrictions==CombatRestriction.None || (restrictions & ~CombatRestriction.All)!=0 || leases.Count>=64) return false;
            var id=CombatEvents.NextId(); leases.Add(new Lease { Id=id, Restriction=restrictions }); manual |= restrictions;
            handle=new ControlHandle(this,id); changed.Publish(Restrictions); return true;
        }
        public bool Remove(ControlHandle handle)
        {
            if (!ReferenceEquals(handle.Owner,this) || handle.Id==0) return false;
            for (var i=0;i<leases.Count;i++)
                if (leases[i].Id==handle.Id) { leases.RemoveAt(i); Rebuild(); return true; }
            return false;
        }
        public void ResetRestrictions() { leases.Clear(); Rebuild(); }
        private void Rebuild()
        { manual=CombatRestriction.None; foreach (var lease in leases) manual |= lease.Restriction; changed.Publish(Restrictions); }
        public static bool IsAllowed(GameObject actor,CombatRestriction action)
        {
            if (actor==null) return false;
            var control=actor.GetComponent<CombatControl>();
            if (control!=null && control.isActiveAndEnabled) return control.Allows(action);
            var statuses=actor.GetComponent<StatusController>();
            var restrictions=LifeRestrictions(actor) | (statuses!=null && statuses.isActiveAndEnabled ? statuses.Restrictions : CombatRestriction.None);
            return (restrictions & action)==0;
        }
        private static CombatRestriction LifeRestrictions(GameObject actor)
        {
            var health=actor.GetComponent<Health>();
            if(health==null) return CombatRestriction.None;
            if(health.LifeState==CombatLifeState.Dead) return CombatRestriction.All;
            return health.LifeState==CombatLifeState.Downed && health.LifeRules!=null ? health.LifeRules.DownedRestrictions : CombatRestriction.None;
        }
    }
}
