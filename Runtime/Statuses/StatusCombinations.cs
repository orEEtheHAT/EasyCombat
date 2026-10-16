using System.Buffers;
using UnityEngine;

namespace EasyCombat
{
    public sealed partial class StatusController
    {
        /// <summary>Atomically exchange live stacks for one prepared status application. Failed chance/capture/capacity changes nothing.</summary>
        public bool TryCombine(StatusCombinationDefinition recipe,GameObject source=null,DamageResult trigger=default,bool hasHit=false,DamageOrigin actionOrigin=default)
        {
            if(advancing || recipe==null || !recipe.IsValid || GetStacks(recipe.Consume)<recipe.Stacks) return false;
            var origin=actionOrigin.CastId!=0 ? actionOrigin : hasHit ? trigger.Damage.Origin : default;
            if(!TryCaptureData(recipe.Apply,source,ref origin,trigger,hasHit,out var data)) return false;
            var index=Find(recipe.Consume);
            if(index<0 || GetStacks(recipe.Consume)<recipe.Stacks) { ReleaseData(data); return false; }
            var original=entries[index];
            if(original.IndependentStacks==null && original.ExpiresAt<=clock) { ReleaseData(data); return false; }
            var before=ActiveStacks(original); var resultBefore=GetStacks(recipe.Apply);
            var removed=original.IndependentStacks!=null ? ArrayPool<StackState>.Shared.Rent(recipe.Stacks) : null;
            var positions=original.IndependentStacks!=null ? ArrayPool<int>.Shared.Rent(recipe.Stacks) : null;
            var removedGroup=false; var accepted=false;
            if(original.IndependentStacks!=null)
            {
                for(var n=0;n<recipe.Stacks;n++)
                {
                    var earliest=-1;
                    for(var i=0;i<original.IndependentStacks.Count;i++)
                    {
                        var stack=original.IndependentStacks[i]; if(stack.ExpiresAt<=clock) continue;
                        if(earliest<0 || stack.ExpiresAt<original.IndependentStacks[earliest].ExpiresAt) earliest=i;
                    }
                    positions[n]=earliest; removed[n]=original.IndependentStacks[earliest]; original.IndependentStacks.RemoveAt(earliest);
                }
                independentRecordCount-=recipe.Stacks; removedGroup=original.IndependentStacks.Count==0;
            }
            else
            {
                var reduced=original; reduced.Stacks-=recipe.Stacks; removedGroup=reduced.Stacks==0;
                if(!removedGroup) entries[index]=reduced;
            }
            if(removedGroup) { entries[index]=entries[entries.Count-1]; entries.RemoveAt(entries.Count-1); }
            try
            {
                accepted=ApplyCaptured(recipe.Apply,source,origin,data);
            }
            finally
            {
                if(!accepted)
                {
                    if(removed!=null)
                    {
                        for(var n=recipe.Stacks-1;n>=0;n--) original.IndependentStacks.Insert(positions[n],removed[n]);
                        independentRecordCount+=recipe.Stacks;
                    }
                    if(removedGroup) entries.Add(original); else entries[index]=original;
                    ReleaseData(data);
                }
                else if(removed!=null)
                {
                    for(var n=0;n<recipe.Stacks;n++) ReleaseData(removed[n].Data);
                    if(removedGroup) stackLists.Push(original.IndependentStacks);
                }
                else if(removedGroup) ReleaseData(original.Data);
                if(removed!=null) { ArrayPool<StackState>.Shared.Return(removed,true); ArrayPool<int>.Shared.Return(positions,true); }
            }
            if(!accepted) return false;
            modifiersDirty=true; ReconcileScheduler();
            var resultAfter=GetStacks(recipe.Apply);
            var kind=resultBefore==0 ? StatusChangeKind.Applied : resultAfter>resultBefore ? StatusChangeKind.StackAdded
                : recipe.Apply.Stacking==StatusStacking.Independent ? StatusChangeKind.StackReplaced : StatusChangeKind.Refreshed;
            var appliedChange=new StatusChange(this,recipe.Apply,kind,resultBefore,resultAfter,origin,recipe.Apply.Stacking==StatusStacking.Independent ? nextStackId : 0);
            lifecycle.Publish(new StatusChange(this,recipe.Consume,StatusChangeKind.Removed,before,before-recipe.Stacks,reason:StatusRemovalReason.Consumed));
            lifecycle.Publish(appliedChange);
            changedSignal.Publish(); return true;
        }
    }
}
