# EasyCombat development roadmap

User-approved scope: one Unity package including Combat, Stats, Resources and Abilities. Public GitHub repository: orEEtheHAT/EasyCombat. License: MIT, matching Easy Feedbacks. This is a working checklist; incomplete items must not be presented as shipped features.

## Foundation and packaging
- [x] Typed damage, formulas, defenses, shields, status stacks, AOE and overlap rules.
- [x] UPM extraction, EasyCombat naming, GUID-preserving serialized type migration.
- [x] Runtime/editor independence from third-party integrations.
- [x] Existing 276 tests pass after migration; existing sandbox remains intact.
- [x] Dependency-free sample and clean-project installation test (Unity 6000.3.22f1; WebGL build passed).
- [ ] Public repository, versioned release and package validation automation.

## 1. Hit context and execution contracts
- [ ] Tags, root/parent action identity and origin classification.
- [x] Controlled RNG and explicit clock for rolls, cooldowns and journal; timers also expose manual advance APIs.
- [ ] Deferred reactions, bounded chains and observer isolation.

## 2. Stats and damage modifiers
- [x] Owned modifier handles; flat/additive/multiplicative operations and removal.
- [ ] Conditional outgoing/incoming modifiers and tag filters.
- [ ] Penetration, type conversion and explicit defense bypass policies.

## 3. Reaction construction
- [ ] Event → conditions → chance → target selection → actions through SOs.
- [ ] Cooldowns per actor/target/action, proc limits and chain guards.
- [ ] Lifesteal, reflection, counterattack, kill explosions and resource restoration examples.

## 4. Resources and healing
- [x] Generic bounded resources and transactional spend/restore.
- [x] Healing requests/results, modifiers, restrictions and optional criticals.
- [x] Explicit overheal and lifesteal bases (SO calculation; automatic lifesteal reaction follows in section 3).

## 5. Status lifecycle
- [ ] Application/tick/stack/refresh/expiration/removal events with reasons.
- [ ] Categories, selective cleansing and chance/duration/power resistance.
- [ ] Status interactions and consumption with atomic execution.

## 6. Control adapters
- [ ] Overlapping action restrictions, stun/root/silence/disarm.
- [ ] Knockback/pull intent and an ECM2 adapter without mandatory dependency.

## 7. Life states
- [ ] Downed/dead/revived, non-lethal damage and minimum-health rules.
- [ ] Kill/assist attribution and exactly-once death notification.

## 8. Advanced protection
- [ ] Temporary/decaying shields, damage caps and protection charges.
- [ ] Shield-specific multipliers, redirection and block resource costs.

## 9. Composite targets and buildup
- [ ] Linked body parts and destruction policies.
- [ ] Stagger/status buildup resources with threshold actions.

## 10. Abilities and delivery
- [ ] Ability execution, targeting, costs, cooldowns, charges and cancellation.
- [ ] Melee windows, swept contacts and repeat-hit policy.
- [ ] Sustained beams, area enter/stay/exit rules.
- [ ] Optional homing, ricochet/return/chain delivery and Physics2D adapter.

## 11. Save/restore
- [ ] Stable definition catalog and actor identity mapping.
- [ ] Versioned snapshots for resources, shields, statuses, modifiers and abilities.
- [ ] Restore without rewards, new random rolls or hit side effects.

## 12. Authoring and diagnostics
- [ ] Battle calculator and scenario simulation.
- [ ] Templates, dependency/cycle validation and runtime explanations.

## 13. Presentation
- [ ] Structured presentation events and Easy Feedbacks bridge.
- [ ] Coalescing and rate limits for feedback/numbers, separate from gameplay.

## 14. Validation and delivery
- [ ] Isolated regression tests for every new block.
- [ ] Shooter, melee, mass-combat and status-combination samples.
- [ ] WebGL build and browser smoke test; measured load scenarios.
- [ ] Package integrity, migration guide, API docs and release notes.

Network authority/rollback remains a separate future direction, as agreed in the plan; no multiplayer implementation is claimed by this roadmap.
