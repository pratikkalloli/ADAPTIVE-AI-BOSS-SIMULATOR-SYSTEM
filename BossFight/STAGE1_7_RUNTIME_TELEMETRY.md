# Stage 1.7: Runtime combat telemetry

## Scope

This stage adds temporary Editor-only diagnostics for the existing rule-based combat flow. It does not add ML-Agents, PPO, observations, rewards, episode resets, or a new data-file schema. The current damage values and combat calls are retained. These are source changes only; Unity Play Mode has not been run here.

## Diagnostic events

All new `[CombatDiag]` messages begin with `t=<Time.time seconds>` and are compiled only under `#if UNITY_EDITOR`.

| Event | Meaning |
| --- | --- |
| `PlayerAttackInput` | Left mouse input received; includes player attack sequence, player-to-boss distance. |
| `PlayerAttackAccepted` | Attack animation began and a qualifying boss attack collider was in range; includes target collider, distance and boss HP before damage. This is an accepted attack attempt, not the authoritative damage record. |
| `PlayerAttackRejected` | Input or attack start was rejected; reason and distance are included. Reasons include dead/missing combat manager, active attack animation, and no qualifying collider in range. |
| `PlayerDamageApplied` | Authoritative player-to-boss damage application; attack sequence, boss HP before/after, damage, and player HP. |
| `PlayerDamageRejected` | Damage call reached the manager after boss death; includes sequence and final HP values. |
| `BossSwingStarted` | Boss swing ID and attack type assigned at the corresponding `*AttackSlow` event; includes distance and NavMesh speed, stopping distance, and path status. |
| `BossHitGateOpened` | Shared gate opened by `ClawAttackSlow`, `BasicAttackSlow`, or `HornAttackSlow`. |
| `BossHitGateClosed` | Gate closed by attack-state entry, an attack finish event, or the accepted hit consuming the one-hit allowance. `source` identifies which transition closed it. |
| `BossHitAttempt` | First relevant player-collider contact reported by a given `EnemyCombat` hitbox during a swing. Attempts are logged at most once per hitbox/swing to avoid per-frame `OnTriggerStay` spam. |
| `BossHitAcceptedByGate` | Hitbox claimed the shared swing gate. The authoritative HP change is recorded separately by `BossDamageApplied`. |
| `BossHitRejected` | Relevant contact was rejected because the Animator was outside `AttackState`, either actor was dead, the finish event had closed the gate, or another hitbox already consumed the shared gate. |
| `BossDamageApplied` | Authoritative boss-to-player damage application; swing ID, player HP before/after, damage, and boss HP. |
| `BossDamageRejected` | Damage call reached the manager after player death; includes swing ID and final HP values. |
| `AttackStateEntered` / `AttackStateExited` | State transition with game time, distance, thresholds, NavMesh speed/stopping distance/path status, HP, and exit reason. |
| `FailedAttackCounterChanged` | Counter increment or reset, its prior/new value, reason, and distance. |
| `BossRepositionStart` / `BossRepositionEnd` | Position and distance around the existing reposition, plus useful NavMesh status. |
| `PlayerDeathTransition` / `BossDeathTransition` | One-shot lethal transition with final player and boss HP. |

The boss swing ID remains owned by the single `EnemyMovement` component. Both `EnemyCombat` hitboxes read `CurrentSwingId`; neither creates an ID. A new ID is assigned only when an attack's existing `*AttackSlow` animation event calls `BeginAttackSwing`. The per-swing hit gate and attack-event method names/signatures are otherwise preserved.

## Files changed

- `Assets/Scripts/PlayerCombat.cs`: Editor-only player input/accept/reject records and per-input attack sequence IDs; passes the ID to the existing damage manager path.
- `Assets/Scripts/CombatManager.cs`: records authoritative HP changes, damage amount, caller sequence ID, death HP, and already-dead rejection. Existing parameterless methods remain as wrappers for compatibility.
- `Assets/Scripts/EnemyCombat.cs`: records each relevant collision attempt at most once per hitbox per swing, its reason/result and collider context; passes the shared swing ID into the damage manager.
- `Assets/Scripts/EnemyMovement.cs`: timestamps swing/gate lifecycle and reposition/counter resets, including NavMesh context.
- `Assets/Scripts/AttackState.cs`: timestamps state entry/exit and missed-attack counter increments with distance and agent context.
- `STAGE1_7_RUNTIME_TELEMETRY.md`: this event reference and capture/removal procedure.

No scene, prefab, Animator asset, Python file, package manifest, damage value, or ML-Agents asset was changed for Stage 1.7.

## Static validation

Run from the repository root:

```powershell
git diff --check
```

The source review should also confirm all `[CombatDiag]` calls in the five scripts include game time and are inside `#if UNITY_EDITOR` blocks, the animation-event methods remain named `ClawAttackSlow`, `ClawAttackFast`, `Claw_fin`, `BasicAttackSlow`, `BasicAttackFast`, `Basic_fin`, `HornAttackSlow`, `HornAttackFast`, and `Horn_fin`, and no damage values or behavior branches changed. Static checks do not establish runtime collision or Animator behavior.

## Unity Play Mode capture procedure

1. Open the project in Unity 2021.3.15f1 and open `Assets/Scenes/KnightScene.unity`.
2. Open **Window → General → Console**. Turn **Collapse** off so separate hitbox events remain visible. Clear the Console immediately before entering Play Mode.
3. Start Play Mode and let the encounter run normally. Avoid changing damage, Animator speed, movement, scene settings, or the player/boss position through Inspector edits during the run.
4. Keep the Console visible until the player or boss death transition occurs. The output should include `PlayerAttackInput` / accepted or rejected outcome, `BossSwingStarted`, gate events, hit attempts/rejections, authoritative damage records, state/counter/reposition events, and death.
5. Copy all `[CombatDiag]` lines from the run in their original order. Include the first swing start through the terminal death event, and retain non-diagnostic exception/warning lines from the same interval. If the Unity Console copy operation drops timestamps or lines, use the Editor log file for the same run.
6. Repeat from a fresh Play Mode start for another sample. Do not combine separate runs without marking their boundaries; `Time.time` and both sequence counters restart with a new scene play session.

## Removing temporary telemetry later

After collecting and reviewing the logs, remove only the Stage 1.7 telemetry additions: the `[CombatDiag]` blocks, player attack sequence counter, per-hitbox log-throttling field, and ID-context damage overloads/wrapper forwarding added for telemetry. Keep the pre-existing Stage 1.5/1.6 behavior and any independently required fixes. Review the diff against the pre-Stage 1.7 working tree before committing or reverting anything; do not reset the whole working tree because it contains earlier uncommitted project work.

## Limitations

- `BossHitAttempt` is intentionally throttled to once per `EnemyCombat` component per swing. Repeated stay callbacks in that same swing are not logged individually, so Console volume stays bounded.
- Player attack range is tracked as a set of overlapping boss attack colliders. `PlayerAttackAccepted.target` reports one currently overlapping collider, not necessarily every overlap.
- `Time.time` is scaled game time. The existing hit-stop sets `Time.timeScale` to zero briefly, so these timestamps pause during hit-stop; use `Time.realtimeSinceStartup` only if real elapsed wall time is needed in a later diagnostic pass.
- This report describes instrumentation, not successful gameplay validation. Manual Editor testing remains required.
