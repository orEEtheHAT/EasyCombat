using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    public readonly struct DamageImpact2D
    {
        public readonly Collider2D Collider;
        public readonly Vector2 Point,Normal;
        public readonly DamageResult Result;
        public DamageImpact2D(Collider2D collider,Vector2 point,Vector2 normal,DamageResult result)
        { Collider=collider; Point=point; Normal=normal; Result=result; }
    }
    internal readonly struct DamageContact2D
    {
        public readonly Collider2D Collider;
        public readonly MonoBehaviour Receiver;
        public readonly Hurtbox Zone;
        public readonly CombatHitKey Key;
        public readonly long Life;
        public readonly Vector2 Point,Normal;
        public readonly float Distance;
        public readonly int ColliderId;
        public bool WasTarget=>!ReferenceEquals(Receiver,null);
        public bool IsCurrent=>Collider!=null && Collider.enabled && Collider.gameObject.activeInHierarchy && (!WasTarget || Receiver!=null
            && Receiver.isActiveAndEnabled && (Zone==null || Zone.Receiver==Receiver) && Key.Equals(new CombatHitKey(Receiver))
            && (Life==0 || CombatTargetGroups.Life(Receiver)==Life));
        public DamageContact2D(Collider2D collider,float distance,Vector2 point,Vector2 normal)
        {
            Collider=collider; Distance=distance; Point=point; Normal=normal; ColliderId=collider.GetInstanceID();
            Zone=collider.GetComponentInParent<Hurtbox>(); Receiver=Zone!=null ? Zone.Receiver : collider.GetComponentInParent<IDamageable>() as MonoBehaviour;
            if(Receiver is Hurtbox || !(Receiver is IDamageable)) Receiver=null;
            Key=Receiver!=null ? new CombatHitKey(Receiver) : default; Life=Receiver!=null ? CombatTargetGroups.Life(Receiver) : 0;
        }
        public DamageImpact2D Apply(DamageInfo damage,bool useZone=true)
        {
            var result=useZone && Zone!=null ? Zone.ApplyDamage(damage) : Receiver!=null ? ((IDamageable)Receiver).ApplyDamage(damage) : DamageResult.Rejected(damage,DamageRejection.NoReceiver);
            return new DamageImpact2D(Collider,Point,Normal,result);
        }
        internal sealed class Comparer:IComparer<DamageContact2D>
        {
            public int Compare(DamageContact2D a,DamageContact2D b)
            { var order=a.Distance.CompareTo(b.Distance); if(order!=0) return order; order=a.WasTarget.CompareTo(b.WasTarget); return order!=0 ? order : a.ColliderId.CompareTo(b.ColliderId); }
        }
    }
    public sealed class DamageQuery2D
    {
        private RaycastHit2D[] hits=new RaycastHit2D[16];
        private Collider2D[] overlaps=new Collider2D[16];
        private readonly List<DamageContact2D> contacts=new List<DamageContact2D>(16);
        private static readonly DamageContact2D.Comparer comparer=new DamageContact2D.Comparer();
        internal DamageContact2D Get(int index)=>contacts[index];
        internal void Clear()=>contacts.Clear();
        internal static ContactFilter2D Filter(LayerMask mask,QueryTriggerInteraction triggers)
        {
            var filter=new ContactFilter2D(); filter.SetLayerMask(mask);
            filter.useTriggers=triggers==QueryTriggerInteraction.Collide || triggers==QueryTriggerInteraction.UseGlobal && Physics2D.queriesHitTriggers; return filter;
        }
        internal static bool IsOwner(Collider2D collider,Transform owner)=>owner!=null && (collider.transform==owner || collider.transform.IsChildOf(owner));
        internal int Collect(Vector2 origin,Vector2 direction,float distance,float radius,LayerMask mask,QueryTriggerInteraction triggers,Transform owner)
        {
            contacts.Clear(); var filter=Filter(mask,triggers); var overlapsCount=0;
            try
            {
                while(true) { overlapsCount=Physics2D.OverlapCircle(origin,Mathf.Max(.001f,radius),filter,overlaps); if(overlapsCount<overlaps.Length) break; Array.Resize(ref overlaps,overlaps.Length*2); }
                for(var i=0;i<overlapsCount;i++) if(overlaps[i]!=null && !IsOwner(overlaps[i],owner))
                    contacts.Add(new DamageContact2D(overlaps[i],0,overlaps[i].ClosestPoint(origin),-direction));
                if(distance>0)
                {
                    int count;
                    while(true)
                    {
                        count=radius>0 ? Physics2D.CircleCast(origin,radius,direction,filter,hits,distance) : Physics2D.Raycast(origin,direction,filter,hits,distance);
                        if(count<hits.Length) break; Array.Resize(ref hits,hits.Length*2);
                    }
                    for(var i=0;i<count;i++)
                    {
                        var hit=hits[i]; if(hit.collider==null || IsOwner(hit.collider,owner)) continue;
                        var duplicate=false; for(var j=0;j<overlapsCount;j++) if(overlaps[j]==hit.collider) { duplicate=true; break; }
                        if(!duplicate) contacts.Add(new DamageContact2D(hit.collider,hit.distance,hit.point,hit.normal));
                    }
                }
                contacts.Sort(comparer); return contacts.Count;
            }
            finally { Array.Clear(overlaps,0,overlapsCount); }
        }
        internal bool Visible(Vector2 from,Vector2 to,LayerMask mask,Transform owner,MonoBehaviour receiver)
        {
            var delta=to-from; if(delta.sqrMagnitude<=.000001f) return true;
            var count=Collect(from,delta.normalized,delta.magnitude,0,mask,QueryTriggerInteraction.Ignore,owner);
            try
            {
                for(var i=0;i<count;i++)
                {
                    var hit=contacts[i]; if(!hit.IsCurrent) continue;
                    return hit.Receiver!=null && CombatTargetGroups.Key(hit.Receiver)==CombatTargetGroups.Key(receiver);
                }
                return true;
            }
            finally { Clear(); }
        }
        public static bool Supports(AttackDefinition attack)=>attack!=null && attack.IsValid && attack.ImpactArea==null && attack.ProjectileMotion==null;
    }
    public sealed class AreaDamageQuery2D
    {
        private Collider2D[] overlaps=new Collider2D[32];
        private readonly List<DamageContact2D> contacts=new List<DamageContact2D>(32);
        private readonly Dictionary<CombatHitKey,int> indices=new Dictionary<CombatHitKey,int>(32);
        private readonly DamageQuery2D visibility=new DamageQuery2D();
        private bool busy;
        public int Apply(Vector2 center,Vector2 forward,DamageInfo damage,AreaSettings settings,Transform owner=null,Action<DamageImpact2D> onImpact=null)
        {
            if(busy) throw new InvalidOperationException("Do not recursively reuse the same 2D area query.");
            if(!settings.Enabled || !damage.IsValid || !CombatMath.Finite(center) || !CombatMath.Finite(forward) || settings.Shape==AreaShape.Cone && forward.sqrMagnitude<.000001f) return 0;
            busy=true; var count=0; CombatReactionQueue.Enter();
            try
            {
                forward.Normalize(); var filter=DamageQuery2D.Filter(settings.TargetMask,settings.Triggers);
                while(true) { count=Physics2D.OverlapCircle(center,settings.Radius,filter,overlaps); if(count<overlaps.Length) break; Array.Resize(ref overlaps,overlaps.Length*2); }
                for(var i=0;i<count;i++)
                {
                    var collider=overlaps[i]; if(collider==null) continue;
                    var point=collider.ClosestPoint(center); var delta=point-center; var distance=delta.magnitude;
                    var contact=new DamageContact2D(collider,distance,point,-delta.normalized);
                    if(!contact.WasTarget || !contact.IsCurrent || contact.Zone!=null && !contact.Zone.isActiveAndEnabled || distance>settings.Radius) continue;
                    var health=CombatTargetGroups.Rules(contact.Receiver);
                    if(!settings.AffectOwner && (DamageQuery2D.IsOwner(collider,owner) || health!=null && health.Identity.Id==damage.Origin.Actor.Id)) continue;
                    if(settings.Targets!=AreaTargetFilter.All)
                    {
                        var team=contact.Receiver.GetComponentInParent<CombatTeam>(); var ally=damage.Origin.HasTeam && team!=null && team.Team==damage.Origin.Team;
                        if(settings.Targets==AreaTargetFilter.Allies ? !ally : ally) continue;
                    }
                    if(settings.Shape==AreaShape.Cone && distance>.0001f && Vector2.Dot(forward,delta/distance)<settings.ConeCosine) continue;
                    if(settings.RequireLineOfSight && !visibility.Visible(center,point,settings.ObstacleMask,owner,contact.Receiver)) continue;
                    if(indices.TryGetValue(contact.Key,out var existing))
                    { var old=contacts[existing]; if(distance<old.Distance || distance==old.Distance && contact.ColliderId<old.ColliderId) contacts[existing]=contact; }
                    else { indices.Add(contact.Key,contacts.Count); contacts.Add(contact); }
                }
                var affected=0;
                foreach(var contact in contacts)
                {
                    if(!contact.IsCurrent) continue;
                    var multiplier=settings.DistanceFalloff ? Mathf.Lerp(1,settings.EdgeMultiplier,contact.Distance/settings.Radius) : 1; if(multiplier<=0) continue;
                    var direction=contact.Point-center; if(direction.sqrMagnitude<.000001f) direction=forward; else direction.Normalize();
                    var impact=contact.Apply(damage.Scale(multiplier).At(contact.Point,direction),settings.UseHurtboxMultiplier);
                    onImpact?.Invoke(impact); if(impact.Result.HitApplied || impact.Result.StatusesApplied>0) affected++;
                }
                return affected;
            }
            finally { Array.Clear(overlaps,0,count); contacts.Clear(); indices.Clear(); busy=false; CombatReactionQueue.Exit(); }
        }
    }
    internal sealed class AttackTrace2D
    {
        private readonly DamageQuery2D query=new DamageQuery2D();
        private readonly AreaDamageQuery2D explosion=new AreaDamageQuery2D();
        private readonly HashSet<CombatHitKey> visited=new HashSet<CombatHitKey>();
        private Transform owner;
        private DamageInfo damage;
        private AreaSettings area;
        private LayerMask mask;
        private QueryTriggerInteraction triggers;
        private bool piercing;
        private int maximumTargets,contacts;
        private Vector2 direction;
        public bool Active { get; private set; }
        public float Radius { get; private set; }
        public void Begin(AttackDefinition attack,DamageInfo request,Transform source,Vector2 forward)
        {
            Clear(); owner=source; damage=request; area=new AreaSettings(attack.Area); mask=attack.HitMask; triggers=attack.Triggers;
            piercing=attack.ContactBehavior==ContactBehavior.PierceTargets; maximumTargets=attack.MaximumTargets; direction=forward.normalized; Radius=attack.HitRadius; Active=true;
        }
        public void Advance(Vector2 origin,float distance,Action<DamageImpact2D> callback,out Vector2 end)
        {
            end=origin+direction*distance; if(!Active) return;
            var count=query.Collect(origin,direction,distance,Radius,mask,triggers,owner);
            try
            {
                for(var i=0;i<count && Active;i++)
                {
                    var contact=query.Get(i); if(!contact.IsCurrent || contact.WasTarget && !visited.Add(contact.Key)) continue;
                    contacts++; var stop=!piercing || !contact.WasTarget || maximumTargets>0 && visited.Count>=maximumTargets;
                    if(stop) { Active=false; end=origin+direction*contact.Distance; }
                    if(area.Enabled) explosion.Apply(contact.Point+contact.Normal*.001f,direction,damage,area,owner,callback);
                    else { var impact=contact.Apply(damage.At(contact.Point,direction)); callback?.Invoke(impact); }
                    if(!Active) end=origin+direction*contact.Distance;
                }
            }
            catch { Active=false; throw; }
            finally { query.Clear(); }
        }
        public void Complete(Vector2 end,Action<DamageImpact2D> callback)
        { Active=false; if(area.Enabled && contacts==0) explosion.Apply(end,direction,damage,area,owner,callback); }
        public void Stop()=>Active=false;
        public void Clear() { Active=false; visited.Clear(); query.Clear(); owner=null; damage=default; area=default; contacts=0; }
    }
}
