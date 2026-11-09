using UnityEngine;

namespace EasyCombat
{
    [RequireComponent(typeof(WheelCollider)),DisallowMultipleComponent,AddComponentMenu("EasyCombat/Environment/Wheel Suspension Contact")]
    public sealed class WheelContactSource : MonoBehaviour
    {
        public ImpactDurability Durability;
        public RollingResistance Mud;
        [Tooltip("Optional cylinder mesh (local Y along its axle); never a collider or the wheel transform itself.")] public Transform WheelVisual;
        private WheelCollider wheel;
        private bool initialized,loading;
        private float low,peak;
        private Vector3 peakPoint,peakNormal;
        private long life;
        private void Awake() { wheel=GetComponent<WheelCollider>(); if(!Durability) Durability=GetComponent<ImpactDurability>(); if(!Mud) Mud=GetComponent<RollingResistance>(); }
        private void OnEnable() { initialized=loading=false; }
        private void LateUpdate()
        {
            if(!WheelVisual || WheelVisual==transform) return;
            wheel.GetWorldPose(out var point,out var rotation);
            WheelVisual.SetPositionAndRotation(point,rotation*Quaternion.Euler(0,0,90));
        }
        private void FixedUpdate()
        {
            if(wheel.enabled && wheel.GetGroundHit(out var hit))
            {
                if(Mud) Mud.ApplyGroundContact(hit.point,hit.normal,Time.fixedDeltaTime);
                SampleLoad(true,hit.force,Mathf.Max(0,wheel.sprungMass*Physics.gravity.magnitude),hit.point,hit.normal);
            }
            else SampleLoad(false,0,0,transform.position,transform.up);
        }
        // A single load pulse is measured from trough to peak above static support. Report on its first plateau/descent.
        public void SampleLoad(bool grounded,float load,float staticSupport,Vector3 point,Vector3 normal)
        {
            if(!Durability || !Durability.Target || !CombatMath.Finite(load) || !CombatMath.Finite(staticSupport)) return;
            long current=Durability.Target.Identity.Id;
            if(current!=life) { life=current; initialized=loading=false; }
            float value=grounded ? Mathf.Max(0,load-staticSupport) : 0;
            if(!initialized) { low=peak=value; initialized=true; return; }
            if(value>peak) { loading=true; peak=value; peakPoint=point; peakNormal=normal; return; }
            if(loading)
            {
                float rise=peak-low;
                loading=false; low=peak=value;
                Durability.TryImpact(ImpactMeasure.SuspensionLoad,rise,peakPoint,peakNormal,transform.position,out _);
            }
            else low=peak=value;
        }
    }
}
