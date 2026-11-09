# Migrating the Games Factory prototype

The reusable code moved from `Assets/GamesFactory/Combat/Runtime` and the reusable part of `Editor` into this package. Existing scene, prefab and SO script GUIDs are preserved. Public serialized types carry their previous namespace/assembly in `MovedFrom` metadata.

Update source imports from `GamesFactory.Combat` to `EasyCombat`. Update assembly references from `GamesFactory.Combat` to `EasyCombat.Runtime`; editor references become `EasyCombat.Editor`. Presentation formatting moved from the demo assembly to `EasyCombat.Presentation.CombatLogText`.

The development project's third-party sandbox and its setup scripts stay in Assets. They use a separate `EasyCombat.Sandbox.Editor` assembly. A clean project can install runtime/editor authoring without these files or their dependencies.

Do not keep a second active copy of the old runtime scripts alongside the package. The migrated project keeps its existing definitions and optional adapters; it does not need to import those adapter samples again.

Before migration, a local archive of the original combat directory and package manifests was saved in the development project's Temp folder. It is not part of the distributed package.

Verification: zero missing scene scripts, six targets, twelve weapons, zero invalid authored definitions, 155 EditMode and 121 PlayMode tests passed after migration.

## From 0.2.0 to 0.3.0

The combat package no longer distributes the experimental game-specific sample or vehicle simulation. An already imported sample under `Assets` is a project copy: UPM won't update it. Before upgrading, keep any local edits and port that copy or remove it from compilation. Old sample scripts using removed types will otherwise fail to compile.

For custom measured hits, use `ImpactMeasure.External` (serialized value 3 is preserved). `ImpactDurability` has no built-in containment rule; implement game-specific vetoes by overriding `IsProtected` locally. Likewise, `MovementInfluenceDefinition` describes a movement speed effect, not ground physics. Migrate any serialized game-specific fields in your own assets before upgrading; existing generic hit, fall, slow and other combat definitions keep their script GUIDs.
