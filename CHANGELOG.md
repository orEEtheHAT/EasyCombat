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

Further modules are tracked in ROADMAP.md; this entry is updated before release.
