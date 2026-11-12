using System;
using UnityEngine;

namespace EasyCombat
{
    /// <summary>
    /// Optional actor-wide cost backend for abilities, beam pulses and paid defenses.
    /// Attach one provider to the same GameObject that would otherwise hold CombatResources.
    /// An assigned provider takes precedence even when disabled; it never falls back to a second balance.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class CombatCostProvider : MonoBehaviour
    {
        /// <summary>Read-only validation of both cost lists as one atomic cost, including duplicates.</summary>
        public abstract bool CanSpend(ReadOnlySpan<ResourceCost> first, ReadOnlySpan<ResourceCost> additional = default);

        /// <summary>
        /// Atomically makes costs unavailable without invoking observers. On failure nothing changes.
        /// Completion must remain safe if this component is disabled or destroyed during delivery.
        /// Nested payments must share availability and publish only after the outer payment completes.
        /// </summary>
        public abstract bool TrySpend(ReadOnlySpan<ResourceCost> costs, out CombatCostTransaction transaction);
    }

    /// <summary>A synchronous deferred payment. Each handle must be independently idempotent.</summary>
    public abstract class CombatCostTransaction
    {
        /// <summary>
        /// True finalizes and publishes a completed combat outcome. False releases an admission that
        /// never began irreversible delivery. Once an action executes, ExecutionFailed still consumes
        /// its costs: an action may already have changed the world. Call exactly once in a finally block.
        /// Observers must not be able to mutate participating balances during publication.
        /// </summary>
        public abstract void Complete(bool committed);
    }

    internal static class CombatCosts
    {
        internal static bool CanSpend(GameObject actor, ReadOnlySpan<ResourceCost> first, ReadOnlySpan<ResourceCost> additional = default)
        {
            if(actor==null) return false;
            var provider=actor.GetComponent<CombatCostProvider>();
            if(provider!=null) return provider.isActiveAndEnabled && provider.CanSpend(first,additional);
            var resources=actor.GetComponent<CombatResources>();
            return resources!=null && resources.CanSpend(first,additional);
        }

        internal static bool TrySpend(GameObject actor, ReadOnlySpan<ResourceCost> costs, out ResourceSpendReceipt receipt)
        {
            receipt=default;
            if(actor==null) return false;
            var provider=actor.GetComponent<CombatCostProvider>();
            if(provider==null)
            {
                var resources=actor.GetComponent<CombatResources>();
                return resources!=null && resources.TrySpendSilently(costs,out receipt);
            }
            if(!provider.isActiveAndEnabled || !provider.TrySpend(costs,out var transaction)) return false;
            if(transaction==null) throw new InvalidOperationException("A successful CombatCostProvider payment must return a transaction.");
            receipt.Transaction=transaction;
            return true;
        }
    }
}
