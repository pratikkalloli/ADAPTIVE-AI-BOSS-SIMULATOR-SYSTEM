# Stage 3 — ML-Agents Integration Foundation

## Package and compatibility gate

Verified in the current checkout:

- Unity project version: `2021.3.15f1` (`ProjectSettings/ProjectVersion.txt`).
- `Packages/manifest.json`: `com.unity.ml-agents` is `2.3.0-exp.3`.
- `Packages/packages-lock.json`: ML-Agents resolves to `2.3.0-exp.3` and requires `com.unity.barracuda` `3.0.0`; Barracuda resolves to `3.0.0`.
- `Library/PackageCache` contains both exact packages.
- `.venv-mlagents`: Python `3.9.13`, `mlagents` and `mlagents-envs` `0.30.0`, PyTorch `1.11.0+cpu`, protobuf `3.20.3`. `pip check` found no broken requirements.

These versions match the requested Release 20 pairing. No package manifest, lockfile, or Python dependency was changed in this stage.

## Integration

`BossAgent` is a separate `Unity.MLAgents.Agent`. It requests actions through `EnemyMovement`; it never writes health, invokes damage methods, bypasses hitboxes, or forces arbitrary Animator states. The existing player/boss combat scripts and `CombatManager` remain authoritative for damage and terminal outcomes.

The vector observation contract is fixed at 12 values, in this order:

1. Boss-local direction to the player X and Z (`[-1, 1]`).
2. Planar distance divided by 20 and clamped to `[0, 1]`.
3. Player HP / 500 and boss HP / 1500, clamped to `[0, 1]`.
4. Four-value one-hot Animator state: `IdleState`, `PatrolState`, `ChaseState`, `AttackState`.
5. Whether the current Chase state and range permit a basic attack.
6. Player-dead and boss-dead flags.

One discrete branch of size four is configured:

| Action | Request |
|---:|---|
| 0 | Approach the player through the existing NavMeshAgent and Chase Animator state. |
| 1 | Retreat using the existing two-unit reposition target. |
| 2 | Request the existing Basic attack animation at valid range. |
| 3 | Hold position and clear movement/attack intent. |

The Claw and Horn animations are not exposed as learned actions in this first contract. `Heuristic()` provides a deterministic close-range test policy. The new scene Behavior Parameters are set to **Heuristic Only**; change Behavior Type to **Default** only when connecting the Python trainer.

Rewards use actual HP deltas emitted from the `CombatManager` damage application path: +0.10 per normalized boss hit on the player, -0.10 per normalized player hit on the boss, +1 for `boss_win`, and -1 for `player_win`. An unavailable/invalid request receives -0.002 once per decision. There is no reward for merely remaining alive.

## Controller isolation and reset

`EnemyMovement.ControllerMode` is serialized and defaults to `RuleBased`. In that mode, the Agent returns without sending commands. In `MLAgents` mode, the decision-writing portions of `IdleState`, `PatrolState`, `ChaseState`, and `AttackState` are gated. AttackState entry/exit lifecycle work, animation events, the shared swing hit gate, `EnemyCombat`, and damage application remain active.

On terminal damage, `CombatManager` writes the existing EpisodeEnd event and publishes the actual outcome. The Agent issues one terminal reward and waits 1.5 real-time seconds before ending the ML episode, allowing the existing boss death animation or player ragdoll to run. The subsequent episode reset restores both combatants' starting transforms, HP/death flags, player Animator and movement state, ragdoll bone pose and rigidbody velocities, boss Animator/NavMesh/swing/enrage state, combat contact guards, UI/VFX, and time scale. `DataLogger` starts a new unique v2 CSV file and EpisodeStart row. The R-key scene reload remains available.

The reset path is source-implemented but has not been exercised in Unity Play Mode. In particular, root-motion/NavMesh interaction and ragdoll restoration need manual runtime verification.

## PPO configuration

`AITraining/BossPPO.yaml` uses PPO, a small 2-layer 128-unit network, batch size 64, buffer 1024, time horizon 64, and explicitly selects `torch_settings.device: cpu`. Its behavior name is `AdaptiveBoss` and matches the scene.

After confirming the heuristic smoke test, from the `BossFight` directory run:

```powershell
.\.venv-mlagents\Scripts\mlagents-learn.exe AITraining\BossPPO.yaml --run-id=adaptive_boss_cpu_stage3_20261017
```

