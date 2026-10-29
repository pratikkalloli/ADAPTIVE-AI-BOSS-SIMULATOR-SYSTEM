# AdaptiveBoss training and inference

This guide is for the existing Unity 2021.3.15f1 project, Unity ML-Agents package `2.3.0-exp.3` (Barracuda `3.0.0`), and the project-local Python environment with `mlagents==0.30.0`, `mlagents-envs==0.30.0`, and CPU PyTorch `1.11.0+cpu`.

## Verified scene contract

`Assets/Scenes/KnightScene.unity` contains an enabled `BossAgent` with behavior name `AdaptiveBoss`, 12 vector observations, one discrete action branch of size 4 (Approach, Retreat, BasicAttack, Hold), and a `DecisionRequester` with period 10. Its scene `controlMode` is `RuleBased`; the scripted player is disabled, so normal play remains the rule-based boss and keyboard player. The Behavior Parameters model slot is empty and Behavior Type is `Default`. No trained ONNX model is present in this project.

Select the `BossAgent.controlMode` explicitly before entering Play Mode:

- `RuleBased`: original boss state machine; scripted player off; keyboard player.
- `Training`: ML-Agents boss; scripted player on. Use Behavior Type `Default` for a connected trainer or `HeuristicOnly` for interface testing.
- `Inference`: ML-Agents boss; scripted player off; keyboard player. Requires a trained model assigned in Behavior Parameters; `BossAgent` switches its Behavior Type to `InferenceOnly`. If the model slot is empty, initialization reports an error and keeps the boss in RuleBased mode.

The `--boss-training` command-line argument selects `Training` for a standalone build. Trainer arguments do not automatically reach the Unity Editor, so select `Training` in the `BossRLAgent` Inspector for Editor training.

Episodes end on the `CombatManager.FightEnded` event after either combatant reaches zero HP. `BossAgent` guards terminal reward/stat updates against duplicates and preserves the 1.5-second death presentation before calling `EndEpisode()`. Training and inference agent episodes also have a 1,200-decision timeout; timeout records final HP and a `timeout` outcome, applies one `-0.1` reward, then ends on the following frame before the existing in-place reset. The trainer's `max_steps` remains a separate training-wide step budget.

## 1. Activate and check the Python environment

Run these commands in a new Windows PowerShell window from the Unity project root:

```powershell
Set-Location 'C:\Users\Pratik Kalloli\OneDrive\Desktop\Internship\Boss-Fight_Unity_Engine-main\BossFight'
& .\.venv-mlagents\Scripts\Activate.ps1
python --version
python -m pip check
```

Run the read-only readiness diagnostic from the same activated environment:

```powershell
python .\AITraining\check_training_readiness.py
```

The script validates installed Python dependencies and PPO configs, checks the documented scene contract against the serialized scene and Agent source, checks expected project paths, probes the documented trainer port, and reports existing ONNX/results artifacts. A listening port only means a TCP listener answered; it does not prove a Unity environment connected.

Expected checks from the current environment: Python `3.9.13` and `No broken requirements found`. Python 3.9 is now end-of-life; this guide retains it because it is the environment already verified with the project's ML-Agents 0.30.0 setup. Do not upgrade it as part of a training run without a separate dependency audit.

## 2. Run a short CPU smoke connection from the Unity Editor

Start a fresh smoke run ID, then launch the trainer in PowerShell:

```powershell
$RunId = 'adaptiveboss-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
mlagents-learn .\AITraining\BossPPO.smoke.yaml --run-id $RunId --results-dir .\AITraining\Results --base-port 5004
```

The trainer waits for an environment connection. In the Unity Editor, open `Assets/Scenes/KnightScene.unity`, select `BossRLAgent`, set Control Mode to `Training`, verify Behavior Type is `Default`, and press Play. Do not save the Inspector change if the checked-in scene default should remain baseline. The smoke config is CPU-only and limited to 2,000 trainer steps; it is a connection/configuration smoke test, not evidence that the policy learned useful combat.

If the Editor cannot compile or enter Play Mode, stop the waiting trainer with Ctrl+C. A trainer process waiting for Unity is not a successful connection test.

## 3. Run against a standalone Unity build (optional)

