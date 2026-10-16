using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("EasyCombat/2D/Projectile Pool")]
    public sealed class ProjectilePool2D:MonoBehaviour
    {
        [SerializeField] private DamageProjectile2D prefab;
        [SerializeField,Min(1)] private int maximumActive=128;
        [SerializeField,Min(0)] private int prewarmCount=8;
        private readonly Stack<DamageProjectile2D> available=new Stack<DamageProjectile2D>(16);
        private readonly HashSet<DamageProjectile2D> active=new HashSet<DamageProjectile2D>();
        private readonly List<DamageProjectile2D> all=new List<DamageProjectile2D>(16);
        private Action<DamageProjectile2D> returnCallback;
        private bool shuttingDown;
        public DamageProjectile2D Prefab { get=>prefab; set { if(all.Count!=0 && value!=prefab) throw new InvalidOperationException("Use a different pool for a different 2D prefab after prewarming."); prefab=value; } }
        public int ActiveCount=>active.Count;
        public bool CanSpawn=>!shuttingDown && isActiveAndEnabled && prefab!=null && active.Count<maximumActive;
        private void Start()=>Prewarm(prewarmCount);
        private DamageProjectile2D Create()
        { var item=Instantiate(prefab,transform); item.gameObject.SetActive(false); all.Add(item); return item; }
        public void Prewarm(int count)
        { if(prefab==null || shuttingDown) return; count=Mathf.Clamp(count,0,maximumActive); while(available.Count<count) available.Push(Create()); }
        public DamageProjectile2D Spawn(Vector2 origin,Vector2 direction,DamageInfo request,Transform owner,AttackDefinition attack,Action<DamageImpact2D> impact=null)
        {
            if(!CanSpawn) return null;
            DamageProjectile2D item=null; while(available.Count>0 && item==null) item=available.Pop(); if(item==null) item=Create();
            active.Add(item); returnCallback??=Return;
            try { item.Launch(origin,direction,request,owner,attack,impact,returnCallback); }
            catch { Return(item); throw; }
            return item;
        }
        private void Return(DamageProjectile2D item)
        { if(!active.Remove(item) || item==null) return; item.gameObject.SetActive(false); if(!shuttingDown) available.Push(item); }
        public void ClearActive() { for(var i=0;i<all.Count;i++) if(all[i]!=null && all[i].IsFlying) all[i].Despawn(); }
        private void OnDisable()=>ClearActive();
        private void OnDestroy() { shuttingDown=true; ClearActive(); }
    }
}
