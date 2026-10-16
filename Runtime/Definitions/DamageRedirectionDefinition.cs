using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName="EasyCombat/Defense/Redirection",fileName="Redirection")]
    public sealed class DamageRedirectionDefinition : ScriptableObject
    {
        [Range(0,1)] public float Fraction=.5f;
        [Min(0)] public float MaximumDamage;
        [Min(0)] public float MaximumDistance;
        public bool FilterTypes;
        public DamageTypeDefinition[] Types=Array.Empty<DamageTypeDefinition>();
        public bool TransferStatuses;
        public bool IsValid=>CombatMath.Finite(Fraction) && Fraction>=0 && Fraction<=1 && CombatMath.Finite(MaximumDamage) && MaximumDamage>=0
            && CombatMath.Finite(MaximumDistance) && MaximumDistance>=0;
        public bool Matches(DamageTypeDefinition type)=>!FilterTypes || Types!=null && Array.IndexOf(Types,type)>=0;
    }
}
