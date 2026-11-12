using System;
using System.Collections.Generic;
using EasyResources;
using UnityEngine;

namespace EasyCombat.Integrations
{
    /// <summary>Optional cost adapter. The supplied ResourceStore remains the only authority for balances.</summary>
    [AddComponentMenu("EasyCombat/Integrations/EasyResources Cost Provider")]
    public sealed class EasyResourcesCostProvider : CombatCostProvider
    {
        [Tooltip("Existing player wallet. If empty, uses ResourceContainer on this GameObject.")]
        public ResourceContainer Container;
        public CombatResourceMap Mapping;
        private ResourceStore boundStore;
        // Admission guards only; no balance is stored here. Providers may share one wallet.
        private static readonly Dictionary<ResourceStore,int> completingStores=new Dictionary<ResourceStore,int>();
        public ResourceFailure LastFailure { get; private set; }
        public string LastError { get; private set; }

        /// <summary>Optional pure-runtime binding. This adapter never owns or disposes the supplied store.</summary>
        public void Bind(ResourceStore store,CombatResourceMap mapping)
        {
            boundStore=store ?? throw new ArgumentNullException(nameof(store));
            Mapping=mapping;
        }

        private ResourceStore Resolve()
        {
            if(boundStore!=null) return boundStore;
            if(Container==null) Container=GetComponent<ResourceContainer>();
            if(Container==null) { LastError="Assign the player's ResourceContainer or add one to this GameObject."; return null; }
            return Container.Store;
        }

        private bool Prepare(ReadOnlySpan<ResourceCost> first,ReadOnlySpan<ResourceCost> additional,out ResourceStore store,out EasyResources.ResourceCost cost)
        {
            store=null; cost=null; LastError=null; LastFailure=default;
            if(!isActiveAndEnabled) { LastError="Enable the cost provider before using paid combat actions."; return false; }
            if(Mapping==null) { LastError="Assign a Combat Resource Map."; return false; }
            if(!Mapping.TryConvert(first,additional,out cost,out var error)) { LastError=error; return false; }
            store=Resolve();
            if(store==null) return false;
            if(completingStores.ContainsKey(store)) { LastError="Queue combat actions until the wallet notification finishes."; return false; }
            if(store.IsReadOnly) { Report(new ResourceFailure(ResourceFailureCode.ReadOnly,store:store)); return false; }
            return true;
        }

        public override bool CanSpend(ReadOnlySpan<ResourceCost> first,ReadOnlySpan<ResourceCost> additional=default)
        {
            try
            {
                if(!Prepare(first,additional,out var store,out var cost)) return false;
                var accepted=store.CanAfford(cost,out var failure);
                if(!accepted) Report(failure);
                return accepted;
            }
            catch(InvalidOperationException exception) { LastError=exception.Message; return false; }
            catch(ArgumentException exception) { LastError=exception.Message; return false; }
        }

        public override bool TrySpend(ReadOnlySpan<ResourceCost> costs,out CombatCostTransaction transaction)
        {
            transaction=null;
            ResourceNotificationScope scope=default;
            ResourceReservation hold=default;
            var handedOff=false;
            try
            {
                if(!Prepare(costs,default,out var store,out var cost)) return false;
                scope=store.DeferNotifications(new ResourceOperationContext("combat-cost"));
                if(!store.TryReserve(cost,out hold,out var failure)) { Report(failure); return false; }
                transaction=new Payment(this,store,hold,scope);
                handedOff=true;
                return true;
            }
            catch(InvalidOperationException exception) { LastError=exception.Message; return false; }
            catch(ArgumentException exception) { LastError=exception.Message; return false; }
            finally
            {
                if(!handedOff)
                {
                    try { hold.Cancel(); }
                    finally { scope.Dispose(); }
                }
            }
        }

        private void Report(ResourceFailure failure) { LastFailure=failure; LastError=failure.HasFailure ? failure.ToString() : null; }

        private sealed class Payment : CombatCostTransaction
        {
            private EasyResourcesCostProvider provider;
            private readonly ResourceStore store;
            private ResourceReservation hold;
            private ResourceNotificationScope scope;
            private bool completed;
            internal Payment(EasyResourcesCostProvider owner,ResourceStore source,ResourceReservation reservation,ResourceNotificationScope notifications)
            { provider=owner; store=source; hold=reservation; scope=notifications; }

            public override void Complete(bool committed)
            {
                if(completed) return;
                completed=true;
                completingStores.TryGetValue(store,out var depth); completingStores[store]=depth+1;
                try
                {
                    if(committed)
                    {
                        if(!hold.TryCommit(out var failure))
                        {
                            // A restored/destroyed ledger invalidates old handles. Never retry against a new balance.
                            if(provider!=null) provider.Report(failure);
                            hold.Cancel();
                        }
                    }
                    else hold.Cancel();
                }
                finally
                {
                    try { scope.Dispose(); }
                    finally
                    {
                        if(depth==0) completingStores.Remove(store); else completingStores[store]=depth;
                        provider=null; hold=default; scope=default;
                    }
                }
            }
        }
    }
}
