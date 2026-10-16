# EasyCombat

Modular, ScriptableObject-driven combat for Unity 6.3. Stats, resources, health, damage, defenses, statuses and abilities belong to one package with independent internal modules.

**Development preview.** See [ROADMAP](ROADMAP.md) for implemented and planned work. The foundation is covered by EditMode and PlayMode regression tests; new modules are validated as they are added.

## Installation

For local development, use **Package Manager → Add package from disk** and select `package.json`. In this development project the package is embedded under `Packages/com.oreethehat.easy-combat`.

For a Git installation, use **Add package from git URL** with `https://github.com/orEEtheHAT/EasyCombat.git`. Pin a release tag for production; `main` is ongoing development.

Import **Basic Arena** from the Samples tab, run **EasyCombat → Samples → Create Basic Arena**, and press Play. The generated SOs are editable under `Assets/EasyCombatSamples/`. The sample has also built successfully for WebGL in an isolated Unity 6000.3.22f1 project.

Runtime and editor authoring do not depend on ECM2, PrimeTween, EasyFeedbacks, URP, Input System, or Yandex SDK. Optional adapters are imported separately from Package Manager's Samples tab after installing their prerequisites.

To run the package tests, install Unity Test Framework and add `com.oreethehat.easy-combat` to the project manifest's `testables` array. Assemblies: `EasyCombat.EditModeTests`, `EasyCombat.PlayModeTests`.

## Current foundation

- Typed multi-part damage, shared formulas, per-term caps and critical participation.
- Health, resistances, hit rules, flat armor, reduction, directional block and rechargeable shields.
- Independent status stacks with captured strength, periodic formulas and stat/resistance modifiers.
- Hitscan, swept projectiles, penetration, AOE, persistent areas and overlap arbitration.
- Bounded damage journal and Russian-language EasyCombat Studio authoring UI.
- Optional ECM2 movement and Easy Feedbacks presentation adapters.
- Actor-owned stat modifier handles, atomic SO modifier sets, and calculation breakdowns.
- Bounded resources with atomic multi-resource costs, regeneration and stat-driven capacities.
- Healing formulas, criticals, restrictions, explicit overheal and lifesteal calculation bases.
- Injectable RNG/clock for reproducible rolls, cooldowns and journal timestamps.
- SO reactions with conditions, tags, cooldowns, deferred execution and bounded chains.
- Conditional damage modifiers, type conversion, resistance penetration and selective protection bypass.
- Status lifecycle notifications, category dispels, exact stack costs, status resistance and overlapping control restrictions.
- Atomic status combinations, optional downed/revive rules, nonlethal damage and bounded kill/assist attribution.
- Temporary/decaying shields, protection charges, damage caps, paid blocks and bounded guardian redirection.
- Linked body-part durability, grouped contacts and independent buildup meters with deferred threshold actions.
- SO abilities with targeting, atomic release costs, cast cancellation, cooldowns and sequential charges.
- Swept melee windows, sustained beams and sampled area enter/stay/exit payloads.
- Optional 3D homing, ricochet, chain and returning projectile profiles; optional Physics2D hitscan/projectile/AOE adapter.
- Versioned actor-state snapshots/JSON with stable catalogs, preserved captured status strength and restore without gameplay reward replay.

Definitions are shared configuration. Health, resources, timers, shield charges and status instances are owned by actors. Treat definitions as immutable during combat.

Open **EasyCombat → Combat Studio**. Start with a Health Profile, a Weapon definition and a Damage Emitter. Assign collision geometry and a projectile pool when needed.

## Documentation

- [Detailed guide / Полное руководство](Documentation~/CombatGuide.ru.md)
- [Development roadmap](ROADMAP.md)
- [Migration from the Games Factory prototype](Documentation~/Migration.md)
- [Stats and modifiers / Характеристики](Documentation~/Stats.ru.md)
- [Resources and healing / Ресурсы и лечение](Documentation~/ResourcesHealing.ru.md)
- [Reactions / Реакции](Documentation~/Reactions.ru.md)
- [Damage modifiers / Модификаторы урона](Documentation~/DamageModifiers.ru.md)
- [Statuses and control / Статусы и контроль](Documentation~/StatusesControl.ru.md)
- [Life and attribution / Жизнь и учёт убийств](Documentation~/LifeAttribution.ru.md)
- [Advanced defense / Расширенная защита](Documentation~/AdvancedDefense.ru.md)
- [Body parts and buildup / Части тела и накопление](Documentation~/Composite.ru.md)
- [Abilities and delivery / Способности и доставка](Documentation~/AbilitiesDelivery.ru.md)
- [Projectile motion and Physics2D / Движение снарядов и 2D](Documentation~/ProjectileMotion2D.ru.md)
- [Save and restore / Снимки состояния](Documentation~/Saving.ru.md)
- [Changelog](CHANGELOG.md)

## Package boundaries

`Runtime/` contains gameplay and opt-in presentation formatting. `Editor/` contains inspectors and Studio. `Tests/` contains regression and performance checks. `Samples~/` contains optional integrations and examples; third-party implementations are not redistributed.

The pre-existing Games Factory sandbox stays in the development project, outside this package. It is an integration fixture rather than a dependency.

## License

MIT, matching Easy Feedbacks. Copyright 2026 orEEtheHAT. See [LICENSE.md](LICENSE.md).
