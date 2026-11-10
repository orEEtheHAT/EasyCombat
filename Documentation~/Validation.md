# Release validation

## 0.5.0 — 2026-11-10

With EasyMovement and ECM2 installed, both EasyMovement adapter PlayMode tests passed. The tests cover speed scaling across gaits, movement and jump restrictions, body yaw, and knockback while movement is restricted. The prefab generator produced a combat player with its camera and look pivot references intact.

## 0.4.1 — 2026-11-10

Unity 6000.3.22f1 passed 352 EditMode and 164 PlayMode tests in an isolated project. The suite covers projectile callbacks that move a target during a mixed volley, nested blasts, and rejected status applications that consume random rolls.

Windows x64 release players matched gameplay counters and health loss across eight fixed-step workloads. A separate real-time CPU capture exercised a mixed wave of 100,000 shots, firing 256 shots per frame.

## 0.4.0 — 2026-11-10

Unity 6000.3.22f1 passed 349 EditMode and 160 PlayMode tests in an isolated project. Windows x64 players ran both projectile execution modes. A Unity 6.3 Web player configured for Parallel used Single Thread and applied 8 damage at the blast center and 4 at half radius.

The Windows x64 headless benchmark compared the pre-Jobs implementation with the current Single Thread and Parallel modes on an Intel Core i7-14700KF. The values below are median window means in milliseconds per combat update across six warmed windows per scenario and mode. All three modes agreed on hit counts, periodic ticks and per-target health loss.

| Workload | Pre-Jobs | Single Thread | Parallel |
|---|---:|---:|---:|
| 1,000 projectiles in empty space | 0.98 | 0.93 | 0.67 |
| 5,000 projectiles in empty space | 4.84 | 4.53 | 2.73 |
| 1,000 projectiles with direct hits | 2.06 | 2.06 | 1.81 |
| 1,000 projectiles with splash and burn | 5.85 | 5.56 | 5.30 |

## 0.3.1 — 2026-11-10

Unity 6000.3.22f1 / Windows, Intel Core i7-14700KF. All 349 EditMode + 148 PlayMode tests in the factory project passed (497 total, including optional adapters and local integration tests). In an isolated project, all 9 independent audit checks passed, including the five regressions and a 2000-case seeded shield/resistance comparison. The additional sender-observer fanout case and all five fixes also passed in the desktop release WebGL player.

A warmed, independent Unity GC.Alloc recorder returned 0 events for an empty action and 16 for 16 known allocations, both in Editor and a separate Development WebGL build. Per 1000 operations:

| Workload | Editor events | WebGL Dev events |
|---|---:|---:|
| Direct hits, with or without the journal | 0 | 0 |
| Shot calculations, named SO and owner with Health | 0 | 0 |
| Shot calculations, owner without Health | 0 | 0 |
| Shot calculations, fallback SO name | 0 | 0 |
| Full warmed hitscan without owner Health or explicit weapon DisplayName | 0 | 0 |
| Isolated Object.name read (control) | 1000 | 1000 |

These are counts of allocations in these configurations, not bytes or guarantees about an entire game. First use of a name, pool growth, UI, VFX and user code are outside these warm measurements. In the release WebGL CPU run, 1000 sparse projectiles averaged 1.36 ms (p95 1.50), and 1024 direct two-channel hits averaged 3.00 ms (p95 3.20). No rendering, AI or VFX in timed loops; no mobile-device FPS claim.

## 0.1.0 — historical report

The timing measurements below are historical. Its allocated-byte counter was not positively calibrated on these Unity backends; historical zero-byte values are not evidence of zero GC. See the calibrated 0.3.1 measurements above.


Target editor: Unity 6000.3.22f1, Windows, 2026-10-17. Initial release validation completed. The clean build used public Git commit `a1ba5c3dcd0388300f144654ccb57b6b5117443c`; subsequent release changes only finalize this report and roadmap.

## Regression suites

