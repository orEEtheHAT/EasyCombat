using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>Transient delivery is cancelled on a successful world-state restore, without applying exit damage.</summary>
    public interface ICombatTransient { void CancelForRestore(); }
    public static class CombatSaveSystem
    {
        public const int MaximumActors=4096,MaximumJsonCharacters=16*1024*1024;
        private static bool busy;
        public static bool IsRestoring { get; private set; }
        private static readonly CombatSignal<CombatRestoreResult> restored=new CombatSignal<CombatRestoreResult>();
        public static event Action<CombatRestoreResult> Restored { add=>restored.Add(value); remove=>restored.Remove(value); }
        private static void Available()
        { SaveCodec.Need(!busy && !IsRestoring && !CombatReactionQueue.IsProcessing,"Save/restore is only available between combat transactions."); }
        public static bool TryCapture(CombatDefinitionCatalog catalog,CombatReferenceRegistry references,out CombatWorldSnapshot snapshot,out string error)
        {
            snapshot=null; error=null; var entered=false;
            try
            {
                Available(); busy=true; entered=true;
                var codec=new SaveCodec(catalog,references,false); SaveCodec.Need(references.ActorCount<=MaximumActors,"Actor save budget exceeded.");
                var world=new CombatWorldSnapshot { CatalogCompatibility=catalog.CompatibilityKey,Actors=new CombatActorSnapshot[references.ActorCount] }; var i=0;
                foreach(var pair in references.Actors)
                {
                    var actor=pair.Value; world.Actors[i++]=new CombatActorSnapshot
                    {
                        Id=pair.Key,Health=actor.SaveCombatState(codec),
                        Resources=actor.GetComponent<CombatResources>()?.SaveCombatState(codec),Stats=actor.GetComponent<CombatStats>()?.SaveCombatState(codec),
                        Statuses=actor.GetComponent<StatusController>()?.SaveCombatState(codec),Abilities=actor.GetComponent<CombatAbilities>()?.SaveCombatState(codec),
                        Buildup=actor.GetComponent<CombatBuildup>()?.SaveCombatState(codec),Control=actor.GetComponent<CombatControl>()?.SaveCombatState(codec),
                        Reactions=actor.GetComponent<CombatReactions>()?.SaveCombatState(codec),Attribution=actor.GetComponent<CombatAttribution>()?.SaveCombatState(codec),
                        BodyPart=actor.GetComponent<CombatBodyPart>()?.SaveCombatState(codec),DamageModifiers=LinkedStateSaving.Save(actor.GetComponent<CombatDamageModifiers>(),codec),
                        Redirector=LinkedStateSaving.Save(actor.GetComponent<DamageRedirector>(),codec),Team=actor.TryGetComponent<CombatTeam>(out var team) ? new SavedTeam { Value=team.Team } : null
                    };
                    world.Actors[i-1].CaptureModules();
                }
                snapshot=world; return true;
            }
            catch(Exception exception) { error=exception.Message; return false; }
            finally { if(entered) busy=false; }
        }
        public static bool TryCaptureJson(CombatDefinitionCatalog catalog,CombatReferenceRegistry references,out string json,out string error,bool pretty=false)
        {
            json=null; if(!TryCapture(catalog,references,out var snapshot,out error)) return false;
            try { var value=JsonUtility.ToJson(snapshot,pretty); SaveCodec.Need(value.Length<=MaximumJsonCharacters,"JSON save budget exceeded."); json=value; return true; }
            catch(Exception exception) { error=exception.Message; return false; }
        }
        public static bool TryRestoreJson(string json,CombatDefinitionCatalog catalog,CombatReferenceRegistry references,out CombatRestoreResult result,out string error)
        {
            result=null; error=null;
            try
            {
                SaveCodec.Need(!string.IsNullOrWhiteSpace(json) && json.Length<=MaximumJsonCharacters,"Empty or oversized save JSON.");
                return TryRestore(JsonUtility.FromJson<CombatWorldSnapshot>(json),catalog,references,out result,out error);
            }
            catch(Exception exception) { error=exception.Message; return false; }
        }
        public static bool TryRestore(CombatWorldSnapshot snapshot,CombatDefinitionCatalog catalog,CombatReferenceRegistry references,out CombatRestoreResult result,out string error)
        {
            result=null; error=null; var entered=false;
            try
            {
                Available(); busy=true; entered=true;
                SaveCodec.Need(snapshot!=null && snapshot.Version==1,"Unsupported world snapshot version."); var codec=new SaveCodec(catalog,references,true);
                SaveCodec.Need(snapshot.CatalogCompatibility==catalog.CompatibilityKey,"Catalog compatibility key mismatch; migrate the save first.");
                SaveCodec.Length(snapshot.Actors,MaximumActors,"Actors"); SaveCodec.Need(snapshot.Actors.Length==references.ActorCount,"Registered actor set differs from saved world.");
                var ids=new HashSet<string>(StringComparer.Ordinal); var healthCommits=new List<Action>(); var moduleCommits=new List<Action>(); var actors=new List<Health>();
                foreach(var saved in snapshot.Actors)
                {
                    SaveCodec.Need(saved!=null && !string.IsNullOrWhiteSpace(saved.Id) && ids.Add(saved.Id) && (saved.Modules & ~SavedCombatModules.All)==0,"Missing/duplicate actor ID or unknown module flags.");
                    SaveCodec.Need(references.TryGetActor(saved.Id,out var actor),"Actor not registered: "+saved.Id); actors.Add(actor);
                    healthCommits.Add(actor.PrepareCombatRestore(saved.Health,codec)); var alive=saved.Health.Health.Current>0 || saved.Health.Health.LifeState==CombatLifeState.Downed;
                    Add(actor.GetComponent<CombatResources>(),Present(saved,SavedCombatModules.Resources,saved.Resources),(component,state)=>component.PrepareCombatRestore(state,codec),moduleCommits);
                    Add(actor.GetComponent<CombatStats>(),Present(saved,SavedCombatModules.Stats,saved.Stats),(component,state)=>component.PrepareCombatRestore(state,codec,saved.Id),moduleCommits);
                    Add(actor.GetComponent<StatusController>(),Present(saved,SavedCombatModules.Statuses,saved.Statuses),(component,state)=>component.PrepareCombatRestore(state,codec,alive),moduleCommits);
                    Add(actor.GetComponent<CombatAbilities>(),Present(saved,SavedCombatModules.Abilities,saved.Abilities),(component,state)=>component.PrepareCombatRestore(state,codec,saved.Health.Health.Current>0),moduleCommits);
                    Add(actor.GetComponent<CombatBuildup>(),Present(saved,SavedCombatModules.Buildup,saved.Buildup),(component,state)=>component.PrepareCombatRestore(state,codec),moduleCommits);
                    Add(actor.GetComponent<CombatControl>(),Present(saved,SavedCombatModules.Control,saved.Control),(component,state)=>component.PrepareCombatRestore(state,codec,saved.Id),moduleCommits);
                    Add(actor.GetComponent<CombatReactions>(),Present(saved,SavedCombatModules.Reactions,saved.Reactions),(component,state)=>component.PrepareCombatRestore(state,codec),moduleCommits);
                    Add(actor.GetComponent<CombatAttribution>(),Present(saved,SavedCombatModules.Attribution,saved.Attribution),(component,state)=>component.PrepareCombatRestore(state,codec),moduleCommits);
                    Add(actor.GetComponent<CombatBodyPart>(),Present(saved,SavedCombatModules.BodyPart,saved.BodyPart),(component,state)=>component.PrepareCombatRestore(state,codec),moduleCommits);
                    Add(actor.GetComponent<CombatDamageModifiers>(),Present(saved,SavedCombatModules.DamageModifiers,saved.DamageModifiers),(component,state)=>LinkedStateSaving.Prepare(component,state,codec),moduleCommits);
                    Add(actor.GetComponent<DamageRedirector>(),Present(saved,SavedCombatModules.Redirector,saved.Redirector),(component,state)=>LinkedStateSaving.Prepare(component,state,codec),moduleCommits);
                    Add(actor.GetComponent<CombatTeam>(),Present(saved,SavedCombatModules.Team,saved.Team),(component,state)=>()=>component.Team=state.Value,moduleCommits);
                }
                // All schemas, references, capacities and actor states have been prepared before any mutation.
                IsRestoring=true;
                try
                {
                    foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                        if(component!=null && component is ICombatTransient transient) transient.CancelForRestore();
                    foreach(var commit in healthCommits) commit();
                    foreach(var commit in moduleCommits) commit();
                    foreach(var actor in actors) actor.GetComponent<CombatBodyPart>()?.RefreshAfterRestore();
                }
                finally { IsRestoring=false; }
                result=codec.Result;
                foreach(var actor in actors)
                {
                    if(actor==null) continue; actor.NotifyStateRestored(); if(actor==null) continue;
                    actor.GetComponent<StatusController>()?.NotifyStateRestored(); if(actor!=null) actor.GetComponent<CombatControl>()?.NotifyStateRestored();
                }
                restored.Publish(result); return true;
            }
            catch(Exception exception) { error=exception.Message; return false; }
            finally { if(entered) { IsRestoring=false; busy=false; } }
        }
        private static void Add<T,S>(T component,S saved,Func<T,S,Action> prepare,List<Action> commits) where T:Component where S:class
        {
            SaveCodec.Need((component==null)==(saved==null),"Combat module layout differs: "+typeof(T).Name);
            if(component!=null) commits.Add(prepare(component,saved));
        }
        // JsonUtility materializes empty nested objects for null fields; explicit presence flags preserve module shape.
        private static S Present<S>(CombatActorSnapshot actor,SavedCombatModules flag,S saved) where S:class
        { if((actor.Modules & flag)==0) return null; SaveCodec.Need(saved!=null,"Missing module state: "+flag); return saved; }
    }
}
