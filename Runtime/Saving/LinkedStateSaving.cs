using System;
using UnityEngine;

namespace EasyCombat
{
    public sealed partial class CombatBodyPart
    {
        internal SavedBodyPart SaveCombatState(SaveCodec codec)
        {
            SaveCodec.Need(!applying && definition!=null && definition.IsValid,"Body part must be configured and idle to save.");
            return new SavedBodyPart { Definition=codec.Id(definition),Core=codec.Reference(core!=null ? core.gameObject : null) };
        }
        internal Action PrepareCombatRestore(SavedBodyPart saved,SaveCodec codec)
        {
            SaveCodec.Need(saved!=null && !applying,"Invalid/busy body part restore."); var profile=codec.Get<BodyPartDefinition>(saved.Definition,true);
            SaveCodec.Need(profile.IsValid,"Invalid body part definition."); var source=codec.Source(saved.Core); var owner=source!=null ? source.GetComponent<Health>() : null;
            SaveCodec.Need(owner!=LocalHealth && (string.IsNullOrEmpty(saved.Core) || owner!=null),"Missing or circular body core reference.");
            var nextColliders=initialized ? colliders : GetComponentsInChildren<Collider>(true);
            var original=initialized ? originalColliderStates : new bool[nextColliders.Length];
            if(!initialized) for(var i=0;i<nextColliders.Length;i++) original[i]=nextColliders[i].enabled;
            return ()=>
            {
                Unbind(); definition=profile; core=owner; colliders=nextColliders; originalColliderStates=original;
                initialized=true; broken=!LocalHealth.IsAlive; lastHit=default; activeRequest=default;
                var zone=GetComponent<Hurtbox>(); zone.Configure(this,zone.Multiplier); Bind();
            };
        }
        internal void RefreshAfterRestore()
        {
            if(!initialized || definition==null) return;
            broken=!LocalHealth.IsAlive;
            var disable=broken && definition.OnBreak==BodyPartBreakBehavior.DisableColliders || core!=null && !core.IsAlive && definition.DisableWithCoreDeath;
            SetColliders(!disable);
            if(definition.OnBreak==BodyPartBreakBehavior.DisableObject) gameObject.SetActive(!broken);
            Bind();
        }
    }
    internal static class LinkedStateSaving
    {
        internal static SavedDamageModifiers Save(CombatDamageModifiers component,SaveCodec codec)=>component!=null ? new SavedDamageModifiers { Incoming=codec.Id(component.Incoming),Outgoing=codec.Id(component.Outgoing) } : null;
        internal static Action Prepare(CombatDamageModifiers component,SavedDamageModifiers saved,SaveCodec codec)
        {
            var outgoing=codec.Get<DamageModifierProfile>(saved.Outgoing); var incoming=codec.Get<DamageModifierProfile>(saved.Incoming);
            SaveCodec.Need((outgoing==null || outgoing.IsValid) && (incoming==null || incoming.IsValid),"Invalid damage modifier profile.");
            return ()=> { component.Outgoing=outgoing; component.Incoming=incoming; };
        }
        internal static SavedRedirector Save(DamageRedirector component,SaveCodec codec)=>component!=null ? new SavedRedirector { Definition=codec.Id(component.Definition),Guardian=codec.Reference(component.Guardian!=null ? component.Guardian.gameObject : null) } : null;
        internal static Action Prepare(DamageRedirector component,SavedRedirector saved,SaveCodec codec)
        {
            var definition=codec.Get<DamageRedirectionDefinition>(saved.Definition); SaveCodec.Need(definition==null || definition.IsValid,"Invalid redirection profile.");
            var source=codec.Source(saved.Guardian); var guardian=source!=null ? source.GetComponent<Health>() : null;
            return ()=> { component.Definition=definition; component.Guardian=guardian; };
        }
    }
}
