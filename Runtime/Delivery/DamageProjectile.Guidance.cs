using System;
using UnityEngine;

namespace EasyCombat
{
    public sealed partial class DamageProjectile
    {
        private sealed class GuidanceState
        {
            public readonly AreaDamageQuery Search=new AreaDamageQuery();
            public AreaSettings SearchSettings;
            public Health Target,SourceHealth;
            public Transform Source;
            public long TargetLife,SourceLife;
            public bool Homing,AutoAcquire,Returning,HitAgainOnReturn,Cancelled,HasContact;
            public float TurnRadians,ReturnAt,CatchRadius,MaximumStep,BounceScale,ChainScale;
            public int Ricochets,Chains;
            public double Age,PendingTime;
            public DamageContact Contact;
            public void Clear()
            { Target=SourceHealth=null; Source=null; Contact=default; Search.ClearSelection(); PendingTime=Age=0; Returning=Cancelled=HasContact=false; }
        }
        private GuidanceState guidance;
        private bool usesGuidance;
        private Action<DamageContact> physicalContact;
        private void ConfigureGuidance(ProjectileMotionDefinition definition,Transform source,Health target)
        {
            usesGuidance=definition!=null;
            guidance?.Clear(); if(!usesGuidance) return;
            guidance??=new GuidanceState(); physicalContact??=OnPhysicalContact;
            var g=guidance; g.Homing=definition.Homing; g.AutoAcquire=definition.AutoAcquire; g.SearchSettings=new AreaSettings(definition.SearchArea);
            g.TurnRadians=definition.TurnDegreesPerSecond*Mathf.Deg2Rad; g.ReturnAt=definition.ReturnAfterSeconds; g.CatchRadius=definition.CatchRadius;
            g.MaximumStep=definition.MaximumStep; g.Ricochets=definition.Ricochets; g.Chains=definition.ChainJumps;
            g.BounceScale=definition.RicochetDamageMultiplier; g.ChainScale=definition.ChainDamageMultiplier; g.HitAgainOnReturn=definition.HitAgainOnReturn;
            g.Source=source; g.SourceHealth=source!=null ? source.GetComponentInParent<Health>() : null; g.SourceLife=g.SourceHealth!=null ? g.SourceHealth.Identity.Id : 0;
            SetHomingTarget(target);
        }
        public bool SetHomingTarget(Health target)
        {
            if(!usesGuidance) return false; guidance.Target=target; guidance.TargetLife=target!=null ? target.Identity.Id : 0; return true;
        }
        private void OnPhysicalContact(DamageContact contact)
        {
            var g=guidance; if(g.Cancelled) return;
            if(!contact.WasTarget && g.Ricochets>0 || contact.WasTarget && !g.Returning && g.Chains>0)
            { g.Contact=contact; g.HasContact=true; trace.Stop(); }
        }
        private void SimulateGuided(float deltaTime)
        {
            var g=guidance; g.PendingTime+=deltaTime; var iterations=0;
            while(trace.Active && !g.Cancelled && g.PendingTime>1e-8 && iterations++<8)
            {
                if(remainingRange<=0 || remainingLifetime<=0) { trace.Stop(); break; }
                if(!g.Returning && g.ReturnAt>0 && g.Age+1e-7>=g.ReturnAt)
                { g.Returning=true; g.Target=null; if(g.HitAgainOnReturn) trace.ClearVisited(); }
                var dt=(float)Math.Min(g.PendingTime,Math.Min(g.MaximumStep,remainingLifetime));
                if(!g.Returning && g.ReturnAt>0) dt=(float)Math.Min(dt,Math.Max(0,g.ReturnAt-g.Age));
                if(dt<=0) continue;
                var at=transform.position; var direction=trace.Direction; var catchDistance=float.PositiveInfinity;
                if(g.Returning)
                {
                    if(g.Source==null || g.SourceLife!=0 && (g.SourceHealth==null || g.SourceHealth.Identity.Id!=g.SourceLife || !g.SourceHealth.IsAlive)) { trace.Stop(); break; }
                    var delta=g.Source.position-at; catchDistance=Mathf.Max(0,delta.magnitude-g.CatchRadius);
                    if(catchDistance<=.0001f) { trace.Stop(); break; }
                    direction=delta.normalized;
                }
                else if(g.Homing)
                {
                    if(!TargetValid(g.Target,g.TargetLife) && g.AutoAcquire) SetHomingTarget(FindNext(at,false));
                    if(TargetValid(g.Target,g.TargetLife))
                    {
                        var delta=g.Target.transform.position-at;
                        if(delta.sqrMagnitude>.000001f) direction=Vector3.RotateTowards(direction,delta.normalized,g.TurnRadians*dt,0).normalized;
                    }
                }
                trace.SetDirection(direction); transform.rotation=Quaternion.LookRotation(direction);
                var distance=Mathf.Min(remainingRange,Mathf.Min(speed*dt,catchDistance));
                g.HasContact=false; g.Contact=default;
                trace.Advance(at,distance,onImpact,out var end,transform,physicalContact); transform.position=end;
                var travelled=Vector3.Distance(at,end); var consumed=Math.Min(dt,travelled/speed);
                if(trace.Active && travelled>=distance-.00001f) consumed=dt;
                remainingRange=Mathf.Max(0,remainingRange-travelled); remainingLifetime=Mathf.Max(0,remainingLifetime-(float)consumed);
                g.Age+=consumed; g.PendingTime=Math.Max(0,g.PendingTime-consumed);
                if(g.Cancelled || remainingRange<=0 || remainingLifetime<=0 || g.Returning && travelled>=catchDistance-.0001f) { trace.Stop(); break; }
                if(g.HasContact)
                {
                    if(trace.TargetBudgetExhausted) { trace.Stop(); break; }
                    var redirected=false; var scale=1f;
                    if(!g.Contact.WasTarget && g.Ricochets>0 && g.Contact.Normal.sqrMagnitude>.000001f)
                    { direction=Vector3.Reflect(direction,g.Contact.Normal.normalized).normalized; g.Ricochets--; scale=g.BounceScale; redirected=true; }
                    else if(g.Contact.WasTarget && !g.Returning && g.Chains>0)
                    {
                        var next=FindNext(end,true);
                        if(next!=null && (next.transform.position-end).sqrMagnitude>.000001f)
                        { direction=(next.transform.position-end).normalized; SetHomingTarget(next); g.Chains--; scale=g.ChainScale; redirected=true; }
                    }
                    if(redirected)
                    {
                        const float skin=.002f; transform.position=end+direction*skin; remainingRange=Mathf.Max(0,remainingRange-skin);
                        var nudgeTime=skin/speed; g.Age+=nudgeTime; g.PendingTime=Math.Max(0,g.PendingTime-nudgeTime); remainingLifetime=Mathf.Max(0,remainingLifetime-nudgeTime);
                        if(scale<=0) trace.Stop(); else trace.Continue(direction,scale);
                    }
                }
                if(consumed<=0 && !g.HasContact) { trace.Stop(); break; }
            }
        }
        private static bool TargetValid(Health target,long life)=>target!=null && target.isActiveAndEnabled && target.IsAlive && target.Identity.Id==life;
        private Health FindNext(Vector3 at,bool requireUnvisited)
        {
            var g=guidance; if(!g.SearchSettings.Enabled) return null;
            var count=g.Search.Select(at,trace.Direction,trace.Payload,g.SearchSettings,g.Source);
            try
            {
                Health best=null; var bestDistance=float.PositiveInfinity; var bestId=int.MaxValue;
                for(var i=0;i<count;i++)
                {
                    var candidate=g.Search.GetCandidate(i); var health=CombatTargetGroups.Rules(candidate.Receiver);
                    if(health==null || !health.IsAlive || !health.isActiveAndEnabled || candidate.Receiver is CombatBodyPart body && body.IsBroken
                        || (requireUnvisited || g.Homing) && trace.HasVisited(candidate.Receiver)) continue;
                    var id=candidate.Collider.GetInstanceID();
                    if(candidate.Distance<bestDistance || candidate.Distance==bestDistance && id<bestId) { best=health; bestDistance=candidate.Distance; bestId=id; }
                }
                return best;
            }
            finally { g.Search.ClearSelection(); }
        }
    }
}
