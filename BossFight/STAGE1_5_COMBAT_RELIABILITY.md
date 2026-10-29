# Stage 1.5: Combat reliability fixes

## Scope and verification

This stage preserves the rule-based boss, player input, damage values, and attack clips. Findings are from C# and Unity YAML inspection. Unity Play Mode was not run, so runtime collision, navigation, and ragdoll behavior remain unverified.

## Verified defects fixed

- `PlayerCombat` only recognized colliders directly tagged `Enemy`. In `KnightScene.unity`, the active dragon `EnemyCombat` colliders are on untagged `Jaw01` and `R_Hand` children. A separate large root trigger also exists, so broad root/descendant acceptance would give an unreliable range. Player attack range now tracks only colliders that directly carry `EnemyCombat` and whose hierarchy root is tagged `Enemy`. A set tracks simultaneous overlaps correctly, and attacks are disabled after either combatant dies.
- `KnightScene.unity` sets the dragon `NavMeshAgent` stopping distance to 3.71, while both chase attack-entry and attack-state exit thresholds were 3.0. The boss could stop outside the range that starts an attack. Chase entry now uses `stoppingDistance + 0.5` (4.21 in this scene); attack exit uses `stoppingDistance + 0.75` (4.46), giving a small hysteresis band to reduce state toggling without approaching closer to the player.
- `CombatManager.PlayerTakeDamage()` could reduce player HP below zero and ran its death branch on later hits. It now clamps HP to 0–500, latches death once, disables movement once, and only applies hit-stop for nonlethal hits. Damage remains 95 and maximum HP remains 500.
- `RagDollManager.Update()` repeatedly invoked `RagdollOn()` while HP remained zero. The ragdoll activation is now guarded to run once; `RagdollOff()` clears the guard for initialization or explicit reuse.
- The two boss `EnemyCombat` trigger components could both damage the player during one swing. A per-`EnemyMovement` gate now opens in the existing `ClawAttackSlow`, `BasicAttackSlow`, or `HornAttackSlow` animation event and closes at the matching finish event. Both trigger colliders share that gate. `OnTriggerStay` allows a new swing to hit when the player remains overlapping across swings; the gate still prevents per-frame repeat damage. The existing `CombatManager.BossAttackHit` remains for failed-attack bookkeeping.
- The Stage 1 `SetAttackFast()` change remains. The fast setting is used during attack clips; `AttackState.OnStateExit()` restores Animator speed to 1.0 so the attack multiplier does not persist into chase/death transitions. Attack-specific slow/fast events and enrage/profile speeds remain in place.

## Files changed

- `Assets/Scripts/PlayerCombat.cs`: selected collider/root detection, overlap tracking, and death-state attack gate.
- `Assets/Scripts/ChaseState.cs`, `Assets/Scripts/AttackState.cs`: NavMesh-relative attack entry/exit thresholds; close the swing gate on attack-state entry; restore Animator speed on exit.
- `Assets/Scripts/CombatManager.cs`: clamp player HP and latch the lethal transition.
- `Assets/Scripts/RagDollManager.cs`: one-shot ragdoll activation guard.
- `Assets/Scripts/EnemyMovement.cs`: swing gate lifecycle and Animator speed restoration helper.
- `Assets/Scripts/EnemyCombat.cs`: shared per-swing hit guard and stay handling for continuous overlap.

The existing uncommitted Stage 1 changes in `CombatManager.cs`, `EnemyCombat.cs`, and `EnemyMovement.cs`, plus `STAGE1_COMBAT_STABILIZATION.md`, were preserved. No scene, prefab, Animator, Python, dependency, or ML-Agents files were edited.

## Reset requirements for later episodes (not implemented)

Future reset logic must restore: `CombatManager` player/boss HP, `PlayerIsDead`, `BossAttackHit`, `FailedAttacks`, `waiting`, active hit-stop coroutine and `Time.timeScale`; `EnemyMovement` `finishedAttack`, `playerHitThisSwing`, `enraged`, `berserk`, `dragonFastSpeed`, `chaseMultiplier`, Animator speed, transform, trails, and audio; Animator parameters/triggers/layers and state; `NavMeshAgent` destination/path/speed/stopped state; player transform, movement `YSpeed`/rotation state, controls, and CharacterController enabled state; `PlayerCombat` overlap set/`canAttack` and attack audio; ragdoll collider/rigidbody states; and health UI/fade state. Scene objects disabled during play and randomness/profile inputs also need deterministic initialization.

## Checks executed

- `git diff --check`: **passed** (no whitespace errors; only Git line-ending normalization warnings were printed).
- Source reference and animation event inspection: `ClawAttackSlow`/`Claw_fin`, `BasicAttackSlow`/`Basic_fin`, and `HornAttackSlow`/`Horn_fin` are present in the three current boss attack clips.
- No Unity Editor Play Mode run or automated gameplay test was performed. A C# compiler/build check was not available in the inspected environment.

## Required Unity Play Mode tests

1. Player attack: approach until the player's right-hand trigger overlaps the boss `Jaw01` or `R_Hand` attack collider; click once and verify boss HP decreases by exactly 75. Move out of overlap and verify clicking does not change boss HP. Confirm the large root trigger alone does not enable damage.
2. Boss attack: for claw, basic, and horn attacks, keep the player in the hit area; verify one swing removes at most 95 HP even if both boss colliders overlap. Stay overlapped for the next swing and verify it can cause one additional hit.
3. Range/navigation: start at the scene spawn and let the boss chase. Verify attack begins near 4.21 units and exits only beyond approximately 4.46 units; check no oscillation, player overlap, or unreachable attack state.
4. Player death: take repeated 95-damage hits until lethal. Verify HP ends at 0, movement is disabled, ragdoll activates once, and later trigger callbacks do not repeat lethal handling.
5. Boss death: apply 75-damage player attacks until HP reaches 0. Verify the existing death animation occurs and no further player damage is accepted.
6. Animator speed: observe normal, profile/enrage fast, and attack-complete transitions. Verify attack clip speed remains intentional and Animator speed returns to 1.0 after leaving `AttackState`.
7. Console: check for missing references, Animator parameter errors, NavMesh warnings, and animation-event warnings throughout the encounter.