Then press Play in `KnightScene`. Do not start a long run until Unity reports a trainer connection and short test episodes end/reset correctly. The current player remains keyboard-controlled; unattended and repeatable training still requires a scripted player/opponent policy or an externally controlled test harness.

For a built environment, add `--env=Builds\BossFight.exe` to the command. Use a new run ID for later runs instead of forcing an existing run directory.

## Changed files

- `Assets/Scripts/BossAgent.cs` and `.meta`: Agent observations, action handling, heuristic, reward subscription, terminal handling.
- `Assets/Scripts/EnemyMovement.cs`: RuleBased/MLAgents mode, validated intent adapter, and boss episode reset support.
- `Assets/Scripts/IdleState.cs`, `PatrolState.cs`, `ChaseState.cs`, `AttackState.cs`: stop rule-based decisions from overwriting Agent intent in MLAgents mode; preserve attack lifecycle and baseline behavior.
- `Assets/Scripts/CombatManager.cs`: authoritative damage/outcome events and in-place fight reset.
- `Assets/Scripts/EnemyCombat.cs`, `PlayerCombat.cs`, `PlayerMovement.cs`, `RagDollManager.cs`, `UI_Manager.cs`: reset transient attack guards, movement, ragdoll, and UI state for the next episode.
- `Assets/Scripts/DataLogger.cs`: start a unique telemetry file for the next episode without changing the v2 schema.
- `Assets/Scenes/KnightScene.unity`: add the `BossRLAgent` object with 12 vector observations, one four-action discrete branch, Heuristic Only mode, and a 10-step DecisionRequester.
- `AITraining/BossPPO.yaml`: CPU PPO trainer configuration.
- `STAGE3_RL_IMPLEMENTATION_REPORT.md`: this report.

The project already had uncommitted changes before this stage. Those changes were preserved; the list above identifies files touched by this integration, not a claim that each file's full working-tree diff originated here.

## Validation actually run

- Package manifest, lockfile, PackageCache, Unity version, and Python package versions inspected.
- `.venv-mlagents` `pip check`: passed.
- ML-Agents 0.30.0 trainer config parsing with its installed `RunOptions` parser: passed; parsed `trainer_type=ppo`, behavior `AdaptiveBoss`, batch size 64, two network layers, and CPU device.
- Unity Roslyn static compilation using the Unity 2021.3 generated Assembly-CSharp response file/references plus `BossAgent.cs`: passed (exit 0). Two warnings remain: existing unused `EnemyMovement.normalSpeed` and `projectileActor.projectileSimFire` fields.
- An initial Mono `mcs` attempt was not a valid compile check: that older compiler rejected the Unity response file's `-langversion:9.0`. The Roslyn check above replaced it.
- Unity Editor batch attempt: command used `Unity.exe -batchmode -nographics -projectPath <BossFight> -quit -logFile <BossFight/UnityStage3Compile.log>`; the process returned `127` and did not create the requested log. Unity Editor compilation therefore remains unverified.
- No Play Mode fight, trainer connection, PPO run, or model training was performed.

## Manual verification checklist

1. Open `Assets/Scenes/KnightScene.unity`; confirm no missing scripts and that Behavior Parameters show `AdaptiveBoss`, 12 observations, one discrete branch of size 4, and Heuristic Only.
2. Leave the dragon `EnemyMovement.ControllerMode` at `RuleBased`. Play, verify the original state-machine boss behavior and player controls, both damage directions, both death paths, and R-key reload.
3. Set the dragon mode to `MLAgents` while keeping Heuristic Only. Play and verify deterministic approach/retreat/basic-attack requests; accepted boss damage must still come only from existing hitboxes and animation events.
4. Defeat each combatant in separate trials. Confirm the death animation/ragdoll is visible before reset, HP/positions/Animator/NavMesh/UI/time scale return to their starting values, and a new v2 CSV has one EpisodeStart and a unique episode ID.
5. Repeat several episodes and inspect for stuck Animator transitions, NavMesh warnings, duplicate damage, stale hit gates, telemetry after terminal rows, or ragdoll pose drift.
6. Only after the heuristic test passes, set Behavior Type to Default, launch the CPU trainer command above, press Play, and confirm a communicator connection plus a short completed episode before continuing training.
