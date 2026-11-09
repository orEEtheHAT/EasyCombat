using UnityEngine;

namespace EasyCombat
{
    [RequireComponent(typeof(Rigidbody)), AddComponentMenu("EasyCombat/Environment/Rigidbody Impact Source")]
    public sealed class RigidbodyImpactSource : MonoBehaviour
    {
        public ImpactDurability Receiver;
        [Tooltip("Optional specific collider on this body. Contacts from other colliders are excluded.")]
        public Collider OwnCollider;
        public LayerMask SurfaceLayers=~0;
        [Range(0,1)] public float MinimumLandingNormal=.5f;
        private Rigidbody body;
        private float apex;
        private long life;
        private Vector3 Up => Physics.gravity.sqrMagnitude>1e-6f ? -Physics.gravity.normalized : Vector3.up;
        private void Awake() { body=GetComponent<Rigidbody>(); if(!Receiver) Receiver=GetComponent<ImpactDurability>(); }
        private void OnEnable() { ResetFlight(); }
        private void ResetFlight()
        {
            if(!body) body=GetComponent<Rigidbody>();
            apex=Vector3.Dot(body.worldCenterOfMass,Up); life=Receiver && Receiver.Target ? Receiver.Target.Identity.Id : 0;
        }
        private void SampleHeight() { if(Receiver.Target && life!=Receiver.Target.Identity.Id) ResetFlight(); else apex=Mathf.Max(apex,Vector3.Dot(body.worldCenterOfMass,Up)); }
        private void FixedUpdate()
        { if(Receiver && Receiver.Profile && Receiver.Profile.Measure==ImpactMeasure.DropHeight) SampleHeight(); }
        private void OnCollisionEnter(Collision hit) => Contact(hit,true);
        private void OnCollisionStay(Collision hit)
        { if(Receiver && Receiver.Profile && Receiver.Profile.Measure==ImpactMeasure.DropHeight) Contact(hit,false); }
        private void Contact(Collision hit,bool enter)
        {
            if(!isActiveAndEnabled || !Receiver || !Receiver.Profile || hit.contactCount==0) return;
            var mode=Receiver.Profile.Measure;
            if(mode==ImpactMeasure.External) return;
            var up=Up; Vector3 center=body.worldCenterOfMass;
            if(mode==ImpactMeasure.DropHeight) SampleHeight();
            float best=-1; ContactPoint contact=default; bool mixed=false;
            for(int i=0;i<hit.contactCount;i++)
            {
                var c=hit.GetContact(i);
                if(OwnCollider && c.thisCollider!=OwnCollider) { mixed=true; continue; }
                if(!c.otherCollider || (SurfaceLayers.value & (1<<c.otherCollider.gameObject.layer))==0) continue;
                float value;
                if(mode==ImpactMeasure.DropHeight)
                { if(Vector3.Dot(up,c.normal)<MinimumLandingNormal) continue; value=Mathf.Max(0,apex-Vector3.Dot(center,up)); }
                else if(mode==ImpactMeasure.NormalSpeed) value=Mathf.Abs(Vector3.Dot(hit.relativeVelocity,c.normal));
                else value=Mathf.Abs(Vector3.Dot(hit.impulse,c.normal));
                if(value>best) { best=value; contact=c; }
            }
            if(best<0) return;
            if(mode==ImpactMeasure.DropHeight) apex=Vector3.Dot(center,up);
            // The total pair impulse cannot be attributed to a single collider in a mixed contact.
            if(enter && !(mode==ImpactMeasure.NormalImpulse && mixed))
                Receiver.TryImpact(mode,best,contact.point,contact.normal,center,out _);
        }
    }
}
