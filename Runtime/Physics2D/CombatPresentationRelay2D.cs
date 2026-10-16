using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,RequireComponent(typeof(DamageEmitter2D)),AddComponentMenu("EasyCombat/2D/Presentation Relay")]
    public sealed class CombatPresentationRelay2D:MonoBehaviour
    {
        [SerializeField] private CombatPresentationHub hub;
        private DamageEmitter2D emitter;
        public void Configure(CombatPresentationHub destination) { Unbind(); hub=destination; Bind(); }
        private void OnEnable()=>Bind();
        private void Bind()
        {
            emitter=GetComponent<DamageEmitter2D>(); if(hub==null) hub=GetComponentInParent<CombatPresentationHub>();
            if(!isActiveAndEnabled) return; emitter.Fired-=Shot; emitter.Fired+=Shot; emitter.Impacted-=Impact; emitter.Impacted+=Impact;
        }
        private void Unbind() { if(emitter!=null) { emitter.Fired-=Shot; emitter.Impacted-=Impact; } }
        private void OnDisable()=>Unbind();
        private void OnDestroy()=>Unbind();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BeginSession()
        { foreach(var relay in FindObjectsByType<CombatPresentationRelay2D>(FindObjectsSortMode.None)) if(relay.isActiveAndEnabled) relay.Bind(); }
        private void Shot(ShotInfo shot)=>CombatPresentationRelay.PublishShot(hub,in shot);
        private void Impact(DamageImpact2D impact)
        {
            var collider=impact.Collider; var zone=collider!=null ? collider.GetComponentInParent<Hurtbox>() : null;
            var receiver=zone!=null ? zone.Receiver : collider!=null ? collider.GetComponentInParent<IDamageable>() as MonoBehaviour : null;
            CombatPresentationRelay.PublishImpact(hub,receiver,impact.Point,impact.Normal,impact.Result);
        }
    }
}
