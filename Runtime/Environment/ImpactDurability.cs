using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Environment/Impact Durability")]
    public sealed class ImpactDurability : MonoBehaviour
    {
        public Health Target;
        public ImpactDamageDefinition Profile;
        [Tooltip("Optional cargo compartment. While the cargo's center is inside this box, incoming impact samples are ignored.")]
        public BoxCollider SafeHold;
        private long life;
        private double next;
        private void Awake() { if(!Target) Target=GetComponent<Health>(); }
        private void OnEnable() { next=double.NegativeInfinity; }
        public bool IsProtected(Vector3 center)
        {
            if(!SafeHold || !SafeHold.enabled || !SafeHold.gameObject.activeInHierarchy) return false;
            var p=SafeHold.transform.InverseTransformPoint(center)-SafeHold.center; var half=SafeHold.size*.5f;
            return Mathf.Abs(p.x)<=half.x && Mathf.Abs(p.y)<=half.y && Mathf.Abs(p.z)<=half.z;
        }
        // Also the entry point for custom raycast suspensions or a separate controller's landing event.
        public bool TryImpact(ImpactMeasure measure,float magnitude,Vector3 point,Vector3 normal,Vector3 center,out DamageResult result)
        {
            result=default;
            if(!isActiveAndEnabled || !Target || !Target.isActiveAndEnabled || !Target.IsAlive || !Profile || Profile.Measure!=measure
                || !CombatMath.Finite(center) || !CombatMath.Finite(point) || !CombatMath.Finite(normal) || IsProtected(center)) return false;
            long id=Target.Identity.Id;
            if(life!=id) { life=id; next=double.NegativeInfinity; }
            double now=CombatClock.Now; if(now<next) return false;
            float amount=Profile.Evaluate(magnitude); if(amount<=0) return false;
            next=now+Profile.RepeatDelay;
            result=Target.ApplyDamage(new DamageInfo(amount,point:point,direction:normal,type:Profile.DamageType));
            return true; // A request was submitted; defenses can reduce or reject it.
        }
    }
}
