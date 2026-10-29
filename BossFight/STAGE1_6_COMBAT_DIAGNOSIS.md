# Stage 1.6: Combat diagnosis, reliability, and balance

## A. Git status and preserved work

This report was added in Stage 1.6. Existing uncommitted Stage 1 and Stage 1.5 work was preserved. Modified scripts: `AttackState.cs`, `ChaseState.cs`, `CombatManager.cs`, `EnemyCombat.cs`, `EnemyMovement.cs`, `PlayerCombat.cs`, `PlayerMovement.cs`, and `RagDollManager.cs`. New report: `STAGE1_6_COMBAT_DIAGNOSIS.md`. The working tree also contains the untracked `STAGE1_COMBAT_STABILIZATION.md` and `STAGE1_5_COMBAT_RELIABILITY.md` reports. No scene, prefab, Animator, Python, package, or ML-Agents asset was changed in this stage.

Stage 1 changes preserved: invalid `GetComponent<GameObject>()` calls removed in `CombatManager.cs`; boss fast-speed short-circuit changed to `SetAttackFast()` in `EnemyCombat.cs`; `SetAttackFast()` added to `EnemyMovement.cs`.

Stage 1.5 changes preserved: boss-root/child attack-collider filtering and overlap tracking in `PlayerCombat.cs`; player HP clamping/death latch and one-shot ragdoll guard; per-swing boss hit gate; stopping-distance-relative attack entry/exit with hysteresis; Animator speed restoration on attack-state exit.

## B. Findings, ranked by confidence

### 1. Repeated damage is once per looped boss attack swing, not once per contact frame — confirmed code behavior

`EnemyCombat.OnTriggerEnter/OnTriggerStay()` share `EnemyMovement.TryRegisterPlayerHitThisSwing()`, so only one of the two `EnemyCombat` components can claim a given swing. The gate opens at `ClawAttackSlow`, `BasicAttackSlow`, or `HornAttackSlow` and closes at the matching `*_fin` event. If both boss hitboxes overlap the player during one swing, the first accepted callback consumes the shared per-boss gate; later callbacks cannot apply another 95 damage until a new slow event opens it.

However, the three source animation clips have `m_LoopTime: 1`, and their slow/fast/finish events repeat on each clip loop. The Animator remains in `AttackState` while `isAttacking` remains true and distance is within the exit range. Therefore the guard permits one hit on every later looped swing. There is no inter-swing cooldown or recovery timer in the current code. Six valid swings can kill a 500 HP player; this is a likely explanation for early death, but the actual hit cadence still needs Play Mode logs. This is repeated valid swings rather than duplicate per-frame damage.

### 2. The chase can close much faster than the Inspector speed suggests — likely contributor, runtime confirmation required

`KnightScene.unity` serializes the agent speed as 2.27, but `ChaseState.OnStateEnter()` overwrites it with `agent.speed = 20f`. This is about 8.8 times the serialized speed. In conjunction with a 3.71 stopping distance and 4.21/4.46 transform-distance thresholds, this may produce overshoot or rapid attack/chase transitions. It is a verified runtime setting, but whether speed 20 was intentional cannot be established from source, so it was left unchanged as a possible balance/behavior choice.

### 3. Scene enables root motion on an object also driven by NavMeshAgent — configuration risk, runtime confirmation required

The dragon prefab has `Animator.applyRootMotion` off, but `KnightScene.unity` overrides it to on. `ChaseState` retrieves the `NavMeshAgent` from the same Animator object, and no `OnAnimatorMove()` synchronization method exists. The agent’s serialized `updatePosition` is not explicitly overridden, so Unity's default is true. This is a competing movement-ownership setup worth checking against the observed 0.864 distance and repeated state exits. It was not changed because the intended attack lunges/root-motion behavior cannot be determined from files alone. Unity documents that the default agent position synchronization is enabled and that explicit root-motion coupling normally requires deliberate synchronization. [NavMeshAgent.updatePosition](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AI.NavMeshAgent-updatePosition.html), [Animator.applyRootMotion](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Animator-applyRootMotion.html).

