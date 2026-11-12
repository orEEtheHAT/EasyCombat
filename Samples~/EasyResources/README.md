# EasyResources cost adapter

**Compatibility:** EasyCombat 0.7.0+, EasyResources 0.3.0+. No prerequisite sample imports.

This optional sample lets abilities, beam pulses and paid block layers use the player's existing EasyResources wallet. EasyCombat itself does not depend on EasyResources. Without a provider, the existing `CombatResources` backend behaves as before.

## First setup

1. Install EasyCombat and EasyResources, then import **EasyResources Adapter** from EasyCombat's Samples in Package Manager.
2. Use one `ResourceContainer` for the player's wallet. For example, create an EasyResources Energy definition with **Decimal Places = 2** and initialize the container with **10000 raw units** (100 whole Energy).
3. Create **EasyCombat / Integrations / EasyResources Map**. Add a row from the combat Mana definition used in `AbilityDefinition.Costs`, `BeamDefinition.PulseCosts` or `BlockLayer.Costs` to Energy.
4. Add `EasyResourcesCostProvider` to the same actor GameObject as `CombatAbilities` / `Health`; assign the map and wallet. A beam resolves costs from its `OwnerRoot`, just as the legacy backend does.
5. Remove `CombatResources` from this actor when migrating it. The provider does not copy its initial balance, regeneration or saved state. EasyResources owns all grants, regeneration, purchases and spending for mapped resources.
6. Activate an ability costing 20 Mana: the wallet changes from 10000 to 8000 raw units after the ability finishes delivery. Cancelling a windup or rejecting missing delivery leaves 10000. A pulse costing 0.25 Mana consumes 25 raw Energy units.

Keep the sample's runtime asmdef with references to `EasyCombat.Runtime` and `EasyResources.Runtime`. Its Editor and test assemblies are separate. Do not import a second copy of the sample into the same project.

For a runtime `ResourceStore` without a component, call `provider.Bind(store, map)`. The caller still owns that store; the provider never disposes it. The basic inspector path only needs Container and Mapping.

## Amounts and failures

One combat cost unit is one whole EasyResources unit. The map converts the float's round-trip decimal text through `ResourceDefinition.ToUnits(decimal)`. It does not truncate, round away an unsupported fraction, or multiply into an unchecked integer. For a resource with two decimal places, 0.1 and 1.25 are valid; 0.001 is rejected. Nonfinite, negative, missing mappings and integer overflow are rejected before any hold is created. Duplicate cost rows and different combat definitions mapped to one wallet resource are summed atomically. Each combat definition may appear only once in the map.

An explicitly present but disabled provider rejects paid actions; it never falls back to a separate legacy balance. `LastError` explains mapping or runtime failure; `LastFailure` exposes EasyResources' typed ledger failure. The inspector explains missing setup. Existing `ReactionResourceAction` restores the legacy `CombatResources` backend only; grants to the wallet must use EasyResources public APIs.

## Transaction boundary

The provider opens a short `ResourceNotificationScope`, atomically reserves the entire cost, and returns a `CombatCostTransaction`. Combat finalizes it in `finally` after the ability event, pulse or resolved hit. Nested immediate beam and paid defense payments share wallet availability and publish only after all enclosing scopes finish. Resource observers see coherent final balances and combat state; mutation from those observers is rejected.

`Complete(false)` cancels an admission before irreversible delivery: the complete hold is released and a pure reserve/cancel does not produce a spend event. `Complete(true)` consumes the hold. Both are idempotent. Existing combat behavior is preserved: rejection/cancellation during windup costs nothing, but after `CombatAbilityAction.Execute` or a beam trace begins, `ExecutionFailed` still consumes its cost because the action may have already damaged something. This API does not roll back world effects or refund a partially executed action.

The synchronous scope must not cross a frame or `await`. Windup stores no reservation and rechecks the budget on release. For long-lived construction/crafting operations, use ordinary named EasyResources reservations and save their phase and IDs. A provider may be disabled, destroyed or rebound during delivery; each payment holds its original store/handle. Restoring or destroying that store invalidates old handles; completion never retries against a new balance.

The sample converts authoring costs and creates a payment object per admission. It is not an allocation-free beam adapter. The default combat backend is unchanged; profile a workload before adding a prepared-cost cache.

## Saving together

EasyCombat and EasyResources capture data; neither chooses files, cloud services or storage policy. See `Documentation~/IntegrationWave1.md` for a coherent common snapshot. In particular, do not save an outstanding synchronous payment and do not re-run already completed rewards after restore.

The optional Editor tests cover exact conversion, overflow, atomic holds, cancellation, nested costs, observer reentry, destruction/restore, ability rejection, partial execution failure, beam exhaustion, paid defense and a combined active-cast snapshot.
