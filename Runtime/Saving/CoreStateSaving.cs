using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    public sealed partial class Health
    {
        private readonly CombatSignal<Health> stateRestoredSignal=new CombatSignal<Health>();
        public event Action<Health> StateRestored { add=>stateRestoredSignal.Add(value); remove=>stateRestoredSignal.Remove(value); }
        internal void NotifyStateRestored()=>stateRestoredSignal.Publish(this);
        internal SavedHealth SaveCombatState(SaveCodec codec)
        {
            EnsureInitialized(); SaveCodec.Need(!applyingDamage && !applyingHealing,"Cannot save during a health transaction.");
            return new SavedHealth { Definition=codec.Id(definition),ResistanceProfile=codec.Id(Resistances),HitRules=codec.Id(HitRules),DefenseProfile=codec.Id(DefenseProfile),
                Health=CaptureState(),ManualInvulnerability=manualInvulnerability,HitInvulnerability=hitInvulnerability,Invulnerable=invulnerable,
                Defenses=defense!=null ? defense.SaveCombatState(codec) : Array.Empty<SavedDefense>() };
        }
        internal Action PrepareCombatRestore(SavedHealth saved,SaveCodec codec)
        {
            EnsureInitialized(); SaveCodec.Need(saved!=null && !applyingDamage && !applyingHealing,"Invalid/busy health restore.");
            var hp=saved.Health;
            SaveCodec.Need((hp.Version==1 || hp.Version==2) && CombatMath.Positive(hp.Maximum) && SaveCodec.Nonnegative(hp.Current) && hp.Current<=hp.Maximum
                && hp.LifeState>=CombatLifeState.Alive && hp.LifeState<=CombatLifeState.Dead && SaveCodec.Nonnegative(saved.ManualInvulnerability) && SaveCodec.Nonnegative(saved.HitInvulnerability),"Invalid health snapshot.");
            var profile=codec.Get<HealthDefinition>(saved.Definition); var resistance=codec.Get<ResistanceProfile>(saved.ResistanceProfile);
            var rules=codec.Get<HitRulesDefinition>(saved.HitRules); var protection=codec.Get<DefenseProfile>(saved.DefenseProfile);
            SaveCodec.Need((profile==null || profile.IsValid) && (rules==null || rules.IsValid) && (protection==null || protection.IsValid),"Invalid health configuration.");
            SaveCodec.Need(profile==null || profile.Resistances==resistance && profile.HitRules==rules && profile.Defense==protection,"Health profile references changed; migrate the save/catalog.");
            var life=hp.Version==2 && hp.LifeState==CombatLifeState.Downed ? CombatLifeState.Downed : hp.Current>0 ? CombatLifeState.Alive : CombatLifeState.Dead;
            SaveCodec.Need(life!=CombatLifeState.Downed || hp.Current==0 && profile!=null && profile.LifeRules!=null && profile.LifeRules.AllowDowned,"Downed state requires compatible life rules.");
            var nextDefense=protection!=null ? new DamageDefenseState(protection) : null;
            SaveCodec.Length(saved.Defenses,EasyCombat.DefenseProfile.MaximumLayers,"Defense states");
            if(nextDefense!=null) nextDefense.LoadCombatState(saved.Defenses,codec); else SaveCodec.Need(saved.Defenses.Length==0,"Unexpected defense states.");
            var nextIdentity=new CombatActorIdentity(codec.Life(this),identity.Name);
            return ()=>
            {
                definition=profile; resistances=resistance; hitRules=rules; defenseProfile=protection; defense=nextDefense;
                maximumHealth=hp.Maximum; currentHealth=hp.Current; lifeState=life; identity=nextIdentity;
                manualInvulnerability=saved.ManualInvulnerability; hitInvulnerability=saved.HitInvulnerability; invulnerable=saved.Invulnerable; ReconcileDefenseScheduler();
            };
        }
    }
    public sealed partial class DamageDefenseState
    {
        internal SavedDefense[] SaveCombatState(SaveCodec codec)
        {
            var saved=new SavedDefense[Count]; for(var i=0;i<Count;i++) saved[i]=new SavedDefense { Definition=codec.Id(slots[i].Layer),Stage=slots[i].Stage,State=states[i] }; return saved;
        }
        internal void LoadCombatState(SavedDefense[] saved,SaveCodec codec)
        {
            SaveCodec.Need(saved.Length==Count,"Defense slot count changed.");
            for(var i=0;i<Count;i++)
            {
                SaveCodec.Need(codec.Get<DefenseLayerDefinition>(saved[i].Definition,true)==slots[i].Layer && saved[i].Stage==slots[i].Stage && Valid(saved[i].State),"Invalid or mismatched defense slot.");
                states[i]=saved[i].State;
            }
        }
    }
    public sealed partial class CombatResources
    {
        internal SavedResources SaveCombatState(SaveCodec codec)
        {
            SaveCodec.Need(!mutating,"Cannot save during a resource transaction.");
            var saved=new SavedResources { Profile=codec.Id(profile),Values=new SavedResource[states.Length] };
            for(var i=0;i<states.Length;i++)
            { var s=states[i]; saved.Values[i]=new SavedResource { Definition=codec.Id(s.Key),MaximumStat=codec.Id(s.MaximumStat),Current=s.Current,Maximum=s.Maximum,InitialFraction=s.InitialFraction,Regeneration=s.Regen,SpendDelay=s.SpendDelay,RemainingDelay=s.Delay }; }
            return saved;
        }
        internal Action PrepareCombatRestore(SavedResources saved,SaveCodec codec)
        {
            SaveCodec.Need(saved!=null && !mutating,"Invalid/busy resource restore."); SaveCodec.Length(saved.Values,Capacity,"Resources");
            var definition=codec.Get<ResourceProfile>(saved.Profile); SaveCodec.Need(definition==null || definition.IsValid,"Invalid resource profile.");
            SaveCodec.Need(definition!=null ? definition.Resources.Length==saved.Values.Length : saved.Values.Length==0,"Resource profile count changed.");
            var next=new State[saved.Values.Length]; var seen=new HashSet<ResourceDefinition>();
            for(var i=0;i<next.Length;i++)
            {
                var s=saved.Values[i]; var key=codec.Get<ResourceDefinition>(s.Definition,true);
                SaveCodec.Need(key.IsValid && seen.Add(key) && definition.Resources[i]==key && CombatMath.Positive(s.Maximum) && SaveCodec.Nonnegative(s.Current) && s.Current<=s.Maximum
                    && SaveCodec.Nonnegative(s.InitialFraction) && s.InitialFraction<=1 && SaveCodec.Nonnegative(s.Regeneration) && SaveCodec.Nonnegative(s.SpendDelay) && SaveCodec.Nonnegative(s.RemainingDelay),"Invalid resource state.");
                next[i]=new State { Key=key,MaximumStat=codec.Get<CombatStatDefinition>(s.MaximumStat),Current=s.Current,Maximum=s.Maximum,InitialFraction=s.InitialFraction,Regen=s.Regeneration,SpendDelay=s.SpendDelay,Delay=s.RemainingDelay };
            }
            return ()=> { states=next; profile=definition; stats=GetComponent<CombatStats>(); };
        }
    }
    public sealed partial class CombatStats
    {
        internal SavedStats SaveCombatState(SaveCodec codec)
        {
            var saved=new SavedStats { Profile=codec.Id(profile),Values=new SavedStatValue[values?.Length ?? 0],Modifiers=new SavedModifier[modifiers.Count] };
            for(var i=0;i<saved.Values.Length;i++) saved.Values[i]=new SavedStatValue { Definition=codec.Id(values[i].Stat),Value=values[i].Value };
            for(var i=0;i<modifiers.Count;i++)
            { var m=modifiers[i]; saved.Modifiers[i]=new SavedModifier { Group=m.Id,Source=codec.Reference(m.Source),Stat=codec.Id(m.Modifier.Stat),Operation=m.Modifier.Operation,Value=m.Modifier.Value }; }
            return saved;
        }
        internal Action PrepareCombatRestore(SavedStats saved,SaveCodec codec,string actorId)
        {
            SaveCodec.Need(saved!=null,"Missing stats snapshot."); SaveCodec.Length(saved.Values,16384,"Stat values"); SaveCodec.Length(saved.Modifiers,maximumModifiers,"Stat modifiers");
            var definition=codec.Get<CombatStatsDefinition>(saved.Profile); SaveCodec.Need(definition==null || definition.IsValid,"Invalid stat profile.");
            var nextValues=new CombatStatValue[saved.Values.Length];
            for(var i=0;i<nextValues.Length;i++)
            { var value=saved.Values[i]; SaveCodec.Need(SaveCodec.Nonnegative(value.Value),"Invalid stat value."); nextValues[i]=new CombatStatValue(codec.Get<CombatStatDefinition>(value.Definition,true),value.Value); }
            var next=new Entry[saved.Modifiers.Length]; var groups=new Dictionary<long,long>(); var sources=new Dictionary<long,object>();
            for(var i=0;i<next.Length;i++)
            {
                var value=saved.Modifiers[i]; var modifier=new StatModifier(codec.Get<CombatStatDefinition>(value.Stat,true),value.Operation,value.Value);
                SaveCodec.Need(modifier.IsValid && value.Group>0,"Invalid modifier."); var source=codec.Resolve(value.Source,!string.IsNullOrEmpty(value.Source));
                if(!groups.TryGetValue(value.Group,out var group))
                {
                    group=CombatEvents.NextId(); groups.Add(value.Group,group); sources.Add(value.Group,source);
                    codec.Result.Modifiers.Add(new RestoredModifierHandle(actorId,value.Group,new StatModifierHandle(this,group)));
                }
                else SaveCodec.Need(ReferenceEquals(sources[value.Group],source),"A modifier group has conflicting owners.");
                next[i]=new Entry { Id=group,Source=source,Modifier=modifier };
            }
            return ()=> { profile=definition; values=nextValues; modifiers.Clear(); modifiers.AddRange(next); Revision++; };
        }
    }
}
