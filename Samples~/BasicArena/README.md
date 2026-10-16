# Basic Arena

Import this sample from Package Manager. Run **EasyCombat → Samples → Create Basic Arena**, then Play.

The builder creates a scene and editable definitions in a new folder under `Assets/EasyCombatSamples/`. It never overwrites an existing sample. Select a weapon and a target, press FIRE, and inspect the health, shield and journal. Automatic fire and reset are available.

- Wide hitscan: pierces three targets.
- Fireball: explosion plus a temporary burning field. The explosion replaces direct damage.
- Cone: immediate area attack, suitable for a simple melee attack.
- Three targets compare normal health, armor/shield and fire resistance.

No paid assets, render pipeline package, Input System or UI package is needed. The sample uses Unity's built-in IMGUI. All gameplay values live in SOs; the sample component only provides controls and display. Runtime `BuildWebGL` validation uses the same scene generator.
