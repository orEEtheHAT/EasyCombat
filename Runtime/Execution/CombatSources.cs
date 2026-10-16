using System;
using UnityEngine;

namespace EasyCombat
{
    internal static class CombatSources
    {
        internal static long IdentityId(GameObject source)
        { if(source==null) return 0; var health=source.GetComponentInParent<Health>(); return health!=null ? health.Identity.Id : -Math.Abs((long)source.GetInstanceID())-1; }
        internal static GameObject ForLife(GameObject source,long expected)
            =>source!=null && (expected==0 || IdentityId(source)==expected) ? source : null;
    }
}