| Assembly | Mode | Passed |
|---|---|---:|
| EasyCombat.EditModeTests | Edit | 330 |
| EasyCombat.PlayModeTests | Play | 128 |
| EasyCombat.Physics2D.Tests | Edit | 11 |
| EasyCombat.Physics2D.PlayModeTests | Play | 2 |
| EasyCombat.EditorToolsTests | Edit | 5 |

**476 passed, zero failures/skips.**

The tests exercise real HP/formula/defense/status transactions, pooled life identities, shared physics delivery, bounded reactions, save/restore JSON, isolated editor simulations and presentation budgets. Warm allocation checks cover selected core paths; third-party effects and sample UI are excluded.

Combat Lab was imported into the development project and exercised through Unity CLI in Play mode: hitscan damage, three poison stacks exchanged for one burst (1000 → 960 HP including three 5-damage hits), one 20-damage melee contact, 1000 active pooled projectiles and complete return to the pool after flight. The package audit checks identity/license, asmdef isolation, runtime dependencies, metadata GUIDs and parallel Studio category arrays.

## Clean Git installation and WebGL

A separate project resolved the public Git URL pinned to the commit above (verified in `packages-lock.json`), with no ECM2/EasyFeedbacks/PrimeTween/Yandex package. Combat Lab built as a WebGL Development Build with IL2CPP: Unity reported **Success**, total build size **36,346,886 bytes** with compression disabled. Both sample source assemblies compiled in that project.

Browser smoke at localhost verified: scene startup, hitscan HP loss, melee contacts and shield absorption, three poison shots consumed into a 25-damage burst, and a 1000-projectile volley with damage and coalescing. Browser console reported no errors/warnings during the smoke. The embedded browser was in the background and throttled rendering; its FPS readout is intentionally not reported as performance evidence. No physical Android/iOS device, Yandex moderation or production hosting was tested.

GitHub Actions package-integrity run [38970398901](https://github.com/orEEtheHAT/EasyCombat/actions/runs/38970398901) passed for the tested code commit. Development project restored its original CombatSandbox scene and background setting, with zero missing scripts and 41 Studio categories.

## Measured simulation load

Intel Core i7-14700KF, Unity Editor, 120 measured manual steps at 1/60 s after 30 warmup steps, sparse static collision geometry. Each projectile performs an initial overlap and sweep query. No rendering, UI, input or VFX. This is **not FPS or WebGL frame time**; other editor/system load affects the numbers.

| Active projectiles | Mean step, ms | p95, ms | Managed bytes over measured loop |
|---:|---:|---:|---:|
| 100 | 0.164 | 0.328 | 0 |
| 500 | 0.707 | 1.087 | 0 |
| 1000 | 1.713 | 2.314 | 0 |

[Raw result](Benchmarks/editor-projectiles-0.1.0.json). Reproduce with Combat Studio's load diagnostic / `CombatSimulationBenchmark.Run()` outside Play mode. Dense impacts, guidance and actual game effects require their own profiling.

## Scope and limits

- This is an initial reusable combat release, not a complete game or a guarantee of every possible genre/mechanic.
- Physics2D provides XY hitscan/projectile/AOE and shared actors. 3D guided motion, melee/beam/trigger/impact-field delivery are not reimplemented in 2D.
- Combat snapshots preserve actor state. Object creation, transforms, inventory, AI and platform/cloud storage belong to the game; transient delivery is cancelled on restore.
- No multiplayer authority, networking, rollback or cross-platform deterministic physics guarantee.
- No ECS/jobified renderer. Bullet-hell suitability depends on actual collision density, targets, effects, UI, hardware and frame budget. Prewarm, cap active counts, profile and choose presentation budgets.
- No Yandex SDK/ads/purchases/cloud saves are embedded. A separate platform adapter should keep this package reusable.
- ECM2 and EasyFeedbacks samples contain only integration code. Their implementations and paid content are not redistributed.
- GitHub Actions runs structural package validation. Unity suite/build/browser results reported here were run locally; the workflow does not claim cloud Unity tests.
