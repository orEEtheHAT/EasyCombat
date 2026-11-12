# EasyCombat Integration Wave 1 verification

Optional actor-wide costs now route abilities, beam pulses and paid block layers to one provider. CombatResources remains the default. Samples~/EasyResources maps definitions and uses atomic reservations plus deferred notifications; no required EasyResources dependency or mirrored balance was added.

Final shared-editor validation: EditMode 382/382; PlayMode 173/173; compiler errors: 0.
New coverage: 23 Resources adapter cases, 4 core provider cases, 2 feedback-context cases and 1 derived-controller input-gate case.

The assembly filter EasyCombat includes the existing EasyBuilding-to-EasyCombat adapter tests as well as Combat core, Physics2D, imported sample and local integration tests. Full per-test results: Audits/EasyCombat/IntegrationWave1/editmode.json and playmode.json. New fixture corrections: feedback lifecycle tests moved to PlayMode, and the runtime-created ECM2 actor received explicit maxFlySpeed rather than relying on Editor Reset defaults. No product workaround or weakened assertions. Initial reports remain as editmode-initial.json and playmode-initial.json.

Resource coverage includes decimal/fraction/overflow rejection, duplicate mappings, multi-resource atomicity, cancellation without intermediate events, nested immediate pulse and paid defense costs, observer reentry, destroyed/rebound/disposed providers/stores, authoritative restore invalidation, ability preflight/cancellation, partial execution failure, exhausted beam pulses, invalid defenses, and an active cast restored with the wallet after death without duplicate charge/reward.

Feedback adapters accept optional IFeedbackContextProvider while preserving authored world positions, sources and intensity. CombatFirstPersonController requires no implementation change: its motor restrictions and impulses remain compatible with the new manual-input gate. Its source and existing imported copy are unchanged.

Public API: CombatCostProvider.CanSpend(first, additional), TrySpend(costs, out CombatCostTransaction); CombatCostTransaction.Complete(bool); optional CombatResourceMap and EasyResourcesCostProvider.Bind(store, mapping), Container, Mapping, LastError, LastFailure; feedback ContextProvider fields. Setup and common snapshot guidance: Documentation~/IntegrationWave1.md and Samples~/EasyResources/README.md.

Limitations: adapter cost conversion/payment objects allocate; this report makes no zero-GC adapter claim. Once irreversible execution starts, failure does not refund partial world effects. Combat/wallet restore coordination belongs to the application. Shared-editor tests do not replace the coordinator's clean-project install, common scene and standalone build validation.

No package version bump, required dependency change, shared manifest edit, push or tag. The pre-existing user edit in Documentation~/CombatGuide.ru.md was preserved.
