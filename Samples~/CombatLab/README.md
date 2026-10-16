# Combat Lab

Import the sample, then choose **EasyCombat → Samples → Create Combat Lab** and press Play. Every run creates a new asset folder without overwriting existing work.

- Shooter: wide hitscan and random criticals.
- Melee: procedural weapon anchor, timed active window and once-per-swing contacts.
- Mass combat: 100/500/1000 visible slow piercing projectiles from a prewarmed pool.
- Status combo: three poison applications consumed by an SO reaction into a burst; periodic poison does not trigger the reaction.

The scene also demonstrates local presentation relays, coalesced floating numbers, a shield and damage journal. Edit the generated `Definitions` folders to rebalance. No paid asset, input package or render pipeline is required. Basic Arena is a separate sample for explosive projectiles and persistent fire areas.

The minimal IMGUI, strings and floating numbers are example UI and allocate memory. The FPS display includes rendering/UI and browser overhead; it is not the core simulation benchmark or a target-device guarantee. For meaningful profiling, disable development tooling and measure the actual game on target hardware.
