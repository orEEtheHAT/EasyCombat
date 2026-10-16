# Release validation

Target editor: Unity 6000.3.22f1, Windows, 2026-10-17. The final clean-project Git/WebGL pass is in progress; the release tag is not created until it completes.

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

## Scope and limits

- This is an initial reusable combat release, not a complete game or a guarantee of every possible genre/mechanic.
- Physics2D provides XY hitscan/projectile/AOE and shared actors. 3D guided motion, melee/beam/trigger/impact-field delivery are not reimplemented in 2D.
- Combat snapshots preserve actor state. Object creation, transforms, inventory, AI and platform/cloud storage belong to the game; transient delivery is cancelled on restore.
- No multiplayer authority, networking, rollback or cross-platform deterministic physics guarantee.
- No ECS/jobified renderer. Bullet-hell suitability depends on actual collision density, targets, effects, UI, hardware and frame budget. Prewarm, cap active counts, profile and choose presentation budgets.
- No Yandex SDK/ads/purchases/cloud saves are embedded. A separate platform adapter should keep this package reusable.
- ECM2 and EasyFeedbacks samples contain only integration code. Their implementations and paid content are not redistributed.
- GitHub Actions runs structural package validation. Unity suite/build/browser results reported here were run locally; the workflow does not claim cloud Unity tests.