### 4. Failed attack exits cause a 2-unit direct reposition after three misses — confirmed behavior; contribution plausible

`AttackState.OnStateExit()` increments `CombatManager.FailedAttacks` when `BossAttackHit` is false. `EnemyMovement.Update()` then shifts the dragon backward by 2 units when the counter reaches 3 and resets the counter. This matches the observed `Boss Repositioning` message and can make player damage harder by moving the boss away. The logs do not prove why the three attack states missed; collider geometry, root motion, movement speed, and player position need to be observed in Play Mode.

### 5. Player damage is input-timed, and range is overlap-based — confirmed code behavior; geometry unverified

`PlayerCombat.Update()` calls `CheckAnimation()`, which calls `PlayerMovement.PlayerAttack()`; if that call starts an attack and `canAttack` is true, `DragonTakeDamage()` runs immediately on that input frame. There is no player attack impact-frame damage event. A click produces at most one `DragonTakeDamage()` call through this component update, though before Stage 1.6 both `PlayerMovement.Update()` and `PlayerCombat.Update()` called `PlayerMovement.PlayerAttack()` on the same input frame and could set/reset Animator triggers twice. Stage 1.6 removes that duplicate animation call; only `PlayerCombat` owns the attack invocation now.

Range is enabled only by trigger overlap with a collider that directly has `EnemyCombat` and belongs to a root tagged `Enemy`; the large root trigger is ignored. A `HashSet<Collider>` handles simultaneous enter/exit overlap. Out-of-range attacks may still play their animation, but do not call `DragonTakeDamage()`. Whether the hand trigger reaches the intended body region at a fair distance needs Editor verification.

### 6. Repeated `AttackState.OnStateExit()` logs are consistent with range exits/re-entries — plausible, not proven

The controller transitions from `AttackState` to `ChaseState` when `isAttacking` is false, with exit-time gating and a 0.25-second transition. `AttackState.OnStateUpdate()` clears `isAttacking` only beyond `stoppingDistance + 0.75`; chase sets it again inside `stoppingDistance + 0.5`. The 0.25-unit hysteresis avoids a single shared threshold but does not prevent repeated state exits if movement/root motion carries the dragon outside the outer threshold. New Editor-only entry/exit logs record distance, thresholds, hit bookkeeping, and HP so Play Mode can confirm the pattern.

### 7. Balance cannot yet be separated from hit cadence — no damage tuning applied

Values remain 500 player HP, 1500 boss HP, 75 player damage, and 95 boss damage. That means 20 accepted player hits to defeat the boss and six boss hits to defeat the player. The supplied logs prove that player-to-boss damage happened at least once and that the actors approached closely; they do not provide accepted hit counts, timestamps, or actual HP history. No damage or attack-speed tuning was applied.

## C. Stage 1.6 fixes and intentionally unchanged issues

- `PlayerMovement.Update()` no longer calls `PlayerAttack()` as well as `PlayerCombat.Update()`. This removes a duplicate same-frame Animator trigger invocation; `PlayerCombat` still routes the same mouse input through `PlayerMovement.PlayerAttack()` and applies damage at most once per input update.
- `EnemyCombat` now checks both combatants' HP before claiming the swing gate, taking damage, or playing `getHit`/fade feedback. This prevents post-death hit feedback and dead-boss callbacks from being treated as accepted hits.
- `CombatManager.DragonTakeDamage()` now clamps boss HP and latches boss death once. Existing values and Animator death parameters are preserved.
- `EnemyMovement.Update()` stops and clears its NavMeshAgent once boss HP reaches zero, and returns before enrage/reposition logic. This prevents a failed-attack reposition after death.
- `AttackState.OnStateExit()` skips failed-attack bookkeeping and next-attack selection if the boss is already dead. Normal state exits retain existing behavior.
- `PlayerCombat` passes live HP values to the existing `DataLogger` calls instead of placeholder 100/100 values.
- Editor-only `[CombatDiag]` logs were added at attack attempts, accepted damage, swing start/finish, gate rejection/acceptance, state entry/exit, reposition, and death transitions. They are guarded by `UNITY_EDITOR`; remove those logging blocks after the Play Mode diagnosis is complete. They do not log every frame.

