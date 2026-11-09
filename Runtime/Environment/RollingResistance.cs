using UnityEngine;

namespace EasyCombat
{
    [AddComponentMenu("EasyCombat/Environment/Rolling Resistance Contact")]
    public sealed class RollingResistance : MonoBehaviour
    {
        public Rigidbody Body;
        [Tooltip("Fraction of the body's mass carried by this wheel; e.g. 0.25 for four equal wheels.")]
        [Range(0,1)] public float LoadShare=.25f;
        [Tooltip("Only include layers containing Mud/Slow volume triggers.")] public LayerMask VolumeMask=~0;
        [Tooltip("Optional probe when no WheelCollider or custom ground-contact feed is used.")] public bool UseRayProbe;
        [Min(.01f)] public float ProbeLength=.5f;
        [Tooltip("Ground only; exclude the cart/wheels themselves.")] public LayerMask GroundMask=1;
        private readonly Collider[] volumes=new Collider[32];
        public int SaturatedProbes { get; private set; }
        private void Awake() { if(!Body) Body=GetComponentInParent<Rigidbody>(); }
        private void FixedUpdate()
        {
            if(UseRayProbe && Physics.Raycast(transform.position,-transform.up,out var hit,ProbeLength,GroundMask,QueryTriggerInteraction.Ignore)
                && hit.rigidbody!=Body) ApplyGroundContact(hit.point,hit.normal,Time.fixedDeltaTime);
        }
        // Call exactly once per supported wheel per physics step. An airborne wheel never calls this.
        public bool ApplyGroundContact(Vector3 point,Vector3 normal,float dt)
        {
            if(!isActiveAndEnabled || !Body || Body.isKinematic || !CombatMath.Positive(dt) || !CombatMath.Finite(point) || !CombatMath.Finite(normal) || normal.sqrMagnitude<.000001f) return false;
            var count=Physics.OverlapSphereNonAlloc(point,.005f,volumes,VolumeMask,QueryTriggerInteraction.Collide);
            if(count==volumes.Length) SaturatedProbes++;
            float decel=0;
            for(int i=0;i<count;i++)
            {
                var c=volumes[i]; volumes[i]=null;
                if(!c || !c.isTrigger || !c.TryGetComponent<MovementSurface>(out var surface) || !surface.isActiveAndEnabled
                    || !surface.Effect || !surface.Effect.IsValid) continue;
                decel=Mathf.Max(decel,surface.Effect.RollingDeceleration);
            }
            if(decel<=0) return false;
            var v=Vector3.ProjectOnPlane(Body.GetPointVelocity(point),normal); float speed=v.magnitude;
            if(speed<=.0001f) return false;
            Body.AddForceAtPosition(-v/speed*(Body.mass*Mathf.Clamp01(LoadShare)*Mathf.Min(decel,speed/dt)),point,ForceMode.Force);
            return true;
        }
    }
}
