# EasyCombat development roadmap

User-approved scope: one Unity package including Combat, Stats, Resources and Abilities. Public GitHub repository: orEEtheHAT/EasyCombat. License: MIT, matching Easy Feedbacks. This is a working checklist; incomplete items must not be presented as shipped features.

## Foundation and packaging
- [x] Typed damage, formulas, defenses, shields, status stacks, AOE and overlap rules.
- [x] UPM extraction, EasyCombat naming, GUID-preserving serialized type migration.
- [x] Runtime/editor independence from third-party integrations.
- [x] Existing 276 tests pass after migration; existing sandbox remains intact.
- [x] Dependency-free sample and clean-project installation test (Unity 6000.3.22f1; WebGL build passed).
- [x] Public repository, versioned release and package validation automation.

## 1. Hit context and execution contracts
- [x] Tags, root/parent action identity and origin classification.
- [x] Controlled RNG and explicit clock for rolls, cooldowns and journal; timers also expose manual advance APIs.
- [x] Deferred reactions, bounded chains and Health/journal observer isolation.

## 2. Stats and damage modifiers
- [x] Owned modifier handles; flat/additive/multiplicative operations and removal.
- [x] Conditional outgoing/incoming modifiers and tag filters; independent periodic stacks capture outgoing changes.
- [x] Penetration, whole-channel type conversion and explicit defense bypass policies.

## 3. Reaction construction
- [x] Event → conditions → chance → target selection → actions through SOs.
- [x] Cooldowns per actor/target/action, proc limits and chain guards.
- [x] Lifesteal, reflection, counterattack, kill explosions and resource restoration actions; examples documented and covered by tests.

## 4. Resources and healing
- [x] Generic bounded resources and transactional spend/restore.
- [x] Healing requests/results, modifiers, restrictions and optional criticals.
- [x] Explicit overheal and lifesteal bases (SO calculation; automatic lifesteal reaction follows in section 3).

## 5. Status lifecycle
- [x] Application/tick/stack/refresh/expiration/removal events with reasons.
- [x] Categories, selective cleansing and chance/duration/power resistance.
- [x] Atomic stack consumption and exchange of one status type into another; rollback preserves IDs, timers and captured strength.

## 6. Control adapters
- [x] Overlapping action restrictions, stun/root/silence/disarm.
- [x] Knockback/pull intent and an ECM2 adapter without mandatory dependency.

## 7. Life states
- [x] Downed/dead/revived, non-lethal damage and minimum-health rules; health snapshots preserve downed state.
- [x] Kill/assist attribution and exactly-once death notification.

## 8. Advanced protection
- [x] Temporary/decaying shields, damage caps and protection charges.
- [x] Shield-specific energy costs, bounded redirection and transactional block resource costs.

## 9. Composite targets and buildup
- [x] Linked body parts and destruction policies.
- [x] Stagger/status buildup resources with threshold actions.

## 10. Abilities and delivery
- [x] Ability execution, targeting, costs, cooldowns, charges and cancellation.
- [x] Melee windows, swept contacts and repeat-hit policy.
- [x] Sustained beams, area enter/stay/exit rules.
- [x] Optional 3D homing, ricochet/return/chain delivery and optional Physics2D hitscan/projectile/AOE adapter (scope in ProjectileMotion2D.ru.md).

## 11. Save/restore
- [x] Stable definition catalog and actor identity mapping.
- [x] Versioned snapshots for resources, shields, statuses, modifiers and abilities, plus buildup/control/reactions/attribution/links.
- [x] Prevalidated restore without rewards, new random rolls or hit side effects; transient delivery is cancelled (actor-state scope documented in Saving.ru.md).

## 12. Authoring and diagnostics
- [x] Battle calculator and bounded seeded scenario simulation in an isolated preview PhysicsScene.
- [x] Eight recipe templates, dependency/cycle diagnostics and read-only runtime explanations.

## 13. Presentation
- [x] Structured presentation events and Easy Feedbacks bridge.
- [x] Coalescing and rate limits for feedback/numbers, separate from gameplay.

## 14. Validation and delivery
- [x] Isolated regression tests for every new block.
- [x] Shooter, melee, mass-combat and status-combination samples.
- [x] WebGL build and browser smoke test; measured load scenarios.
- [x] Package integrity, migration guide, API docs and release notes.

Network authority/rollback remains a separate future direction, as agreed in the plan; no multiplayer implementation is claimed by this roadmap.
