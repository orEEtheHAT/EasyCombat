using System;

namespace EasyCombat
{
    public sealed partial class CombatReactions
    {
        internal SavedReactions SaveCombatState(SaveCodec codec)
        {
            var now=CombatClock.Now; var saved=new SavedReactions { Values=new SavedReaction[states.Length] };
            for(var i=0;i<states.Length;i++)
            {
                var state=states[i]; var value=new SavedReaction { Definition=codec.Id(state.Definition),ActorCooldown=Math.Max(0,state.NextActorTime-now),Cooldowns=new SavedReactionCooldown[state.Cooldowns.Count] };
                for(var j=0;j<value.Cooldowns.Length;j++)
                {
                    var cooldown=state.Cooldowns[j]; value.Cooldowns[j]=new SavedReactionCooldown { Remaining=Math.Max(0,cooldown.Until-now),
                        Actor=state.Definition.CooldownScope==ReactionCooldownScope.Target ? codec.Actor(new CombatActorIdentity(cooldown.Key,null)) : null,
                        Action=state.Definition.CooldownScope==ReactionCooldownScope.RootAction ? cooldown.Key : 0 };
                }
                saved.Values[i]=value;
            }
            return saved;
        }
        internal Action PrepareCombatRestore(SavedReactions saved,SaveCodec codec)
        {
            SaveCodec.Need(saved!=null,"Missing reaction snapshot."); SaveCodec.Length(saved.Values,32,"Reactions");
            var next=new State[saved.Values.Length]; var profiles=new ReactionDefinition[next.Length]; var now=CombatClock.Now;
            for(var i=0;i<next.Length;i++)
            {
                var value=saved.Values[i]; SaveCodec.Need(value!=null && SaveCodec.Finite(value.ActorCooldown) && value.ActorCooldown>=0,"Invalid reaction cooldown.");
                var d=codec.Get<ReactionDefinition>(value.Definition,true); SaveCodec.Need(d.IsValid,"Invalid reaction definition."); SaveCodec.Length(value.Cooldowns,128,"Reaction cooldown records");
                var state=new State { Definition=d,NextActorTime=now+value.ActorCooldown };
                foreach(var entry in value.Cooldowns)
                {
                    SaveCodec.Need(SaveCodec.Finite(entry.Remaining) && entry.Remaining>=0 && SaveCodec.Finite(now+entry.Remaining),"Invalid reaction cooldown remaining.");
                    var key=d.CooldownScope==ReactionCooldownScope.Target ? codec.Actor(entry.Actor).Id : codec.Action(entry.Action);
                    state.Cooldowns.Add(new Cooldown { Key=key,Until=now+entry.Remaining });
                }
                next[i]=state; profiles[i]=d;
            }
            return ()=> { states=next; reactions=profiles; configured=true; RejectedByBudget=0; Bind(); };
        }
    }
    public sealed partial class CombatAttribution
    {
        internal SavedAttribution SaveCombatState(SaveCodec codec)
        {
            if(health==null) health=GetComponent<Health>(); var now=CombatClock.Now;
            var saved=new SavedAttribution { Definition=codec.Id(definition),Awarded=awardedLife==health.Identity.Id,Contributions=new SavedContribution[count] };
            for(var i=0;i<count;i++) { var c=contributions[i]; saved.Contributions[i]=new SavedContribution { Actor=codec.Actor(c.Actor),Damage=c.Damage,Age=Math.Max(0,now-c.Time) }; } return saved;
        }
        internal Action PrepareCombatRestore(SavedAttribution saved,SaveCodec codec)
        {
            SaveCodec.Need(saved!=null,"Missing attribution snapshot."); var profile=codec.Get<AttributionDefinition>(saved.Definition);
            SaveCodec.Need(profile==null || profile.IsValid,"Invalid attribution definition."); SaveCodec.Length(saved.Contributions,profile!=null ? profile.MaximumContributors : 0,"Contributors");
            var next=new Contribution[profile!=null ? profile.MaximumContributors : 0]; var now=CombatClock.Now;
            for(var i=0;i<saved.Contributions.Length;i++)
            {
                var c=saved.Contributions[i]; SaveCodec.Need(SaveCodec.Nonnegative(c.Damage) && SaveCodec.Finite(c.Age) && c.Age>=0,"Invalid contribution.");
                next[i]=new Contribution { Actor=codec.Actor(c.Actor),Damage=c.Damage,Time=now-c.Age };
            }
            return ()=> { definition=profile; contributions=next; count=saved.Contributions.Length; Bind(); trackedLife=health.Identity.Id; awardedLife=saved.Awarded ? health.Identity.Id : 0; TruncatedContributors=0; };
        }
    }
}
