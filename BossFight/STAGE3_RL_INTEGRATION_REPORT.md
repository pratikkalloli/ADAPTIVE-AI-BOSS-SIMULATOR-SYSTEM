# Adaptive AI Boss — ML-Agents Integration Audit and Blocker Report

## Result

The project is **not ready for source integration in this run**. The installed Python trainer is a Release 20 candidate, but Unity Package Manager still has `com.unity.ml-agents` 2.0.2. The release-matched Unity package is 2.3.0-exp.3. I attempted a Unity 2021.3.15f1 batch open to resolve the candidate package; it exited with code 1 because the editor could not connect to an activated Unity licensing client and then timed out contacting Unity services. The package lock therefore could not be safely resolved or compilation verified.

No C# scripts, scenes, trainer configs, Python files, models, or package lock entries were changed. The manifest was temporarily changed for the resolver attempt and restored to the exact pre-attempt value, 2.0.2. Existing uncommitted project work remains intact. The pre-existing tracked diff was backed up at `%TEMP%\BossFight-pre-Stage3-RL-integration.patch`.

## Verified repository state

- Unity editor version: **2021.3.15f1** (`ProjectSettings/ProjectVersion.txt`).
- Candidate scene: `Assets/Scenes/KnightScene.unity`, enabled in the current Editor Build Settings working-tree state.
- Current Unity package pin: **`com.unity.ml-agents` 2.0.2**, with cached package metadata specifying Unity 2019.4 and Barracuda 2.0.0.
- Current Python environment: `BossFight/.venv-mlagents`, **Python 3.9.13**.
- Installed Python packages: `mlagents==0.30.0`, `mlagents-envs==0.30.0`, `torch==1.11.0+cpu`, and `protobuf==3.20.3`.
- `pip check` passed. The PyTorch import/version check passed and reported `torch.cuda.is_available() == False`. `mlagents-learn --help` exited with code 0.
- No `Agent` subclass, `BehaviorParameters`, or PPO configuration exists in the project Assets search.

## Compatibility decision

Unity ML-Agents Release 20 identifies this matching set:

| Component | Release 20 version | Evidence |
|---|---:|---|
| Unity package | `com.unity.ml-agents` **2.3.0-exp.3** | Release 20 package table |
| Unity package dependency | Barracuda **3.0.0** | Release 20 package metadata |
| Python trainer | `mlagents==0.30.0` | Release 20 package table and current venv |
| Python environment API | `mlagents-envs==0.30.0` | Release 20 package table and current venv |
| Python | **3.8.13–3.10.8** | Release 20 trainer metadata; current Python 3.9.13 is in range |
| PyTorch | **1.11.0+cpu** | Current venv; meets Release 20's minimum PyTorch 1.8.0 and uses CPU |

The current 2.0.2 Unity package is not the Release 20 pairing. This audit does not claim it is necessarily wire-incompatible in every case; it is an unverified, mixed-release setup and should not be used as the foundation for a trainer-facing Agent. Release 20 explicitly recommends using package versions from the same release. The required Unity upgrade should be performed with Unity's Package Manager and its lockfile resolution, not by hand-editing `packages-lock.json`.

