using System;
using System.Globalization;
using UnityEngine;

namespace EasyCombat.Integrations
{
    /// <summary>Maps combat cost definitions to the authoritative EasyResources ledger. No balances are stored here.</summary>
    [CreateAssetMenu(menuName="EasyCombat/Integrations/EasyResources Map",fileName="Combat Resource Map")]
    public sealed class CombatResourceMap : ScriptableObject
    {
        [Serializable] public struct Entry
        {
            public ResourceDefinition CombatResource;
            public EasyResources.ResourceDefinition Resource;
            public Entry(ResourceDefinition combatResource,EasyResources.ResourceDefinition resource)
            { CombatResource=combatResource; Resource=resource; }
        }

        [Tooltip("One combat unit equals one whole EasyResources unit. Decimal Places controls allowed fractions.")]
        public Entry[] Entries=Array.Empty<Entry>();

        public bool Validate(out string error)
        {
            error=null;
            if(Entries==null) { error="Resource map entries are missing."; return false; }
            for(var i=0;i<Entries.Length;i++)
            {
                var row=Entries[i];
                if(row.CombatResource==null || row.Resource==null)
                { error="Assign both resources in map entry "+i+" or remove the entry."; return false; }
                try { row.Resource.ValidateDefinition(); }
                catch(ArgumentException exception) { error=exception.Message; return false; }
                for(var j=0;j<i;j++)
                {
                    if(Entries[j].CombatResource==row.CombatResource)
                    { error="Each combat resource must appear only once in the map: "+row.CombatResource.name; return false; }
                    if(Entries[j].Resource!=row.Resource && Entries[j].Resource.Id==row.Resource.Id)
                    { error="Different EasyResources assets have the same stable ID: "+row.Resource.Id; return false; }
                }
            }
            return true;
        }

        /// <summary>Converts each authored float using its round-trip decimal representation, never silently rounding.</summary>
        public bool TryConvert(ReadOnlySpan<ResourceCost> first,ReadOnlySpan<ResourceCost> additional,
            out EasyResources.ResourceCost cost,out string error)
        {
            cost=null;
            if(!Validate(out error)) return false;
            try
            {
                var rows=new EasyResources.ResourceAmount[checked(first.Length+additional.Length)];
                for(var i=0;i<rows.Length;i++)
                {
                    var entry=i<first.Length ? first[i] : additional[i-first.Length];
                    if(!entry.IsValid) { error="Combat costs need an assigned resource and a finite nonnegative amount."; return false; }
                    EasyResources.ResourceDefinition resource=null;
                    foreach(var mapping in Entries) if(mapping.CombatResource==entry.Resource) { resource=mapping.Resource; break; }
                    if(resource==null) { error="Map the combat resource before spending: "+entry.Resource.name; return false; }
                    if(!decimal.TryParse(entry.Amount.ToString("R",CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out var quantity))
                    { error="Combat cost exceeds the supported amount: "+entry.Resource.name; return false; }
                    rows[i]=new EasyResources.ResourceAmount(resource,resource.ToUnits(quantity));
                }
                // EasyResources also sums aliases and duplicate rows with checked integer arithmetic.
                cost=new EasyResources.ResourceCost(rows);
                return true;
            }
            catch(ArgumentException exception) { error=exception.Message; return false; }
            catch(OverflowException) { error="Combat cost exceeds the EasyResources integer balance range."; return false; }
        }
    }
}
