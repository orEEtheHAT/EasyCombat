using System;
using UnityEngine;

namespace EasyCombat
{
    public readonly struct CombatContributor
    {
        public readonly CombatActorIdentity Actor;
        public readonly float Damage;
        public CombatContributor(CombatActorIdentity actor,float damage) { Actor=actor; Damage=damage; }
    }
    public struct CombatAssists
    {
        public const int Capacity=8;
        private CombatContributor a,b,c,d,e,f,g,h;
        public int Count { get; private set; }
        public CombatContributor this[int i] => i>=0 && i<Count ? i switch { 0=>a,1=>b,2=>c,3=>d,4=>e,5=>f,6=>g,_=>h } : throw new ArgumentOutOfRangeException(nameof(i));
        internal void Add(CombatContributor value)
        {
            switch(Count++) { case 0:a=value;break;case 1:b=value;break;case 2:c=value;break;case 3:d=value;break;case 4:e=value;break;case 5:f=value;break;case 6:g=value;break;case 7:h=value;break;default:throw new InvalidOperationException("Assist budget exceeded"); }
        }
    }
    public readonly struct KillCredit
    {
        public readonly CombatActorIdentity Victim,Killer;
        public readonly CombatAssists Assists;
        public readonly DamageOrigin FinalHit;
        public KillCredit(CombatActorIdentity victim,CombatActorIdentity killer,CombatAssists assists,DamageOrigin finalHit)
        { Victim=victim; Killer=killer; Assists=assists; FinalHit=finalHit; }
    }
    [DisallowMultipleComponent,RequireComponent(typeof(Health)),AddComponentMenu("EasyCombat/Life/Combat Attribution")]
    public sealed partial class CombatAttribution : MonoBehaviour
    {
        [SerializeField] private AttributionDefinition definition;
        private struct Contribution { public CombatActorIdentity Actor; public float Damage; public double Time; }
        private Contribution[] contributions=Array.Empty<Contribution>();
        private int count;
        private long awardedLife,trackedLife;
        private Health health;
        private readonly CombatSignal<KillCredit> credited=new CombatSignal<KillCredit>();
        public event Action<KillCredit> Credited { add=>credited.Add(value); remove=>credited.Remove(value); }
        public int TruncatedContributors { get; private set; }
        public bool Configure(AttributionDefinition profile)
        {
            if(profile==null || !profile.IsValid) return false;
            definition=profile; contributions=new Contribution[profile.MaximumContributors]; count=0; Bind(); return true;
        }
        private void OnEnable() { if(definition!=null && contributions.Length==0) Configure(definition); else Bind(); }
        private void Bind()
        {
            health=GetComponent<Health>(); if(!isActiveAndEnabled) return;
            health.Resolved-=Resolved; health.Resolved+=Resolved;
            health.Changed-=HealthChanged; health.Changed+=HealthChanged;
        }
        private void OnDisable() { if(health!=null) { health.Resolved-=Resolved; health.Changed-=HealthChanged; } }
        private void OnDestroy()=>OnDisable();
        private void HealthChanged(HealthChange change)
        { if(change.Reason==HealthChangeReason.Reset || change.Reason==HealthChangeReason.Restore) { Array.Clear(contributions,0,count); count=0; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var actor in FindObjectsByType<CombatAttribution>(FindObjectsSortMode.None)) if(actor.isActiveAndEnabled && actor.definition!=null) actor.Configure(actor.definition); }
        private void Resolved(CombatRecord record)
        {
            if(this==null || health==null || !isActiveAndEnabled || definition==null || !definition.IsValid || record.Target.Id!=health.Identity.Id) return;
            if(trackedLife!=record.Target.Id) { Array.Clear(contributions,0,count); count=0; trackedLife=record.Target.Id; }
            var hit=record.Result; if(!hit.HitApplied && !hit.Killed) return;
            var now=CombatClock.Now;
            for(var i=count-1;i>=0;i--) if(now-contributions[i].Time>definition.ContributionWindow) { contributions[i]=contributions[--count]; contributions[count]=default; }
            var source=hit.Damage.Origin.Actor;
            if(source.Id!=0 && source.Id!=record.Target.Id)
            {
                var index=-1;
                for(var i=0;i<count;i++) if(contributions[i].Actor.Id==source.Id) { index=i; break; }
                if(index<0)
                {
                    if(count<contributions.Length) index=count++;
                    else
                    {
                        index=0; for(var i=1;i<count;i++) if(contributions[i].Time<contributions[index].Time) index=i;
                        TruncatedContributors++;
                    }
                    contributions[index]=new Contribution { Actor=source };
                }
                var contribution=contributions[index]; contribution.Time=now;
                contribution.Damage=(float)Math.Min(float.MaxValue,(double)contribution.Damage+hit.AppliedAmount+(definition.IncludeShieldDamage ? hit.ShieldAbsorbed : 0));
                contributions[index]=contribution;
            }
            if(!hit.Killed || awardedLife==record.Target.Id) return;
            awardedLife=record.Target.Id;
            var killer=source.Id!=record.Target.Id ? source : default;
            double total=0; var highest=-1;
            for(var i=0;i<count;i++)
            {
                total+=contributions[i].Damage;
                if(highest<0 || contributions[i].Damage>contributions[highest].Damage
                    || contributions[i].Damage==contributions[highest].Damage && contributions[i].Actor.Id<contributions[highest].Actor.Id) highest=i;
            }
            if(definition.Killer==KillCreditMode.HighestDamage && highest>=0) killer=contributions[highest].Actor;
            var assists=new CombatAssists();
            Span<bool> selected=stackalloc bool[128]; selected.Clear();
            for(var n=0;n<CombatAssists.Capacity;n++)
            {
                var best=-1;
                for(var i=0;i<count;i++)
                    if(!selected[i] && contributions[i].Actor.Id!=killer.Id && contributions[i].Damage>0 && total>0
                        && contributions[i].Damage/total>=definition.MinimumAssistFraction
                        && (best<0 || contributions[i].Damage>contributions[best].Damage)) best=i;
                if(best<0) break;
                selected[best]=true; assists.Add(new CombatContributor(contributions[best].Actor,contributions[best].Damage));
            }
            credited.Publish(new KillCredit(record.Target,killer,assists,hit.Damage.Origin));
        }
    }
}
