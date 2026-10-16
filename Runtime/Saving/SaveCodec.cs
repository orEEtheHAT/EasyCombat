using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    internal sealed class SaveCodec
    {
        internal readonly CombatDefinitionCatalog Catalog;
        internal readonly CombatReferenceRegistry References;
        internal readonly CombatRestoreResult Result=new CombatRestoreResult();
        private readonly Dictionary<long,string> actors=new Dictionary<long,string>();
        private readonly Dictionary<Health,long> futureLives=new Dictionary<Health,long>();
        private readonly Dictionary<long,long> actions=new Dictionary<long,long>();
        private readonly Dictionary<long,CombatActorIdentity> historical=new Dictionary<long,CombatActorIdentity>();
        internal SaveCodec(CombatDefinitionCatalog catalog,CombatReferenceRegistry references,bool restoring)
        {
            Need(catalog!=null && catalog.Validate(),"A valid definition catalog is required."); Need(references!=null,"A reference registry is required.");
            Catalog=catalog; References=references;
            foreach(var pair in references.Actors)
            { Need(pair.Value!=null,"Destroyed registered actor: "+pair.Key); actors[pair.Value.Identity.Id]=pair.Key; if(restoring) futureLives[pair.Value]=CombatEvents.NextId(); }
            foreach(var pair in references.References)
            {
                var go=pair.Value as GameObject; if(go==null) continue; var identity=CombatActorIdentity.Capture(go);
                if(!actors.ContainsKey(identity.Id)) actors[identity.Id]=pair.Key;
            }
        }
        internal static void Need(bool condition,string message) { if(!condition) throw new ArgumentException(message); }
        internal static bool Nonnegative(float value)=>CombatMath.Finite(value) && value>=0;
        internal static bool Finite(double value)=>!double.IsNaN(value) && !double.IsInfinity(value);
        internal static void Length(Array values,int maximum,string label) =>Need(values!=null && values.Length<=maximum,label+" exceeds its budget or is null.");
        internal string Id(ScriptableObject definition)
        { if(definition==null) return null; Need(Catalog.TryGetId(definition,out var id),"Definition missing from catalog: "+definition.name); return id; }
        internal T Get<T>(string id,bool required=false) where T:ScriptableObject
        {
            if(string.IsNullOrEmpty(id)) { Need(!required,"Missing "+typeof(T).Name+" ID."); return null; }
            Need(Catalog.TryResolve<T>(id,out var value),"Unknown or wrong definition type: "+id); return value;
        }
        internal string Reference(object value)
        {
            if(value==null || value is UnityEngine.Object unity && unity==null) return null;
            Need(References.TryGetId(value,out var id),"Object is not registered with a stable source ID: "+value); return id;
        }
        internal object Resolve(string id,bool required=false)
        {
            if(string.IsNullOrEmpty(id)) { Need(!required,"Missing object ID."); return null; }
            if(!References.TryResolve(id,out var value) || value is UnityEngine.Object unity && unity==null)
            { Need(!required,"Object not registered: "+id); return null; }
            return value;
        }
        internal GameObject Source(string id)
        { var value=Resolve(id); Need(value==null || value is GameObject,"Source ID must refer to a GameObject: "+id); return value as GameObject; }
        internal long Life(Health actor)=>actor!=null ? futureLives.TryGetValue(actor,out var id) ? id : actor.Identity.Id : 0;
        internal long SourceLife(GameObject actor) { var health=actor!=null ? actor.GetComponentInParent<Health>() : null; return health!=null ? Life(health) : CombatSources.IdentityId(actor); }
        internal string SourceId(GameObject source,DamageOrigin origin)=>Reference(CombatSources.ForLife(source,origin.Actor.Id));
        internal SavedActorIdentity Actor(CombatActorIdentity identity)
        { actors.TryGetValue(identity.Id,out var id); return new SavedActorIdentity { Reference=id,Name=identity.Name,HistoricalKey=identity.Id }; }
        internal CombatActorIdentity Actor(SavedActorIdentity saved)
        {
            Need(saved!=null,"Missing actor identity.");
            if(saved.HistoricalKey==0 && string.IsNullOrEmpty(saved.Reference)) return default;
            var go=Source(saved.Reference);
            if(go!=null)
            { var health=go.GetComponentInParent<Health>(); return health!=null ? new CombatActorIdentity(Life(health),saved.Name) : CombatActorIdentity.Capture(go); }
            if(!historical.TryGetValue(saved.HistoricalKey,out var identity))
            { identity=new CombatActorIdentity(CombatEvents.NextId(),saved.Name); historical.Add(saved.HistoricalKey,identity); }
            return identity;
        }
        internal long Action(long saved)
        {
            Need(saved>=0,"Action ID must be nonnegative."); if(saved==0) return 0;
            if(!actions.TryGetValue(saved,out var value)) { value=CombatEvents.NextId(); actions.Add(saved,value); } return value;
        }
        internal SavedOrigin Origin(DamageOrigin origin)
        {
            var tags=new List<string>(); for(var i=0;i<CombatTagSet.Capacity;i++) { var tag=origin.Action.Tags.Get(i); if(tag!=null) tags.Add(Id(tag)); }
            return new SavedOrigin { Actor=Actor(origin.Actor),Attack=Id(origin.Attack),Status=Id(origin.Status),Cast=origin.CastId,
                Action=origin.Action.Id,Root=origin.Action.RootId,Parent=origin.Action.ParentId,Depth=origin.Action.Depth,ReactionDepth=origin.Action.ReactionDepth,
                Kind=origin.Action.Kind,Tags=tags.ToArray(),HasTeam=origin.HasTeam,Team=origin.Team };
        }
        internal DamageOrigin Origin(SavedOrigin saved)
        {
            Need(saved!=null,"Missing damage origin."); Length(saved.Tags,CombatTagSet.Capacity,"Origin tags");
            Need(saved.Depth>=0 && saved.Depth<=1024 && saved.ReactionDepth>=0 && saved.ReactionDepth<=saved.Depth
                && saved.Kind>=CombatOriginKind.Attack && saved.Kind<=CombatOriginKind.Ability,"Invalid action ancestry.");
            var tags=new CombatTagDefinition[saved.Tags.Length]; for(var i=0;i<tags.Length;i++) tags[i]=Get<CombatTagDefinition>(saved.Tags[i],true);
            var action=new CombatActionOrigin(Action(saved.Action),Action(saved.Root),Action(saved.Parent),saved.Depth,saved.Kind,new CombatTagSet(tags),saved.ReactionDepth);
            return new DamageOrigin(Actor(saved.Actor),Get<AttackDefinition>(saved.Attack),Action(saved.Cast),Get<StatusDefinition>(saved.Status),saved.HasTeam,saved.Team,action);
        }
    }
    public partial struct DamageFormulaSnapshot
    {
        internal SavedFormula Save(SaveCodec codec)
        {
            if(Count==0) return null;
            var liveSource=CombatSources.ForLife(source,sourceLife);
            var saved=new SavedFormula { Present=true,Source=codec.Reference(liveSource),Scale=scale,Critical=IsCritical,Terms=new SavedFormulaTerm[Count] };
            for(var i=0;i<Count;i++) { var term=Get(i); saved.Terms[i]=new SavedFormulaTerm { Type=codec.Id(term.Type),Basis=term.Basis,Value=term.Value,Stat=codec.Id(term.Stat),Multiplier=term.Multiplier,Limit=term.Limit }; }
            return saved;
        }
        internal static DamageFormulaSnapshot Load(SavedFormula saved,SaveCodec codec)
        {
            if(saved==null || !saved.Present) return default;
            SaveCodec.Length(saved.Terms,8,"Formula terms"); SaveCodec.Need(saved.Terms.Length>0 && SaveCodec.Nonnegative(saved.Scale),"Invalid formula scale/count.");
            var source=codec.Source(saved.Source); var formula=new DamageFormulaSnapshot(source,saved.Critical); formula.scale=saved.Scale; formula.sourceLife=codec.SourceLife(source);
            foreach(var term in saved.Terms)
            {
                var stat=codec.Get<CombatStatDefinition>(term.Stat);
                SaveCodec.Need(term.Basis>=StatusDamageBasis.Fixed && term.Basis<=StatusDamageBasis.SourceStat && term.Basis!=StatusDamageBasis.TriggeringHit
                    && SaveCodec.Nonnegative(term.Value) && SaveCodec.Nonnegative(term.Multiplier) && SaveCodec.Nonnegative(term.Limit)
                    && (term.Basis!=StatusDamageBasis.SourceStat || stat!=null),"Invalid prepared formula term.");
                formula.Add(new PreparedDamageTerm(codec.Get<DamageTypeDefinition>(term.Type),term.Basis,term.Value,stat,term.Multiplier,term.Limit));
            }
            SaveCodec.Need(formula.IsValid,"Invalid formula snapshot."); return formula;
        }
    }
}
