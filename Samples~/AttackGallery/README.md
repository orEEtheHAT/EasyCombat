# Attack Gallery

**Compatibility:** EasyCombat 0.7.0+. No prerequisite sample imports.

Import **Attack Gallery** from EasyCombat's **Samples** tab. Open `AttackGallery_Sample.unity`, press Play, select an example and press **Attack**. All ten recipes are ready to edit in `Attacks/`. Choosing another example or pressing **Reset targets** clears projectiles, fields and statuses and restores the dummies.

| Recipe | What to try |
|---|---|
| `SwordSlash_Sample` | Watch the blade cross the front dummy. The active window of a single swing can hit each receiver once. |
| `PistolShot_Sample` | Compare one immediate hit on the front dummy with the piercing rail shot. |
| `RailShot_Sample` | A wide hitscan reaches the three dummies in line. The flanking dummy is outside it. |
| `PoisonDart_Sample` | Fire a few darts at different times. Up to three independent poison stacks deal damage and expire separately. |
| `Fireball_Sample` | A projectile explodes for 22 fire damage. The impact projects a four-second field onto the ground. It deals 2 damage immediately and once per second, refreshing Burn. Burn deals another 3 damage per second and can outlast the field. |
| `FrostCone_Sample` | The cone halves the affected dummies' movement speed for three seconds and leaves their HP intact. |
| `ShockNova_Sample` | Compare the damage to the front and middle dummies. The radial pulse weakens toward its edge. |
| `ChainOrb_Sample` | The orb jumps to the nearest eligible unvisited dummy, up to twice, retaining 70% damage per jump. |
| `PlasmaBeam_Sample` | One activation sustains the beam for 1.2 seconds. Its wide pulses pierce the lane every 0.2 seconds. |
| `SiegeBolt_Sample` | The slow bolt deals 6 kinetic damage plus 8% of maximum HP, with a cap of 30 on the percentage part. Against the 500 HP front dummy it deals 36. |

To use a recipe on another actor, assign an `AttackDefinition` to a `DamageEmitter`, a `MeleeDefinition` to `CombatMelee.Begin` with a weapon anchor, or a `BeamDefinition` to `CombatBeam.Begin`. Projectiles need a `ProjectilePool`; the fireball also needs a `DamageAreaPool`. Its field uses the Default layer for ground projection. Update the ground mask if your level uses another floor layer. The frost recipe exposes its movement stat in the Chilled status. The preview dummies use that same stat to scale movement.

Projectile prefabs live in `Projectiles/`. Follow the references from each recipe into `Support/` to edit damage types, formulas, area shapes and tick effects. Keep referenced assets and their Unity metadata when moving examples into a game.
