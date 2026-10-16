using System;
using System.Collections.Generic;

namespace EasyCombat
{
    public sealed partial class StatusController
    {
        internal SavedStatuses SaveCombatState(SaveCodec codec)
        {
            SaveCodec.Need(!advancing && pendingCount==0,"Cannot save during status processing.");
            var saved=new SavedStatuses { Values=new SavedStatus[entries.Count] };
            for(var i=0;i<entries.Count;i++)
            {
                var entry=entries[i]; var independent=entry.IndependentStacks!=null;
                var value=new SavedStatus { Definition=codec.Id(entry.Definition),Independent=independent,Stacks=entry.Stacks,
                    Records=new SavedStatusStack[independent ? entry.IndependentStacks.Count : 1] };
                if(independent)
                {
                    for(var j=0;j<value.Records.Length;j++)
                    { var stack=entry.IndependentStacks[j]; value.Records[j]=SaveStack(stack.Id,stack.Source,stack.Origin,stack.ExpiresAt,stack.NextTick,stack.Interval,stack.Data,codec); }
                }
                else value.Records[0]=SaveStack(0,entry.Source,entry.Origin,entry.ExpiresAt,entry.NextTick,entry.Interval,entry.Data,codec);
                saved.Values[i]=value;
            }
            return saved;
        }
        private SavedStatusStack SaveStack(long id,UnityEngine.GameObject source,DamageOrigin origin,double expires,double next,double interval,ApplicationData data,SaveCodec codec)
        {
            return new SavedStatusStack { Id=id,Source=codec.SourceId(source,origin),Origin=codec.Origin(origin),ExpiresIn=expires-clock,NextTickIn=next-clock,
                Interval=interval,Data=SaveData(data,codec) };
        }
        private static SavedStatusData SaveData(ApplicationData data,SaveCodec codec)
        {
            SaveCodec.Need(data!=null,"Missing captured status data.");
            var saved=new SavedStatusData { Duration=data.Duration,Interval=data.Interval,Power=data.Power,PlannedTicks=data.PlannedTicks,HasBudget=data.HasBudget,Effects=new SavedEffect[data.Effects.Count] };
            for(var i=0;i<data.Effects.Count;i++)
            {
                var e=data.Effects[i]; saved.Effects[i]=new SavedEffect { Definition=codec.Id(e.Effect),Value=e.Value,Formula=e.Formula.Save(codec),OutgoingMultiplier=e.OutgoingMultiplier,HasOutgoingType=e.HasOutgoingType,OutgoingType=codec.Id(e.OutgoingType) };
            }
            return saved;
        }
        internal Action PrepareCombatRestore(SavedStatuses saved,SaveCodec codec,bool targetAlive)
        {
            SaveCodec.Need(saved!=null && !advancing && pendingCount==0,"Invalid/busy status restore."); SaveCodec.Length(saved.Values,maximumActiveStatuses,"Status groups");
            SaveCodec.Need(targetAlive || saved.Values.Length==0,"Dead actors cannot retain active statuses.");
            var next=new Entry[saved.Values.Length]; var seen=new HashSet<StatusDefinition>(); var ids=new HashSet<long>(); var records=0; var largestId=nextStackId;
            for(var i=0;i<next.Length;i++)
            {
                var value=saved.Values[i]; SaveCodec.Need(value!=null,"Missing status group."); var definition=codec.Get<StatusDefinition>(value.Definition,true);
                SaveCodec.Need(definition.IsValid && seen.Add(definition) && value.Independent==(definition.Stacking==StatusStacking.Independent),"Status stacking configuration changed.");
                SaveCodec.Length(value.Records,maximumIndependentStackRecords,"Status records"); SaveCodec.Need(value.Records.Length>0,"Empty status group.");
                var entry=new Entry { Definition=definition,Stacks=value.Stacks };
                if(value.Independent) { records+=value.Records.Length; entry.IndependentStacks=new List<StackState>(value.Records.Length); }
                else SaveCodec.Need(value.Records.Length==1 && value.Stacks>0 && value.Stacks<=definition.MaximumStacks,"Invalid shared status stacks.");
                var live=0;
                foreach(var record in value.Records)
                {
                    SaveCodec.Need(record!=null && SaveCodec.Finite(record.ExpiresIn) && SaveCodec.Finite(record.NextTickIn) && SaveCodec.Finite(record.Interval) && record.Interval>=.01f,"Invalid status timer.");
                    var data=LoadData(record.Data,definition,codec); var source=codec.Source(record.Source); var origin=codec.Origin(record.Origin);
                    SaveCodec.Need(origin.Status==definition,"Status origin definition mismatch.");
                    if(value.Independent)
                    {
                        SaveCodec.Need(record.Id>0 && record.Id<long.MaxValue-4096 && ids.Add(record.Id),"Invalid/duplicate stack ID."); largestId=Math.Max(largestId,record.Id); if(record.ExpiresIn>0) live++;
                        entry.IndependentStacks.Add(new StackState { Id=record.Id,Source=source,Origin=origin,ExpiresAt=record.ExpiresIn,NextTick=record.NextTickIn,Interval=record.Interval,Data=data });
                    }
                    else { entry.Source=source; entry.Origin=origin; entry.ExpiresAt=record.ExpiresIn; entry.NextTick=record.NextTickIn; entry.Interval=record.Interval; entry.Data=data; }
                }
                SaveCodec.Need(live<=definition.MaximumStacks,"Too many live independent stacks."); next[i]=entry;
            }
            SaveCodec.Need(records<=maximumIndependentStackRecords,"Total status record budget exceeded.");
            return ()=>
            {
                BindHealth(); foreach(var entry in entries) ReleaseStacks(entry); entries.Clear();
                clock=0; independentRecordCount=records; nextStackId=largestId; clearRequested=false; modifiersDirty=true;
                entries.AddRange(next); ReconcileScheduler();
            };
        }
        private static ApplicationData LoadData(SavedStatusData saved,StatusDefinition definition,SaveCodec codec)
        {
            SaveCodec.Need(saved!=null && CombatMath.Positive(saved.Duration) && CombatMath.Finite(saved.Interval) && saved.Interval>=.01f
                && SaveCodec.Nonnegative(saved.Power) && SaveCodec.Finite(saved.PlannedTicks) && saved.PlannedTicks>=0 && (!saved.HasBudget || saved.PlannedTicks>=1),"Invalid captured status data.");
            var effects=definition.TickEffects; SaveCodec.Need(saved.Effects!=null && saved.Effects.Length==(effects?.Length ?? 0),"Status effects changed; migrate the save/catalog.");
            var data=new ApplicationData { Duration=saved.Duration,Interval=saved.Interval,Power=saved.Power,PlannedTicks=saved.PlannedTicks,HasBudget=saved.HasBudget };
            for(var i=0;i<saved.Effects.Length;i++)
            {
                var value=saved.Effects[i]; SaveCodec.Need(value!=null,"Missing status effect data."); var effect=codec.Get<StatusTickEffect>(value.Definition);
                SaveCodec.Need(effect==effects[i] && (effect==null || effect.IsValid) && CombatMath.Finite(value.Value) && SaveCodec.Nonnegative(value.OutgoingMultiplier),"Invalid captured status effect.");
                data.Effects.Add(new CapturedEffect { Effect=effect,Value=value.Value,Formula=DamageFormulaSnapshot.Load(value.Formula,codec),OutgoingMultiplier=value.OutgoingMultiplier,
                    HasOutgoingType=value.HasOutgoingType,OutgoingType=codec.Get<DamageTypeDefinition>(value.OutgoingType) });
            }
            return data;
        }
        internal void NotifyStateRestored()=>changedSignal.Publish();
    }
}
