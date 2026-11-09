using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent, AddComponentMenu("EasyCombat/Environment/Impact Durability")]
    public class ImpactDurability : MonoBehaviour
    {
        public Health Target;
        public ImpactDamageDefinition Profile;
        private long life;
        private double next;
        private void Awake() { if(!Target) Target=GetComponent<Health>(); }
        private void OnEnable() { next=double.NegativeInfinity; }
        /// <summary>Optional game-side veto before applying impact damage. The default accepts any position.</summary>
        public virtual bool IsProtected(Vector3 center) => false;
        // Sources can also feed a measured impact directly, without a Rigidbody callback.
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
