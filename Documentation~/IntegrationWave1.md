# Integration Wave 1

## Public extensions

`CombatCostProvider` is an optional actor component. Override `CanSpend(ReadOnlySpan<ResourceCost> first, ReadOnlySpan<ResourceCost> additional = default)` and `TrySpend(ReadOnlySpan<ResourceCost> costs, out CombatCostTransaction transaction)`. `CombatCostTransaction.Complete(bool committed)` finalizes or cancels one silent atomic payment. The XML comments specify nested, idempotent and observer-reentry behavior. Abilities, beam pulses and paid block layers resolve it on the same owner GameObject previously used for `CombatResources`. An inactive provider rejects rather than selecting a second balance. No provider leaves all existing backend behavior intact.

The optional `Samples~/EasyResources` implements this contract using EasyResources' existing authoritative `ResourceStore` and deferred notifications. Import it only when EasyResources is installed. Definitions map through `CombatResourceMap`; no balances are stored in the map or copied into CombatResources. See the sample README for exact setup, precision rules and the irreversible-delivery boundary.

Existing `HealthFeedbacks` and `PresentationFeedbacks` now have an optional `MonoBehaviour ContextProvider` implementing `EasyFeedbacks.IFeedbackContextProvider`. Set it to the player's context adapter to route feedback to that player's device. With no provider their original positions, sources and intensities are preserved. Presentation feedback remains at its captured world point and never follows a reused pooled actor. EasyCombat has no dependency on EasyInput.

`CombatFirstPersonController` works with EasyMovement's `AcquireInputBlock(owner)` API without overriding it. The gate controls manual readers, while combat restrictions still control movement/jump/turn and external impulses remain available. Scripted approach movement is allowed while manual input is blocked. Release the caller's own handle after its scripted action; do not enable another component's reader or release another owner's lock.

## One coherent save

Keep `CombatSaveSystem.TryCapture` / `TryCaptureJson` and `TryRestore` / `TryRestoreJson` as storage-independent APIs. Register actors in `CombatReferenceRegistry`, and use stable definitions in `CombatDefinitionCatalog`. A successful combat restore validates the complete combat snapshot before committing, remaps actor lifetimes and active-cast references, and cancels transient deliveries. `Health.StateRestored` and `CombatSaveSystem.Restored` are lifecycle notifications for adapters; restore does not replay damage, death or reward events.

The application's common snapshot should contain the combat snapshot, the EasyResources wallet states, and other package states from the same synchronous command boundary. Pause gameplay/input at the application level, capture all participants without yielding, and persist the resulting data together. Do not capture from health/ability/resource callbacks or during a deferred payment scope. An active ability windup is safe: it has not paid yet and will recheck/pay exactly once on resumed release. A running beam is transient and is cancelled by successful combat restore.

On restore, keep gameplay paused, restore wallet state and combat state in the same command, reconnect player adapters, then resume. EasyResources' `ResourceSnapshots.RestoreAll` can atomically restore several wallets. It does not make the separate combat restore part of that transaction: validate compatible catalogs/layout beforehand and retain a previous complete snapshot to recover if a later subsystem rejects. Do not expose a partially restored world to player actions.

For a long-lived operation owned by another package, capture `ResourceContainer.CaptureState(includeReservations: true, nowMilliseconds: now)` together with the operation phase, owner ID, frozen cost and reservation ID. Restore with `resumeReservations: true` and the explicit active-reservation replacement policy, then reconnect the existing hold through `TryGetReservation(savedId, ownerId, out hold)`. Do not call `TryReserve` or grant the reward again for an operation already represented in the restored wallet. Synchronous combat payments are deliberately absent from saves; old handles cannot mutate a restored store.

When using the EasyResources backend, omit `CombatResources` from the actor. `CombatSaveSystem` continues to save a real legacy CombatResources component if present; that is a separate legacy module, not the wallet. Save the wallet once through EasyResources. Health/resource UIs refresh from final restore/change notifications; rewards stay attached to newly committed gameplay outcomes.

## Verification and local integration paths

Core regression tests remain in the package's existing test assemblies. `CostProviderTests` adds optional-backend resolution, cancellation and paid-defense coverage without referencing EasyResources. The Resources sample includes optional Editor tests, including a combined active-cast + wallet snapshot after death with no duplicate charge/reward.

In Games Factory the single existing feedback adapter copy is at `Assets/GamesFactory/Combat/Integrations/EasyFeedbacks`. The single Movement sample remains at `Assets/Samples/EasyCombat/0.4.1/EasyMovement Adapter`. The new optional Resources sample and local feedback tests are at `Assets/PackageTests/EasyCombat`. Source samples and imported copies must stay synchronized. Durable run results are in `Audits/EasyCombat/IntegrationWave1`.