Intentionally unchanged: 20-unit chase speed, scene root-motion override, 4.21/4.46 thresholds, damage values, attack-speed profiles, looping attack clips, continuous attack behavior while the player remains in range, 2-unit reposition distance, and input-timed player damage. These require runtime evidence or separate balance review before changing.

## D. Validation

- Read-only source/scene inspection confirmed scene `NavMeshAgent` values (speed 2.27, acceleration 5.28, stopping distance 3.71), the scene root-motion override, the prefab root-motion default, the `Nightmare.controller` parameters/transitions, and the three attack clips' event names/times and loop flags.
- Source reference search confirmed the animation-event methods remain present: `ClawAttackSlow`, `ClawAttackFast`, `Claw_fin`, `BasicAttackSlow`, `BasicAttackFast`, `Basic_fin`, `HornAttackSlow`, `HornAttackFast`, and `Horn_fin`.
- `git diff --check`: **passed** after the edits (exit code 0; Git printed only LF-to-CRLF normalization warnings).
- Static `rg` reference checks: **passed** for the damage entry points, swing-gate methods, existing animation-event method names, and Animator parameters `isPatrolling`, `isChasing`, `isAttacking`, `attacks`, and `dead`.
- Diff review: **completed** for all modified C# scripts. The new report plus the two earlier untracked stage reports are present in `git status`.
- No C# build/compiler or Unity Editor Play Mode test was run. The available environment did not expose a `dotnet`, `csc`, or `mcs` compiler in PATH during earlier checks. This report does not claim runtime behavior passed.

## E. Manual Unity Play Mode test plan

1. **One player hit in range:** open `KnightScene`, record boss HP, place the player's right-hand trigger over a dragon `EnemyCombat` collider, click once. Expect one `[CombatDiag] Player attack accepted` log and exactly 75 boss HP lost. The `CombatManager` log should show before/after values.
2. **Player attack outside range:** move away from both attack-bone colliders but remain near the large root trigger; click. Expect an out-of-range rejection with no boss HP change. The large root trigger alone must not enable damage.
3. **One boss swing / two hitboxes:** hold the player in both boss trigger regions through a claw, basic, and horn swing. Expect one 95 HP loss maximum per swing ID; any second hitbox callback should report the gate consumed. No per-frame HP decrease should occur.
4. **Next swing:** remain overlapping for a second loop/swing. Expect a new swing ID after the next slow event and at most one additional 95 HP loss. Record times between accepted swings; this distinguishes intentional loop cadence from duplicate callbacks.
5. **Player death:** receive six accepted boss hits. Expect HP sequence 500, 405, 310, 215, 120, 25, 0; movement disables and ragdoll activates once. Later overlaps should not play new boss hit/fade feedback or change HP.
6. **Boss death:** land 20 accepted 75-damage player attacks. Expect boss HP 0, one death-transition log, agent stopped, no later boss attack acceptance/reposition, and the existing death animation/physics behavior.
7. **Approach/state behavior:** observe distances and state logs from spawn until several attacks. Confirm the agent does not overshoot or repeatedly cross the 4.21 entry/4.46 exit range. Record actual NavMeshAgent speed, transform position, distance, and whether root motion is moving the object independently. Check whether repeated state exits follow measured distance crossings and whether 3 failed exits move the boss 2 units.
8. **Attack speed:** for each clip, verify Slow → Fast → finish event order, one gate-open interval per loop, and Animator speed restoration to 1.0 when leaving `AttackState`. Confirm any intended profile/enrage fast speed still applies within attack clips.
9. **Console:** verify no missing Animator parameters, animation-event receiver warnings, NavMesh errors, missing references, or new exceptions. Save the diagnostic logs with timestamps and HP for evaluation.
