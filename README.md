# EasyCombat 0.7.0

Modular, ScriptableObject-driven combat for Unity 6.3. Stats, resources, health, damage, defenses, statuses and abilities belong to one package with independent internal modules.

**Version 0.7.0.** EasyCombat owns combat rules and delivery, including [environmental damage and area effects](Documentation~/EnvironmentalCombat.ru.md). Game-specific physics and level authoring belong in the consuming project. See the [roadmap](ROADMAP.md) and [release validation](Documentation~/Validation.md) for scope and tested limits.

## Installation

On first import, **EasyCombat → Projectile Settings** offers **Parallel** (default) and **Single Thread**. Closing the welcome window keeps Parallel in a new project. An explicit selection is stored in the project's Resources and included in player builds.

Automatic straight 3D and 2D projectile flight uses a shared Burst job for displacement, travel range and lifetime. Parallel schedules batches on available job workers. Single Thread runs the same calculation on the main thread. Unity 6.3 Web players use Single Thread even when the project requests Parallel. The runtime reports both values through `ProjectileExecutionSettings.RequestedMode` and `EffectiveMode`.

Parallel can also check swept capsules together for straight 3D volleys with automatic physics transform syncing disabled. The scheduler resolves arriving shots in launch order before sampling the remaining trajectories. Recent contacts help it choose where to start the batch. An empty result skips the exact collision search while the scene is current. A potential contact, release callback or moving projectile collider invalidates the remaining results of that batch. Exact hits, damage, statuses and callbacks resolve in launch order on the main thread. Guided 3D substeps and explicit calls to `Simulate(dt)` run synchronously.

For local development, use **Package Manager → Add package from disk** and select `package.json`. In this development project the package is embedded under `Packages/com.oreethehat.easy-combat`.

For a Git installation, use **Add package from git URL** with `https://github.com/orEEtheHAT/EasyCombat.git#v0.7.0`. Pin a release tag for production. The `main` branch tracks ongoing development.

Import **Attack Gallery** for ten editable attack recipes. Open `AttackGallery_Sample.unity`, enter Play mode, choose a recipe and click **Attack**. [Recipe list and usage](Samples~/AttackGallery/README.md)

Import **Basic Arena** from the Samples tab, run **EasyCombat → Samples → Create Basic Arena**, and press Play. The generated SOs are editable under `Assets/EasyCombatSamples/`. The sample has also built successfully for WebGL in an isolated Unity 6000.3.22f1 project.

Import **Combat Lab** for shooter, swept melee, 100/500/1000 projectile volleys and an automatic three-poison-stacks-to-burst recipe. Run **EasyCombat → Samples → Create Combat Lab**.

Runtime and editor authoring do not depend on ECM2, PrimeTween, EasyFeedbacks, URP, Input System, or Yandex SDK. Optional adapters are imported separately from Package Manager's Samples tab after installing their prerequisites.

To run the package tests, install Unity Test Framework and add `com.oreethehat.easy-combat` to the project manifest's `testables` array. Assemblies: `EasyCombat.EditModeTests`, `EasyCombat.PlayModeTests`, `EasyCombat.EditorToolsTests`; with Physics2D installed, also `EasyCombat.Physics2D.Tests` and `EasyCombat.Physics2D.PlayModeTests`.

## Current foundation

- Typed multi-part damage, shared formulas, per-term caps and critical participation.
- Health, resistances, hit rules, flat armor, reduction, directional block and rechargeable shields.
- Independent status stacks with captured strength, periodic formulas and stat/resistance modifiers.
- Hitscan, swept projectiles, penetration, AOE, persistent areas and overlap arbitration.
- Bounded damage journal and Russian-language EasyCombat Studio authoring UI.
- Optional EasyMovement FPS and Easy Feedbacks presentation adapters. Import **EasyMovement Adapter** once for combat slows, movement restrictions and knockback on the EasyMovement player.
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
- Bounded presentation events, coalescing, per-key/global limits and an optional EasyFeedbacks bridge.
- Isolated battle calculator, recipe templates, dependency/cycle diagnostics and a live state/damage explanation window.

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
- [Authoring tools / Инструменты автора](Documentation~/AuthoringTools.ru.md)
- [Presentation / Звук, VFX и числа](Documentation~/Presentation.ru.md)
- [API entry points](Documentation~/API.ru.md)
- [Validation and limits](Documentation~/Validation.md)
- [Changelog](CHANGELOG.md)

## Package boundaries

`Runtime/` contains gameplay and opt-in presentation formatting. `Editor/` contains inspectors and Studio. `Tests/` contains regression and performance checks. `Samples~/` contains optional integrations and examples; third-party implementations are not redistributed.

The pre-existing Games Factory sandbox stays in the development project, outside this package. It is an integration fixture rather than a dependency.

## License

MIT, matching Easy Feedbacks. Copyright 2026 orEEtheHAT. See [LICENSE.md](LICENSE.md).

## Sample compatibility

The samples shipped with this release require EasyCombat **0.7.0+**. Core installation remains independent of other Easy packages. Install only the dependencies for the sample you need, then import it once from Package Manager. The table gives the supported minimum versions for this adapter generation; older snapshots may lack its public APIs. Unity dependencies declared in package.json are installed automatically.

| Sample | Additional requirements | Import first |
|---|---|---|
| Basic Arena | None | — |
| Combat Lab | None | — |
| Attack Gallery | None | — |
| Easy Feedbacks Adapter | EasyFeedbacks 1.8.0+ | — |
| EasyMovement Adapter | EasyMovement 5.1.0+, ECM2 (separate installation; tested with 1.4.3) | — |
| EasyResources Adapter | EasyResources 0.3.0+ | — |

Keep one copy of each sample assembly. Existing imports may remain in their current Assets folder; review and merge updates before reimporting so local edits and asset/action GUIDs are preserved.
