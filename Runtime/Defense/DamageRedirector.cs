using UnityEngine;

namespace EasyCombat
{
    [DisallowMultipleComponent,RequireComponent(typeof(Health)),AddComponentMenu("EasyCombat/Defense/Damage Redirector")]
    public sealed class DamageRedirector : MonoBehaviour
    {
        public DamageRedirectionDefinition Definition;
        public Health Guardian;
        public bool IsValid=>Definition==null || Definition.IsValid;
        internal float Redirect(in DamageInfo original,DamageAmounts input,out DamageAmounts remaining,out DamageAmounts redirected)
        {
            remaining=input; redirected=default;
            if(!isActiveAndEnabled || Definition==null || !Definition.IsValid || Definition.Fraction<=0 || Guardian==null || !Guardian.isActiveAndEnabled
                || !Guardian.IsStanding || Guardian.gameObject==gameObject) return 0;
            if(Definition.MaximumDistance>0 && (Guardian.transform.position-transform.position).sqrMagnitude>Definition.MaximumDistance*Definition.MaximumDistance) return 0;
            var total=0f; for(var i=0;i<input.Count;i++) if(Definition.Matches(input[i].Type)) total+=input[i].Amount;
            if(total<=0) return 0;
            var amount=total*Definition.Fraction; if(Definition.MaximumDamage>0) amount=Mathf.Min(amount,Definition.MaximumDamage);
            if(amount<=0) return 0;
            var fraction=amount/total; var kept=new DamageAmounts(); var transferred=new DamageAmounts();
            for(var i=0;i<input.Count;i++)
            {
                var part=input[i]; var take=Definition.Matches(part.Type) ? part.Amount*fraction : 0;
                kept.Add(part.Type,part.Amount-take); transferred.Add(part.Type,take);
            }
            var request=new DamageInfo(transferred,original.Source,Guardian.transform.position,original.Direction,original.IsCritical,original.IsPeriodic,
                Definition.TransferStatuses ? original.OnHitStatuses : null,original.Origin.Child(CombatOriginKind.Redirection),original.StatusPolicy,original.Protection);
            if(!CombatReactionQueue.EnqueueDamage(Guardian,request)) return 0;
            remaining=kept; redirected=transferred; return transferred.Total;
        }
    }
}
