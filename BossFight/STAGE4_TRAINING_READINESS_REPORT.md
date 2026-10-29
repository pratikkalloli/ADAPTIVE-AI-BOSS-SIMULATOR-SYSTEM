# Stage 4 — Training Readiness Report

## Result

The project now has an opt-in scripted player policy for unattended training, trainer-compatible behavior settings, a short PPO smoke configuration, and episode outcome/length statistics. The normal scene defaults remain rule-based boss plus keyboard player. No trainer run, model export, or Unity Play Mode fight was started.

## Lifecycle assertion

The current `%LOCALAPPDATA%/Unity/Editor/Editor.log` contains the reported assertion. In the recorded stack, ML-Agents `Agent.OnDisable()` calls `NotifyAgentDone()`, which calls `BossAgent.CollectObservations()`. The then-current `CollectObservations()` called `ResolveReferences()`, where `GameObject.Find("NightmareDragon")` and `GameObject.Find("GameManager")` ran while scene objects were being unloaded. The log records `go.IsActive()` at both lookups. This confirms the teardown-time scene query as the source path.

`BossAgent` now checks that it is active before resolving objects. During the ML-Agents disable notification it contributes twelve zero observations and does no scene lookup or scene-object access. During active play it retains the twelve live observations. A disable also clears the delayed episode-end coroutine flag before calling the required base `Agent.OnDisable()` implementation. The shape matches `KnightScene`'s 12-vector setting.

## Changes made

- `Assets/Scripts/BossAgent.cs`: lifecycle guards; optional `trainingMode` / `--boss-training` activation selects `MLAgents` boss mode and enables the scripted player policy; reports `Combat/BossWin`, `Combat/PlayerWin`, and `Combat/EpisodeLength` to ML-Agents statistics on terminal events.
- `Assets/Scripts/PlayerMovement.cs`: accepts an optional world-space movement/facing/dodge command. Keyboard input remains the default; scripted mode suppresses keyboard movement and attack input.
- `Assets/Scripts/PlayerCombat.cs`: accepts a queued scripted attack only through its existing alive, animation-transition, and collider-overlap gates. Accepted hits still call `CombatManager.DragonTakeDamage`; no second damage path was added.
- `Assets/Scripts/ScriptedPlayerPolicy.cs` and `.meta`: optional deterministic approach/evade/attack policy. It requests movement and attacks through the existing components and never writes HP or forces an animation state.
- `Assets/Scripts/CombatManager.cs`: caches the optional policy and resets its decision timers during the existing in-place episode reset.
- `Assets/Scenes/KnightScene.unity`: attaches the policy with `trainingEnabled: 0`, links it to the agent with `trainingMode: 0`, and changes Behavior Parameters from `HeuristicOnly` to `Default` so a connected trainer can control it. The boss controller's source default remains `RuleBased`.
- `AITraining/BossPPO.smoke.yaml`: new 2,000-step CPU PPO smoke configuration. `AITraining/BossPPO.yaml` remains the longer 2,000,000-step CPU configuration.

The scripted policy's distances and timing are initial test values, not validated balance settings. Damage values, hitboxes, animation events, boss attack timing, and the logger schema were not changed.

## Configuration contract

| Setting | Required value |
|---|---|
| Unity package | `com.unity.ml-agents` 2.3.0-exp.3; Barracuda 3.0.0 |
| Python packages | `mlagents==0.30.0`, `mlagents-envs==0.30.0` |
| Behavior name | `AdaptiveBoss` |
| Vector observations | 12, stack size 1 |
| Actions | one discrete branch of size 4: Approach, Retreat, BasicAttack, Hold |
| Training device | CPU |
| Default play mode | `EnemyMovement.ControllerMode.RuleBased`; `trainingMode` and scripted policy off |

During Agent initialization, `trainingMode` in the `BossRLAgent` Inspector or the executable argument `--boss-training` selects `MLAgents` controller mode and turns on the scripted player. With training mode off, the existing boss state machine and keyboard controls remain in charge. For heuristic-only testing, set Behavior Parameters to `HeuristicOnly`, enable training mode, and run without a trainer. For PPO training, leave Behavior Parameters at `Default`.

## Training commands

Open `Assets/Scenes/KnightScene.unity` in Unity. In the `BossRLAgent` Inspector, enable `trainingMode` before entering Play Mode, and leave the scene unsaved if you want to retain the baseline default. Start one trainer command below in a PowerShell window, wait for it to listen on port 5004, then enter Play Mode. These commands are instructions only; they were not run.

Short connection/learning smoke test:

```powershell
& ".\.venv-mlagents\Scripts\mlagents-learn.exe" `
  "AITraining\BossPPO.smoke.yaml" `
  --run-id adaptiveboss-smoke-20261017-a `
  --results-dir "AITraining\Results" `
  --base-port 5004
```

