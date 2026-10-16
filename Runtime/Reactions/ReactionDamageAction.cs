using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Reactions/Actions/Damage or Reflection", fileName = "DamageReaction")]
    public sealed class ReactionDamageAction : CombatReactionAction
    {
        [Tooltip("Optional formula/typed attack payload. Applies directly; delivery settings are not used.")]
        public AttackDefinition Attack;
        public DamageTypeDefinition Type;
        [Min(0)] public float FixedDamage;
        [Min(0)] public float FractionOfHealthDamage;
        public bool IncludeShieldAbsorption;
        [Min(0), Tooltip("Cap for the fixed/reflected amount. Attack payloads use their own formula caps.")]
        public float MaximumDamage;
        [Tooltip("Optional explosion around the selected target instead of a direct application.")]
        public AreaDefinition Area;
        public override bool IsValid => (Attack == null || Attack.IsValid) && (Area == null || Area.IsValid)
            && CombatMath.Finite(FixedDamage) && FixedDamage >= 0 && CombatMath.Finite(FractionOfHealthDamage) && FractionOfHealthDamage >= 0
            && CombatMath.Finite(MaximumDamage) && MaximumDamage >= 0 && (Attack != null || FixedDamage > 0 || FractionOfHealthDamage > 0);
        public override void Execute(in ReactionContext context)
        {
            if (context.Owner == null || context.Target == null || !IsValid) return;
            var position = context.Target.transform.position;
            var direction = (position-context.Owner.transform.position).normalized;
            DamageInfo damage;
            if (Attack != null) damage = Attack.RollRandom(context.Owner.gameObject,position,direction,origin:context.Origin(Attack));
            else
            {
                var basis = context.Trigger.Result.AppliedAmount + (IncludeShieldAbsorption ? context.Trigger.Result.ShieldAbsorbed : 0);
                double amount = FixedDamage+(double)basis*FractionOfHealthDamage;
                if (MaximumDamage > 0) amount = System.Math.Min(amount,MaximumDamage);
                if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0 || amount > float.MaxValue) return;
                damage = new DamageInfo((float)amount,context.Owner.gameObject,position,direction,Type,origin:context.Origin());
            }
            if (Area == null) context.Target.ApplyDamage(damage);
            else
            {
                var query = ReactionAreaQueries.Rent();
                try { query.Apply(position,context.Owner.transform.forward,damage,new AreaSettings(Area),context.Owner.transform); }
                finally { ReactionAreaQueries.Return(query); }
            }
        }
    }
    internal static class ReactionAreaQueries
    {
        private static readonly System.Collections.Generic.Stack<AreaDamageQuery> pool = new System.Collections.Generic.Stack<AreaDamageQuery>(4);
        internal static AreaDamageQuery Rent() => pool.Count > 0 ? pool.Pop() : new AreaDamageQuery();
        internal static void Return(AreaDamageQuery query) { query.ClearSelection(); if (pool.Count < 8) pool.Push(query); }
    }
}
