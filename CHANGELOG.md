# Changelog

## 0.1.0-preview.1 — in development

- Extracted the existing combat foundation into a standalone UPM package.
- Renamed runtime API namespace to `EasyCombat` and assembly to `EasyCombat.Runtime`.
- Preserved script GUIDs and added `MovedFrom` metadata for existing serialized types.
- Removed mandatory ECM2, PrimeTween and Easy Feedbacks references from runtime/editor assemblies.
- Preserved the six-target, twelve-weapon development sandbox and its authored definitions.
- Migrated the existing 276-test suite without failures.
- Added a standalone Basic Arena sample and validated a clean-project WebGL build in the browser.
- Added owned flat/additive/multiplicative stat modifiers, immutable applied SO sets and optional maximum-health binding.
- Added generic resources, atomic costs, regeneration and explicit stat-driven capacity refresh.
- Added healing formulas/results, criticals, incoming/outgoing modifiers, restrictions and optional shield overheal.
- Isolated Health observers so a presentation exception cannot suppress death or later callbacks.
- Routed combat random rolls through injectable seeded RNG; added an explicit clock for cooldowns and journal records.
- Added package integrity CI and published the source to orEEtheHAT/EasyCombat.
- Added tagged action ancestry and deferred reactions with cooldowns, life-identity checks and bounded chains.
- Added damage modification/type conversion profiles and captured armor/shield/resistance bypass policies.
- Added status lifecycle reasons, category dispels, exact stack consumption and captured duration/power resistance.
- Added composable action restrictions and optional ECM2 knockback/pull integration.
- Added atomic status exchanges that preserve captured stack state on failure.
- Added optional downed/finishing/revive behavior, nonlethal/minimum-HP policies and explicit life prevention accounting.
- Added HealthSnapshot v2 with v1 compatibility, plus bounded last-hit/highest-damage kill and assist attribution.
- Pruned destroyed Unity event subscribers, including components created by editor simulations without runtime lifecycle callbacks.
- Added temporary/decaying shields, regenerating protection charges, shared damage caps and typed shield-energy costs.
- Added bounded post-defense guardian redirection, and block costs committed only after all defense stages validate.
- Added linked body parts, actor-group AOE/piercing, destruction/reset policies and life-aware contact deduplication.
- Added typed buildup, decay, cooldowns and bounded threshold actions with original source attribution.
- Routed actor reactions/attribution through local participant signals instead of one global listener per actor.
- Added SO abilities, target revalidation, atomic release costs, cast cancellation, sequential charges and cooldowns.
- Added bounded swept melee windows, sustained paid beams and area enter/stay/exit sensing with grouped life identities.
- Added shared schedulers for active ability timers and timed delivery, plus authoring inspectors and documentation.
- Added captured homing, physical ricochet, chained-target and returning 3D projectile profiles with bounded substeps.
- Added optional Physics2D delivery assembly and separate EditMode/PlayMode adapter tests; shared health/status/defense contracts remain in the core.
- Added configurable local facing for directional block and 2D body-part collider lifecycle integration.
- Added stable definition catalogs, explicit actor/source registries and versioned JSON actor-state snapshots.
- Preserved captured status formulas/strength, per-stack timers, shields, resources, modifiers, ability windups/charges and reaction/attribution state across restore.
- Added full prevalidation, coherent life/action remapping and notification-safe restore; transient deliveries end without zone exit damage.
- Guarded live status/formula sources against reuse of the original GameObject for a new combat life.
- Added isolated seeded battle scenarios, HP/shield graphs and direct-packet calculation breakdowns.
- Added eight linked recipe templates, bounded dependency/cycle diagnostics, runtime state explanations and Studio tool shortcuts.
- Added scoped 3D physics queries and global-event isolation for safe editor simulations.

Further modules are tracked in ROADMAP.md; this entry is updated before release.

- Added bounded presentation events with captured identities, source/critical separation, coalescing, rate budgets and an optional EasyFeedbacks output bridge.
- Isolated shot/area observers and preserved shot/healing metadata across callbacks; added warm allocation and lifecycle regression tests.
