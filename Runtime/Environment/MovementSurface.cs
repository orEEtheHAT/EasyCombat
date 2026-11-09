using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    // Use one primitive trigger per volume. Wheel sampling uses the actual ground contact, not chassis overlap.
    [DisallowMultipleComponent,AddComponentMenu("EasyCombat/Environment/Mud or Slow Volume")]
    public sealed class MovementSurface : MonoBehaviour,ICombatTransient
    {
        public MovementInfluenceDefinition Effect;
        private struct Occupant { public Collider Collider; public MovementInfluences Receiver; }
        private readonly List<Occupant> occupants=new List<Occupant>(8);
        private void OnTriggerEnter(Collider other)=>Touch(other);
        private void OnTriggerStay(Collider other)=>Touch(other);
        private void Touch(Collider other)
        {
            if(!isActiveAndEnabled || !other || !Effect || !Effect.IsValid) return;
            for(int i=0;i<occupants.Count;i++) if(occupants[i].Collider==other)
            { if(occupants[i].Receiver) occupants[i].Receiver.Set(this,Effect); return; }
            var receiver=other.GetComponentInParent<MovementInfluences>();
            if(receiver && receiver.isActiveAndEnabled) { occupants.Add(new Occupant {Collider=other,Receiver=receiver}); receiver.Set(this,Effect); }
        }
        private void OnTriggerExit(Collider other)
        { for(int i=occupants.Count-1;i>=0;i--) if(occupants[i].Collider==other) RemoveAt(i); }
        private void RemoveAt(int i)
        {
            var receiver=occupants[i].Receiver; occupants.RemoveAt(i);
            if(!receiver) return; foreach(var occupant in occupants) if(occupant.Receiver==receiver) return;
            receiver.Release(this);
        }
        private Collider volume;
        private void Awake() { volume=GetComponent<Collider>(); }
        private void FixedUpdate()
        {
            if(!volume || !volume.enabled || !volume.isTrigger || !Effect || !Effect.IsValid) { CancelForRestore(); return; }
            for(int i=occupants.Count-1;i>=0;i--)
            {
                var entry=occupants[i];
                if(!entry.Collider || !entry.Collider.enabled || !entry.Collider.gameObject.activeInHierarchy || !entry.Receiver || !entry.Receiver.isActiveAndEnabled
                    || !volume.bounds.Intersects(entry.Collider.bounds)) RemoveAt(i);
            }
        }
        public void CancelForRestore() { foreach(var entry in occupants) if(entry.Receiver) entry.Receiver.Release(this); occupants.Clear(); }
        private void OnDisable()=>CancelForRestore();
    }
}
