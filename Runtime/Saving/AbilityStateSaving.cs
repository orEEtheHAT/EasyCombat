using System;
using System.Collections.Generic;

namespace EasyCombat
{
    public sealed partial class CombatAbilities
    {
        internal SavedAbilities SaveCombatState(SaveCodec codec)
        {
            SaveCodec.Need(!mutating,"Cannot save during ability execution.");
            var saved=new SavedAbilities { Values=new SavedAbility[states.Length],GlobalCooldown=globalCooldown };
            for(var i=0;i<states.Length;i++) { var s=states[i]; saved.Values[i]=new SavedAbility { Definition=codec.Id(s.Definition),Charges=s.Charges,Cooldown=s.Cooldown,Recharge=s.Recharge }; }
            if(IsCasting)
            {
                saved.Casting=codec.Id(Casting); saved.CastRemaining=castRemaining; saved.CastPoint=castPoint; saved.CastOrigin=codec.Origin(castOrigin);
                saved.Target=castTarget!=null && castTarget.Identity.Id==targetLife ? codec.Reference(castTarget.gameObject) : null;
            }
            return saved;
        }
        internal Action PrepareCombatRestore(SavedAbilities saved,SaveCodec codec,bool standing)
        {
            SaveCodec.Need(saved!=null && !mutating,"Invalid/busy ability restore."); SaveCodec.Length(saved.Values,32,"Abilities");
            SaveCodec.Need(SaveCodec.Nonnegative(saved.GlobalCooldown),"Invalid global cooldown.");
            var next=new State[saved.Values.Length]; var definitions=new AbilityDefinition[next.Length]; var seen=new HashSet<AbilityDefinition>();
            for(var i=0;i<next.Length;i++)
            {
                var s=saved.Values[i]; var d=codec.Get<AbilityDefinition>(s.Definition,true);
                SaveCodec.Need(d.IsValid && seen.Add(d) && s.Charges>=0 && s.Charges<=d.MaximumCharges && SaveCodec.Nonnegative(s.Cooldown) && SaveCodec.Nonnegative(s.Recharge)
                    && (d.MaximumCharges==0 || s.Charges==d.MaximumCharges || s.Recharge>0),"Invalid ability state.");
                definitions[i]=d; next[i]=new State { Definition=d,Charges=s.Charges,Cooldown=s.Cooldown,Recharge=s.Recharge };
            }
            var cast=codec.Get<AbilityDefinition>(saved.Casting); var index=Array.IndexOf(definitions,cast); var targetObject=codec.Source(saved.Target);
            var target=targetObject!=null ? targetObject.GetComponent<Health>() : null; var origin=default(DamageOrigin); var life=0L;
            if(cast!=null)
            {
                SaveCodec.Need(index>=0 && standing && CombatMath.Positive(saved.CastRemaining) && saved.CastRemaining<=cast.CastTime && CombatMath.Finite(saved.CastPoint),"Invalid active cast.");
                origin=codec.Origin(saved.CastOrigin); SaveCodec.Need(origin.Actor.Id==codec.Life(Owner) && origin.CastId!=0,"Cast source mismatch.");
                if(cast.TargetMode==AbilityTargetMode.Self) target=Owner;
                if(cast.TargetMode==AbilityTargetMode.Unit && target==null) index=-1; // A disappeared external target cancels the unpaid windup.
                life=codec.Life(target);
            }
            else index=-1;
            return ()=>
            {
                states=next; abilities=definitions; configured=true; globalCooldown=saved.GlobalCooldown; resetRequested=false; ClearCast();
                if(index>=0) { casting=index; castRemaining=saved.CastRemaining; castPoint=saved.CastPoint; castTarget=target; targetLife=life; castOrigin=origin; }
                Bind(); Reconcile();
            };
        }
    }
    public sealed partial class CombatBuildup
    {
        internal SavedBuildup SaveCombatState(SaveCodec codec)
        {
            SaveCodec.Need(!mutating,"Cannot save during buildup processing."); var saved=new SavedBuildup { Values=new SavedMeter[states.Length] };
            for(var i=0;i<states.Length;i++) { var s=states[i]; saved.Values[i]=new SavedMeter { Definition=codec.Id(s.Definition),Value=s.Value,Delay=s.Delay,Cooldown=s.Cooldown }; } return saved;
        }
        internal Action PrepareCombatRestore(SavedBuildup saved,SaveCodec codec)
        {
            SaveCodec.Need(saved!=null && !mutating,"Invalid/busy buildup restore."); SaveCodec.Length(saved.Values,16,"Buildup meters");
            var next=new State[saved.Values.Length]; var profiles=new BuildupDefinition[next.Length]; var seen=new HashSet<BuildupDefinition>();
            for(var i=0;i<next.Length;i++)
            {
                var s=saved.Values[i]; var d=codec.Get<BuildupDefinition>(s.Definition,true);
                SaveCodec.Need(d.IsValid && seen.Add(d) && SaveCodec.Nonnegative(s.Value) && s.Value<=d.Threshold && SaveCodec.Nonnegative(s.Delay) && SaveCodec.Nonnegative(s.Cooldown),"Invalid buildup state.");
                profiles[i]=d; next[i]=new State { Definition=d,Value=s.Value,Delay=s.Delay,Cooldown=s.Cooldown };
            }
            return ()=> { states=next; definitions=profiles; configured=true; resetRequested=false; RejectedThresholds=0; Bind(); Reconcile(); };
        }
    }
    public sealed partial class CombatControl
    {
        internal SavedControl SaveCombatState(SaveCodec codec)
        {
            var saved=new SavedControl { Values=new SavedRestriction[leases.Count] };
            for(var i=0;i<leases.Count;i++) saved.Values[i]=new SavedRestriction { Key=leases[i].Id,Restriction=leases[i].Restriction }; return saved;
        }
        internal Action PrepareCombatRestore(SavedControl saved,SaveCodec codec,string actor)
        {
            SaveCodec.Need(saved!=null,"Missing control snapshot."); SaveCodec.Length(saved.Values,64,"Control leases");
            var next=new Lease[saved.Values.Length]; var seen=new HashSet<long>(); var combined=CombatRestriction.None;
            for(var i=0;i<next.Length;i++)
            {
                var s=saved.Values[i]; SaveCodec.Need(s.Key>0 && seen.Add(s.Key) && s.Restriction!=CombatRestriction.None && (s.Restriction & ~CombatRestriction.All)==0,"Invalid control lease.");
                var id=CombatEvents.NextId(); next[i]=new Lease { Id=id,Restriction=s.Restriction }; combined|=s.Restriction;
                codec.Result.Controls.Add(new RestoredControlHandle(actor,s.Key,new ControlHandle(this,id)));
            }
            return ()=> { leases.Clear(); leases.AddRange(next); manual=combined; };
        }
        internal void NotifyStateRestored()=>changed.Publish(Restrictions);
    }
}