Longer training (not started):

```powershell
& ".\.venv-mlagents\Scripts\mlagents-learn.exe" `
  "AITraining\BossPPO.yaml" `
  --run-id adaptiveboss-ppo-20261017-a `
  --results-dir "AITraining\Results" `
  --base-port 5004
```

For a standalone Unity build instead of the Editor, build `KnightScene` as the environment and pass `--env Builds\KnightTraining\BossFight.exe --no-graphics --base-port 5004 --env-args --boss-training` to `mlagents-learn`; the argument enables the scripted opponent and ML boss mode in the build. Use a fresh run ID for each run. ML-Agents stores each run under `AITraining/Results/<run-id>/`, including summaries, checkpoints and exported ONNX files. No `--force` is included, so existing results are not overwritten.

Standard trainer summaries include episode reward and length. The added `Combat/BossWin` and `Combat/PlayerWin` values are 1/0 terminal metrics; `Combat/EpisodeLength` records the Agent step count. Existing CSV telemetry remains the source for detailed damage and terminal event rows.

## Inference steps

After an actual training run exports `AdaptiveBoss.onnx`, copy/import that model under `Assets/ML-Agents/Models/` in Unity. In `BossRLAgent` Behavior Parameters, assign the model, keep behavior name `AdaptiveBoss`, and select `InferenceOnly`. In `EnemyMovement`, select `MLAgents`; leave `BossAgent.trainingMode` and the scripted player policy off to play against the model with the keyboard-controlled player. Select `RuleBased` to return to the original baseline. No trained model is present or claimed by this report.

## Checks actually run

- `git diff --check`: passed; only existing line-ending warnings were printed for pre-existing modified files.
- Python virtual environment: Python 3.9.13; `mlagents` 0.30.0; `mlagents-envs` 0.30.0; `torch` 1.11.0+cpu; protobuf 3.20.3; CUDA unavailable.
- `python -m pip check`: passed (`No broken requirements found`).
- `mlagents-learn --help`: passed; confirms `--base-port`, `--env`, `--env-args`, `--results-dir`, and `--no-graphics` options.
- ML-Agents 0.30.0 `RunOptions.from_dict(load_config(...))`: both PPO YAML files parsed and validated; behavior `AdaptiveBoss`, CPU device, batch/buffer values and max steps were checked.
- Static scene/config checks: behavior name, 12 observations, 4-action branch, default behavior type, training-off fields, script GUID references, unique Unity file IDs, RuleBased enum default, and absence of direct damage calls in the scripted policy passed.
- Unity batch-mode compilation was attempted with Unity 2021.3.15f1 using:

  ```powershell
  & 'C:\Program Files\Unity\Hub\Editor\2021.3.15f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'C:\Users\Pratik Kalloli\OneDrive\Desktop\Internship\Boss-Fight_Unity_Engine-main\BossFight' -logFile -
  ```

  The Editor reported Licensing Client IPC timeout (`0x8000000a`), `BatchMode: Unity has not been activated with a valid License`, and a Unity services request timeout (HTTP 0). It did not reach compilation and was interrupted after the license timeout. **Compilation is not verified.**
- No Unity Play Mode fight, reset loop, trainer connection, or PPO run was executed.

## Unity Editor checks still required

1. Reopen the project after resolving the Unity licensing issue and confirm script compilation, scene import, and serialized references.
2. Play the default scene: confirm keyboard movement/attacks, original rule-based boss behavior, damage/death, and R restart are unchanged.
3. Enable `BossRLAgent.trainingMode` before Play: confirm keyboard movement/attack no longer controls the player, the scripted player approaches, evades and attacks, and the agent alone directs the boss.
4. Run multiple complete episodes. Confirm one terminal CSV event per fight, fresh telemetry episodes, restored HP/positions/Animator/ragdoll/NavMesh state, and no post-terminal rows.
5. With the Agent active, stop Play Mode, reload the scene with R after a death, and disable the Agent during the death delay. Confirm no `go.IsActive()` assertion or observation-shape error.
6. Connect the smoke trainer on port 5004 and verify communicator startup, changing step count, reward/episode-length curves, and combat outcome metrics. Only then consider the longer run.
7. Import a real exported ONNX model and verify inference mode while checking that the rule-based baseline remains selectable.

## Remaining limitations

- Training and inference were not runtime-tested because Unity compilation could not be verified in this environment.
- The scripted opponent is deliberately simple and may not produce a balanced or representative player policy; tune it only after Play Mode verification.
- The Agent and policy still resolve some scene objects by names/tags. Renaming the scene objects would require updating those lookups or adding serialized references.
- Training outcomes depend on the NavMesh, Animator events, hitbox overlap, and in-place reset working correctly in Play Mode. No performance or win-rate improvement is claimed.
