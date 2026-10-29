# Stage 1: Combat environment stabilization

## Scope and verification limits

This stage preserves the rule-based boss and player input. The findings below come from source and Unity YAML inspection; Unity gameplay was not run. `KnightScene.unity` is the apparent gameplay scene because it contains the player, dragon, GameManager, health UI, NavMesh instance, and combat components. `ProjectSettings/EditorBuildSettings.asset` has no configured scenes, so it is not confirmed as a build entry point.

## Verified changes

- `CombatManager.Start()` used `GetComponent<GameObject>()` to overwrite the serialized `PlayerBody` and `DragonBody` references. A GameObject is not a component to retrieve this way; these calls yield no usable body reference. The scene already assigns both fields, and removing the overwrites preserves those inspector references. Neither field is currently read by the combat methods, so this is a reference-integrity correction rather than a demonstrated gameplay behavior change.
- `EnemyCombat.OnTriggerEnter()` called `ClawAttackFast() || BasicAttackFast() || HornAttackFast()`. Since the first helper returns `true`, the other two were never called. All three currently set the same fast animation speed, and the matching attack clips also have animation events that call the correct attack-specific helper. Replaced this misleading short-circuit with one `SetAttackFast()` call, preserving the previous speed-setting effect while leaving attack selection and damage behavior unchanged.

No changes were made to combat damage, HP, controls, Animator assets, scene/prefab data, or the rule-based AI.

## Static combat wiring review

- `KnightScene.unity` instantiates the Nightmare dragon prefab, overrides its name/tag to `NightmareDragon` / `Enemy`, and assigns the `Nightmare.controller` Animator controller. The controller defines `isPatrolling`, `isChasing`, `isAttacking`, `attacks`, and `dead`; its states include Idle, Patrol, Chase, AttackState, and die. The scene contains NavMesh data and a NavMeshAgent on the dragon.
- The player root is tagged `Player` and has a CharacterController. The scene references its imported knight model Animator from `PlayerMovement`, `PlayerCombat`, and both boss hit scripts. The imported model's Paladin controller defines the referenced `idle`, `walk`, `run`, `dodge`, `attack`, `attack_2`, and `getHit` parameters.
- `EnemyCombat` is attached to two dragon attack-bone objects with enabled trigger BoxColliders. `EnemyCombat` checks for an object named `ToonRTS_demo_Knight` during the dragon `AttackState`, then applies 95 player damage and raises the hit animation/fade. The dragon attack clips inspected (`Claw Attack_2`, `Basic Attack_2`, and `Horn Attack_2`) contain matching slow/fast and finish events. The die clip invokes `EnemyMovement.death()`.
- `PlayerCombat` becomes attack-ready after its trigger enters an object tagged `Enemy`. When the player attack input starts an attack and the animation guard permits it, it calls `CombatManager.DragonTakeDamage()`, which subtracts 75 boss HP and updates the health bar. The player Animator and combat references are serialized in the scene.
- Boss HP reaching zero sets `dead` and clears chase/attack flags. Player HP reaching zero disables `PlayerMovement`; `RagDollManager` observes nonpositive HP and enables the ragdoll while disabling the player Animator and CharacterController.
- These are static path checks, not runtime proof. Multiple active boss trigger colliders may cause more than one damage callback in an overlap case; verify hit count in the Editor.

## Mutable state to reset for future episodes (not implemented)

- `CombatManager`: `DragonHP`, `PlayerHP`, `BossAttackHit`, `FailedAttacks`, `waiting`, any active `HitStop` coroutine, and `Time.timeScale`; restore boss/player Animator flags and boss `dead` state.
- `EnemyMovement`: `finishedAttack`, `enraged`, `berserk`, `dragonFastSpeed`, `chaseMultiplier`, `dragonAnim.speed`, boss transform/rotation, active trails, and any relevant audio playback. Profile-derived values must be initialized consistently.
- Animator state-machine behaviors: `IdleState.timer`; `PatrolState.timer`, `isChasing`, and waypoint list/destination; NavMeshAgent destination, speed, path, and stopped state; `AttackState` attack/hit bookkeeping.
- `PlayerMovement`: transform, `YSpeed`, `desiredRotation`, Animator parameters/triggers/layer weight, CharacterController enabled state, and any dodge animation state.
- `PlayerCombat`: `canAttack`, player attack Animator state, and any attack audio/coroutine state that influences an episode.
- `RagDollManager`: Animator/CharacterController enabled flags, ragdoll collider enabled states, Rigidbody kinematic states and velocities.
- `UI_Manager`: health bar fill, blood-splatter color, and active `DoFade()` coroutine.
- Scene/environment: starting transforms, Rigidbody state, NavMesh state, waypoint choice/random seed, and any scene objects disabled by play (for example the music trigger) if they affect repeatability.

## Unresolved issues and manual Unity Editor checks

1. Open `Assets/Scenes/KnightScene.unity`, confirm there are no missing scripts or broken object references, then add it to Build Settings if it is intended to be the shipped scene.
2. In Play mode, land one player attack at the boss and verify exactly 75 HP is removed and the health bar changes.
3. Let each of the three boss attacks contact the player. Verify the correct attack animation events fire, the player receives the intended damage once per intended hit, and hit/fade feedback plays.
4. Defeat the boss and confirm the death animation triggers and no later boss attacks occur. Let the player reach zero HP and confirm movement stops and ragdoll activates once.
5. Inspect both boss trigger volumes and player hitbox alignment; verify that simultaneous bone-trigger overlaps do not create unintended duplicate damage.
6. Check the Console for null-reference, missing Animator parameter, NavMesh, or animation-event warnings throughout the encounter.

## Checks executed

- Repository YAML/source inspection was used to verify the edits and serialized references.
- `git diff --check`: passed (no whitespace errors; Git emitted only line-ending normalization warnings).
- Source search confirmed the invalid `GetComponent<GameObject>()` calls and short-circuit expression are absent from the changed combat scripts, and the single `SetAttackFast()` path is present.
- Unity Editor gameplay tests and a C# build were not run; Unity Editor and .NET compiler/build commands were not found on PATH during this inspection.
