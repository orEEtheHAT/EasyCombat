using System;
using UnityEngine;

namespace EasyCombat
{
    [CreateAssetMenu(menuName = "EasyCombat/Resources/Profile", fileName = "Resources")]
    public sealed class ResourceProfile : ScriptableObject
    {
        public ResourceDefinition[] Resources = Array.Empty<ResourceDefinition>();
        public bool IsValid
        {
            get
            {
                if (Resources == null || Resources.Length > CombatResources.Capacity) return false;
                for (var i = 0; i < Resources.Length; i++)
                {
                    if (Resources[i] == null || !Resources[i].IsValid) return false;
                    for (var j = 0; j < i; j++) if (Resources[j] == Resources[i]) return false;
                }
                return true;
            }
        }
    }
}
