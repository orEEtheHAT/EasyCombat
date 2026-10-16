using System;
using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,RequireComponent(typeof(CombatBodyPart)),AddComponentMenu("EasyCombat/2D/Body Part Colliders")]
    public sealed class BodyPartColliders2D:MonoBehaviour
    {
        private CombatBodyPart part;
        private Health core,local;
        private Collider2D[] colliders=Array.Empty<Collider2D>();
        private bool[] original=Array.Empty<bool>();
        private bool captured;
        private void Awake()=>Initialize();
        private void Start()=>Initialize();
        private void OnEnable() { if(captured) Initialize(); }
        /// <summary>Call after configuring a body part at runtime; serialized prefabs initialize automatically.</summary>
        public void Initialize()
        {
            Unbind(); part=GetComponent<CombatBodyPart>(); if(part==null) return;
            if(!captured)
            {
                colliders=GetComponentsInChildren<Collider2D>(true); original=new bool[colliders.Length];
                for(var i=0;i<colliders.Length;i++) original[i]=colliders[i].enabled; captured=true;
            }
            core=part.Core; local=part.LocalHealth; part.Broken+=Broken; local.Changed+=Changed; local.StateRestored+=Restored; if(core!=null) core.Died+=CoreDied;
        }
        private void Broken(CombatBodyPart body)
        { if(this!=null && enabled && body.Definition!=null && body.Definition.OnBreak==BodyPartBreakBehavior.DisableColliders) Set(false); }
        private void CoreDied(Health _)
        { if(this!=null && enabled && part!=null && part.Definition!=null && part.Definition.DisableWithCoreDeath) Set(false); }
        private void Changed(HealthChange change)
        { if(this!=null && enabled && change.Reason==HealthChangeReason.Reset) Set(true); }
        private void Restored(Health _)
        {
            if(this==null || part==null || part.Definition==null) return; Initialize();
            var disabled=part.IsBroken && part.Definition.OnBreak==BodyPartBreakBehavior.DisableColliders || core!=null && !core.IsAlive && part.Definition.DisableWithCoreDeath;
            Set(!disabled);
        }
        private void Set(bool restore) { for(var i=0;i<colliders.Length;i++) if(colliders[i]!=null) colliders[i].enabled=restore && original[i]; }
        private void Unbind()
        { if(part!=null) part.Broken-=Broken; if(local!=null) { local.Changed-=Changed; local.StateRestored-=Restored; } if(core!=null) core.Died-=CoreDied; }
        private void OnDestroy()=>Unbind();
    }
}
