# EasyMovement Adapter

**Compatibility:** EasyCombat 0.7.0+, EasyMovement 5.1.0+, ECM2 (separate installation; tested with 1.4.3). No prerequisite sample imports.

With EasyMovement and its ECM2 installation present, import **EasyMovement Adapter**. Choose **EasyCombat → Integrations → Create EasyMovement Combat Player** to create a local player prefab that retains the EasyMovement input, camera and gait settings.

`CombatFirstPersonController` applies spatial slows and the optional `MovementInfluences.MovementSpeedStat` after EasyMovement selects a gait. It respects movement, jump and body-turn restrictions and receives knockback and pull through `ICombatMotionReceiver`. Vertical camera look remains under EasyMovement's look pivot.

For an existing generated CombatFPS prefab, keep its controller and speed key when updating the sample. If it also has the old `CharacterStatusModifiers` or `CharacterCombatMotion` component, remove those components after transferring any speed key to `MovementInfluences`. Keep just one imported copy of the EasyMovement sample scripts in Assets.