First create a standalone build that includes `KnightScene.unity`, then set `$Environment` to its actual executable path. In the same activated PowerShell environment:

```powershell
$Environment = '.\Builds\KnightTraining\BossFight.exe'
$RunId = 'adaptiveboss-build-smoke-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
mlagents-learn .\AITraining\BossPPO.smoke.yaml --run-id $RunId --results-dir .\AITraining\Results --base-port 5004 --env $Environment --no-graphics --env-args --boss-training
```

The example executable path is a placeholder and must exist before running. ML-Agents 0.30.0's `--env-args` option forwards trailing arguments to the Unity executable; `BossAgent` reads `--boss-training` from those arguments. Keep `--env-args` last. Do not use this command to launch the Editor.

## 4. Longer training

Only after the smoke environment connects and completes several episodes, use a new run ID with the longer CPU config:

```powershell
$RunId = 'adaptiveboss-ppo-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
mlagents-learn .\AITraining\BossPPO.yaml --run-id $RunId --results-dir .\AITraining\Results --base-port 5004
```

For Editor training, repeat the Inspector steps in section 2. For a standalone build, append the same `--env $Environment --no-graphics --env-args --boss-training` options from section 3. No long run is started by this guide. Do not reuse a run ID unless intentionally resuming with `--resume`; ML-Agents refuses an existing run directory unless `--resume` or `--force` is specified. Avoid `--force` when preserving prior results matters.

## 5. Monitor and inspect results

Trainer progress and connection status appear in the trainer console. ML-Agents writes each run beneath `AITraining\Results\<run-id>`, with behavior artifacts beneath `AdaptiveBoss`. In a second activated PowerShell window, TensorBoard can be started with:

```powershell
tensorboard --logdir .\AITraining\Results --port 6006
```

Open `http://localhost:6006` in a browser. Check episode reward, episode length, and the custom `Combat/BossWin`, `Combat/PlayerWin`, and `Combat/EpisodeLength` statistics. Compare multiple completed runs and baseline matches; a falling or rising reward alone is not proof of improved play.

## 6. Use a trained model for Unity inference

After a run finishes cleanly, inspect `AITraining\Results\<run-id>\AdaptiveBoss\` for the final `AdaptiveBoss.onnx`. ML-Agents 0.30.0 saves periodic files named like `AdaptiveBoss-<step>.onnx`; its final-model save copies the final policy to `AdaptiveBoss.onnx`. A run interrupted before its final save may have checkpoints but no final model. This repository currently has no trained ONNX checkpoint.

Copy the chosen final ONNX into `Assets/ML-Agents/Models/` and let Unity import it. In the `BossRLAgent` Inspector, assign the imported model asset to the Behavior Parameters **Model** slot and retain behavior name `AdaptiveBoss`. Set `BossAgent.controlMode` to `Inference`; initialization selects the ML-Agents `InferenceOnly` behavior using that assigned model and disables scripted player control, leaving the player on keyboard input. The ML-Agents 2.3.0-exp.3 `BehaviorParameters.Model` slot is the supported Unity inference path. Select `RuleBased` to return to the original baseline. No model asset is assigned or fabricated here; verify inference in Play Mode after importing a real exported checkpoint.

## Verification status and limits

The two YAML files were parsed and validated by ML-Agents 0.30.0. The local Python `pip check` and TensorBoard CLI checks pass. Package lock resolution records ML-Agents `2.3.0-exp.3` and Barracuda `3.0.0`. Unity compilation, Editor Play Mode, trainer-to-Unity connection, complete episodes, model export, and inference were not verified in this pass. The existing Stage 4 report records a prior Unity batch attempt blocked before compilation by the Unity licensing client. Resolve that Editor licensing/compilation blocker and perform the smoke connection before starting longer training.

The readiness diagnostic cannot verify Unity licensing, C# compilation in the Editor, Play Mode behavior, a successful ML-Agents environment/communicator connection, actual learning, or model quality. Its scene and observation/action checks are static comparisons of files only. It does not start a trainer, create result directories, or run training. `Inference` remains unavailable until an actual trained ONNX is imported and assigned to the Behavior Parameters Model slot.