References: [Release 20 version table and compatibility notes](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_20), [Release 20 Unity package metadata](https://github.com/Unity-Technologies/ml-agents/blob/release_20/com.unity.ml-agents/package.json), [Release 20 Python trainer metadata](https://github.com/Unity-Technologies/ml-agents/blob/release_20/ml-agents/setup.py), [Release 20 environment API metadata](https://github.com/Unity-Technologies/ml-agents/blob/release_20/ml-agents-envs/setup.py).

## Architecture findings confirmed in current source

- The rule-based controller is implemented by `Assets/Scripts/IdleState.cs`, `PatrolState.cs`, `ChaseState.cs`, and `AttackState.cs`. These behaviours write Animator parameters and NavMesh destinations; `AttackState.OnStateExit` also chooses the next attack clip based on the profile.
- `Assets/Scripts/EnemyMovement.cs` owns swing lifecycle events, shared boss hit gating, attack-animation speed hooks, profile speed variation, and failed-attack repositioning.
- `Assets/Scripts/EnemyCombat.cs` validates boss hitbox callbacks and calls the authoritative player damage path in `CombatManager.PlayerTakeDamage`.
- `Assets/Scripts/CombatManager.cs` owns HP, death flags, terminal CSV events, and the existing `R` scene-reload restart. There is no safe in-place ML episode reset API yet.
- `Assets/Scripts/DataLogger.cs` writes one version-2 telemetry CSV file per episode and rejects events after its terminal event. Its schema was not changed.
- `BossProfileReader` applies file-driven rule/profile adaptation; this is not PPO inference.
- The scene's dragon root is a prefab instance with a NavMeshAgent, root-motion Animator override, and `EnemyMovement`. The prefab's animation and attack-event pipeline should remain authoritative. Root motion and NavMesh movement coexist already and require runtime testing once a controller adapter is added.

## Proposed integration after the Unity package gate passes

1. Pin `com.unity.ml-agents` to 2.3.0-exp.3 through Package Manager and allow Unity to resolve Barracuda 3.0.0 into the lockfile.
2. Compile the untouched current working tree with that package before adding code.
3. Add `BossAgent` and discrete Behavior Parameters on a dedicated scene object, retaining `RuleBased` as the serialized default on the boss controller.
4. Route policy intents through `EnemyMovement`/the existing Animator and NavMesh path. Gate only the decision-writing portions of `IdleState`, `PatrolState`, `ChaseState`, and `AttackState` while the RL mode is selected; preserve `EnemyCombat`, animation events, hit gates, damage, and death as authority.
5. Use one discrete branch for `Approach`, `Retreat`, `Attack`, and `Hold`. Keep observations fixed-size and normalized: horizontal relative direction (2), distance (1), player/boss HP fractions (2), one-hot current boss state (4), attack-ready (1), player-dead (1), boss-dead (1): **12 vector observations**. Use `BehaviorParameters` with one discrete branch of size 4.
6. Deliver damage and terminal rewards from authoritative `CombatManager` events. A bounded initial proposal is +0.1 per accepted boss hit, -0.1 per damage event received by the boss, +1 for boss win, -1 for boss loss, and a small per-decision time cost. Do not award repeated alive rewards.
7. Add an in-place reset only after explicitly resetting the manager HP/flags, transforms, Animator state/parameters, NavMesh path, boss swing/failure flags and enrage state, player attack overlap/input sequence, ragdoll bone pose/rigidbody velocities, UI/hit-stop state, and logger episode file. Keep `R` scene reload unchanged.
8. Add a small CPU PPO YAML after heuristic-only action-interface validation. Train only after the Unity/Python communicator smoke test passes.

These are design proposals, not implemented or runtime-verified changes. The source of the action conflict and reset complexity is described in the existing [Stage 3.1 architecture audit](STAGE3_1_MLAGENTS_ARCHITECTURE_AUDIT.md).

## Blocker and next action

The attempted exact command was equivalent to:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2021.3.15f1\Editor\Unity.exe' `
  -batchmode -quit `
  -projectPath '<BossFight project directory>' `
  -logFile '<temporary Unity log path>'
```

Unity exited with code **1** after: Licensing Client IPC timeout, `BatchMode: Unity has not been activated with a valid License`, and Unity services request timeout. No package resolution occurred. The next safe action is to activate/sign in to this Unity 2021.3.15f1 installation, open the project in the Editor, and install `com.unity.ml-agents` **2.3.0-exp.3** through Package Manager. Let Unity resolve the lockfile, then compile. Do not proceed with source integration until that exact package is visible in the resolved lock and the project compiles.

After the package gate, remaining work includes the Agent/mode adapter, state-behaviour gates, authoritative reward/terminal callbacks, deterministic in-place episode reset, scene Behavior Parameters, PPO YAML, editor heuristic test, and a short Unity-Python connection smoke test. None was attempted in this run.

## Checks actually performed

- Inspected `git status`, tracked diff, package manifest/lock, cached package metadata, Stage 3.1 audit, combat/state/profile/logger source, scene/build settings, and Animator Controller parameters/transitions.
- Verified Python and ML-Agents package versions in `.venv-mlagents`.
- `python -m pip check`: **passed**.
- PyTorch import and CPU availability: **passed** (`1.11.0+cpu`, CUDA unavailable).
- `mlagents-learn --help`: **passed** (exit code 0).
- Unity batch package resolution/compile attempt: **failed before compilation** because the editor was not activated; no Play Mode test or training run occurred.
