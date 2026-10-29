# Stage 3.1 — ML-Agents Architecture Audit

**Project:** `BossFight`  
**Unity:** 2021.3.15f1 (`ProjectSettings/ProjectVersion.txt`)  
**Canonical scene:** `Assets/Scenes/KnightScene.unity` (enabled in `ProjectSettings/EditorBuildSettings.asset`)  
**Scope:** Read-only architecture and integration design. No ML-Agents package was installed, no existing source was changed, and no training or Unity runtime test was run. Existing user changes remain in the working tree.

## 1. Executive summary

The boss currently uses an Animator state machine and `NavMeshAgent`, with decisions made by `IdleState`, `PatrolState`, `ChaseState`, and `AttackState`. It is rule-based with a small profile-driven variation; it is not currently an ML agent. Combat execution is separate and can be retained: `EnemyCombat` accepts boss hitbox contacts, `CombatManager` owns HP and terminal death outcomes, and `EnemyMovement` connects attack animation events to the shared per-swing hit gate.

`KnightScene` references the Nightmare dragon prefab, its `Nightmare.controller` state machine, a baked NavMesh asset, `GameManager`, `DataLogger`, player, and UI. The package manifest/lockfile do not contain ML-Agents. No `Agent` subclass, `BehaviorParameters`, `DecisionRequester`, PPO YAML, or ONNX model was found. `AITraining/boss_model.pkl` belongs to the existing RandomForest next-action prototype and is not loaded by Unity.

The lowest-risk integration is an optional `BossAgent` plus a narrow action adapter, with the old controller selected by default. The agent should request movement/attack intents only. The existing `NavMeshAgent`, Animator clips/events, hitboxes, `EnemyCombat`, `CombatManager`, and HP/death path must remain authoritative. The existing state behaviours must stop issuing rule-based decisions in agent mode, or they will overwrite agent commands.

## 2. Actual project architecture and data flow

### Project structure

- `Assets/Scenes/KnightScene.unity`: playable scene, enabled in Build Settings.
- `Assets/Scenes/KnightScene/NavMesh.asset`: scene's serialized NavMesh data reference; scene YAML has a non-null `m_NavMeshData` GUID.
- `Assets/FourEvilDragonsHP/Prefab/DragonNightmare/Blue.prefab`: source of the active boss instance. Scene prefab overrides rename the root to `NightmareDragon` and tag it `Enemy`.
- `Assets/FourEvilDragonsHP/Animators/Nightmare.controller`: the scene's boss Animator controller override. Its parameters are `isPatrolling` (bool), `isChasing` (bool), `isAttacking` (bool), `attacks` (float), and `dead` (bool). The prefab's alternate `NightmareCTRL.controller` is not the controller selected by this scene override.
- `Assets/Scripts/`: Unity C# runtime scripts and Animator `StateMachineBehaviour` scripts.
- `AITraining/`: legacy Python/data pipeline (`train.py`, `pattern_recognition.py`, `player_profile.json`, `combat_logs.csv`, `boss_model.pkl`). No requirements/lock file for Python was found.
- `Packages/manifest.json` and `Packages/packages-lock.json`: Unity package declarations and resolved package lock.

### Runtime call paths (source-confirmed)

1. `CombatManager.Start()` finds scene objects by hard-coded names, initializes boss HP to 1500 and player HP to 500, and starts telemetry. `CombatManager` is the authoritative damage/death path.
2. Boss animation state transitions are selected by `Nightmare.controller` parameters. `IdleState` (3-second timer, chase range 8), `PatrolState` (waypoint selection, 3-second timer, chase range 12), and `ChaseState` (chase/retreat/attack range rules) write Animator booleans and/or NavMesh destinations.
3. `ChaseState.OnStateEnter` sets NavMesh speed to 20. The serialized boss agent in `KnightScene` has `speed=2.27`, `radius=2.98`, `stoppingDistance=3.71`; `PatrolState` sets speed to 1.5. Chase sets `isAttacking` between 1.5 and `stoppingDistance + 0.5` (4.21 at the serialized stopping distance); `AttackState` clears it outside `stoppingDistance + 0.75` (4.46).
4. When `AttackState` exits, it increments `CombatManager.FailedAttacks` if no hit registered, clears the state hit flag, restores boss Animator speed, and randomly sets `attacks` to 0, 0.5, or 1 according to `BossProfileReader.CurrentPlayerType`. `EnemyMovement.Update()` moves the boss root backward by 2 units after three failed attack states.
5. The `attacks` float is the blend parameter for three clips in the `AttackState` blend tree: 0 = Basic, 0.5 = Claw, 1 = Horn. The current code randomly chooses among them; the existing graph can represent a requested basic/claw/horn clip.
6. The Basic, Claw, and Horn clips have animation events calling `BasicAttackSlow`/`BasicAttackFast`/`Basic_fin`, `ClawAttackSlow`/`ClawAttackFast`/`Claw_fin`, and `HornAttackSlow`/`HornAttackFast`/`Horn_fin` on `EnemyMovement`. Each `*AttackSlow` starts a swing by incrementing `attackSwingId` and opening the shared hit gate; the `*_fin` event closes the gate. `EnemyCombat.OnTriggerEnter/Stay` validates contact and combat state, consumes `TryRegisterPlayerHitThisSwing()`, then calls `CombatManager.PlayerTakeDamage(swingId)` (95 damage). Thus the agent must not synthesize damage or bypass the attack clips/hitboxes.
7. Player attack acceptance is in `PlayerCombat.PlayerAttack()`: mouse input, valid life state, attack Animator not active/transitioning, and tracked qualifying boss collider are required. Once accepted, `PlayerCombat` triggers the existing animation and calls `CombatManager.DragonTakeDamage(sequenceId)` directly (75 damage). Player attack damage is not presently driven by an impact animation event.
8. `CombatManager` clamps HP and sets `dead` on boss death (terminal outcome `player_win`). Player death disables `PlayerMovement`; `RagDollManager.Update()` notices player HP <= 0 and activates the player ragdoll. Outcomes are logged through `DataLogger.LogEpisodeEnd`.
9. After death, `CombatManager.Update()` accepts `R` and reloads the active scene with `LoadSceneMode.Single`. This is a human restart path, not yet an ML-Agents in-place episode reset API.

### Existing player-profile adaptation (not PPO)

`pattern_recognition.py` classifies player behavior from CSV thresholds and writes `AITraining/player_profile.json`. `BossProfileReader.Awake()` loads that file and sets static `CurrentPlayerType`. `AttackState.ChooseDragonAttack()` restricts the random clip subset for Aggressive/Defensive profiles, while `EnemyMovement.trailDeactivate()` changes `dragonFastSpeed` and `chaseMultiplier` for selected profiles. This is file-based rule/profile adaptation; it does not load `boss_model.pkl` and does not learn through an Agent-Python PPO loop.

## 3. Important scripts and responsibilities

| Script | Verified responsibility | Integration relevance |
|---|---|---|
| `Assets/Scripts/CombatManager.cs` | Initializes HP, applies both damage directions, records outcomes/death, handles hit stop and `R` scene reload. | Keep authoritative. Add a narrow reset/event interface only when the training episode lifecycle is implemented. |
| `Assets/Scripts/EnemyMovement.cs` | Boss profile speed adaptation, facing, repositioning, swing ID/shared hit gate, animation-event methods, boss-death NavMesh stop. | Suitable place for mode/property and narrow request methods, but do not allow rule code to also control agent mode. |
| `Assets/Scripts/EnemyCombat.cs` | Boss hitbox callbacks; checks AttackState, life/finished conditions, consumes the shared swing gate, applies boss damage through `CombatManager`. | Keep combat validation and damage call unchanged. |
| `Assets/Scripts/AttackState.cs` | Attack exit range, failed-attack counter, profile-based random clip selection and state diagnostics. | Must not replace an agent-selected attack with random selection in Agent mode. |
| `Assets/Scripts/ChaseState.cs` | Continuous player destination, chase speed, close-range retreat point, attack-entry distance and chase exit. | Must be gated in Agent mode to avoid competing `SetDestination` and state parameter writes. |
| `Assets/Scripts/IdleState.cs` | Idle timer and chase/patrol state flags. | Must not auto-transition the agent-controlled boss in Agent mode. |
| `Assets/Scripts/PatrolState.cs` | Random waypoint destinations, patrol timer and chase transition. | Must not overwrite agent destinations in Agent mode. |
| `Assets/Scripts/BossProfileReader.cs` | Reads profile JSON and exposes `CurrentPlayerType`. | Preserve for baseline comparisons; decide whether agent mode ignores profile speed/attack selection. |
| `Assets/Scripts/PlayerCombat.cs`, `PlayerMovement.cs` | Player input, attack acceptance, movement and direct player damage request. | Keep player controls/mechanics. For unattended training, add a repeatable opponent driver rather than relying on manual keyboard/mouse input. |
| `Assets/Scripts/RagDollManager.cs` | Disables character animation/controller and enables ragdoll on player death. | Keep mechanics; reset logic will need an explicit reverse/reset operation. |
| `Assets/Scripts/DataLogger.cs` | One v2 CSV file per episode, 33-column schema, action/combat events and terminal outcome. | Keep v2 schema stable; route training reward/curve metrics to ML-Agents StatsRecorder or a separate sidecar, not new columns in v2. |
| `AITraining/train.py` | Fits `RandomForestClassifier` to predict the next categorical action using Action/MoveX/MoveY; dumps `boss_model.pkl`. | Not PPO and not called by Unity. Avoid as dependency for ML-Agents. |
| `AITraining/pattern_recognition.py` | Threshold-based player classification and JSON profile output. | Existing baseline adaptation; not an RL trainer. |

## 4. Stage 3.1 agent contract proposal

Use `CollectObservations(VectorSensor)` with a fixed observation order and count. Suggested first contract: **11 scalar observations**.

| Observation | Encoding | Notes |
|---|---|---|
| Player-to-boss distance | `clamp(distance / 20, 0, 1)` | 20 is the current ChaseState disengage distance; check scene layout/runtime before locking it. |
| Relative planar direction | boss-local right and forward components, each `[-1, 1]` | Horizontal direction from boss to player, not world position, keeps translation/rotation changes normalized. |
| Boss HP | `bossHP / 1500` | From `CombatManager.getDragonHP()`. |
| Player HP | `playerHP / 500` | From `CombatManager.getHealth()`. |
| Boss combat state | 5-value one-hot: idle, patrol, chase, attack, dead | Derive from current base-layer Animator state; define explicit mapping, including transition behavior. |
| Boss can attack | 1 boolean/scalar | True only if both alive, the Animator is not in an attack/transition that blocks a request, and the existing attack-entry range/adapter guard permits it. This is a request-validity observation, not permission to apply damage. |
| Boss dead | 1 boolean | `bossHP <= 0` (redundant with one-hot dead by design, because explicitly requested). |
| Player dead | 1 boolean | `playerHP <= 0`. |

Suggested discrete action space: **one branch of size 4**, with fixed action IDs:

| ID | High-level request | Adapter behavior |
|---:|---|---|
| 0 | Approach/reposition | Set a NavMesh destination toward the player using the existing boss agent and a target band; never teleport the Transform. |
| 1 | Maintain distance/retreat | Hold a safe standoff target or request one bounded retreat destination; no per-frame direct root movement. |
| 2 | Request Basic attack | Ask the adapter to select the Basic clip in the existing `attacks` blend tree and enter the existing attack state only when allowed. |
| 3 | Request special attack (initially Claw) | Claw clip and animation-event lifecycle are present. Include Horn later only after clip-event, hitbox and range verification; no direct damage. |

ML-Agents actions are *requests*. The adapter validates life/state/range and issues at most one intent per decision. Invalid requests should be masked when possible; any residual invalid-action penalty should be small and applied once per decision, not per frame. A slow, fixed decision cadence (initially around every 8–12 simulation steps) is more appropriate than requesting actions in every `Update`; calibrate against attack duration and hitstop.

### Bounded reward proposal

Rewards should be event-based and use actual HP deltas from `CombatManager`, not requested attacks or collider attempts:

- Boss damages player: `+0.10 * clamp(effectiveDamage / 95, 0, 1)` once per accepted damage application.
- Boss takes player damage: `-0.10 * clamp(effectiveDamage / 75, 0, 1)` once per accepted damage application. This is the damage-avoidance signal; do not give a positive reward every frame for merely staying alive.
- Terminal boss win (`boss_win`): `+1.0`; terminal boss loss (`player_win`): `-1.0`, exactly once.
- Completed ineffective swing (animation swing ended without accepted player damage): `-0.02` once per swing, not once per repeated action decision/contact callback.
- Invalid/unavailable action request: optional `-0.005` once per request; do not reward repeated identical actions or add an alive-time reward.

These are starting values to tune from reward curves; retain a terminal outcome floor/ceiling and cap/monitor cumulative per-episode shaping. Reward callbacks must observe authoritative damage/outcome events and must never call damage methods.

## 5. Integration boundaries and target architecture

```text
KnightScene
  NightmareDragon root
    BossControllerMode (RuleBased default / Agent)
    NavMeshAgent + existing Animator/Nightmare.controller
    EnemyMovement (motion/swing lifecycle + mode-aware request API)
    EnemyCombat on existing attack collider objects
    BossAgent (only in Agent mode; observations/rewards/discrete requests)
    BossActionAdapter (validate requests -> EnemyMovement/NavMesh/Animator intent)
  GameManager / CombatManager (authoritative HP, damage, terminal outcomes, episode reset)
  Player + existing PlayerCombat/PlayerMovement (unchanged mechanics; scripted opponent for headless training)
  DataLogger v2 unchanged + optional separate training-metrics stream
```

- Keep `RuleBased` as the serialized default/baseline. In that mode, state behaviours retain current behavior and no agent command executes.
- In `Agent` mode, only `BossActionAdapter`/mode-aware `EnemyMovement` may set movement destinations or action transition parameters. Gate the decision portions of `IdleState`, `PatrolState`, `ChaseState`, and random attack selection in `AttackState`; Animator state behaviours may still report state and run animation lifecycle code.
- The adapter requests existing high-level Animator transitions. The Animator plays the selected existing clip, animation events open/close the existing swing gate, collider callbacks validate hits, and `CombatManager` applies damage. Agent actions cannot write HP, invoke damage, or disable hitbox validation.
- Use the ML-Agents `Heuristic()` method and Behavior Parameters `Heuristic Only` mode to exercise the exact same action adapter before Python training. ML-Agents documents the observation/action/episode methods and heuristic-only path for this purpose.
- `OnEpisodeBegin()` must coordinate an **in-place deterministic fight reset** after a terminal event: HP, transforms/orientations, NavMesh path/speed/isStopped, animator parameters/speeds/transitions, swing gates/counters, ragdoll, colliders, hit-stop/timeScale, UI, profile mode and player opponent state. Current `R` scene reload is useful for human play but is not yet a stable per-Agent reset contract; unloading the Agent's scene object during reset is especially risky.
- Keep existing v2 telemetry columns unchanged. Use ML-Agents `StatsRecorder` for training reward/episode metrics, and optionally a new schema/version sidecar that records episode ID, selected action, authoritative damage, outcome and durations. Do not append rewards to current schema without a version change.

## 6. Readiness checklist (verified against current tree)

| Capability | Status | Evidence |
|---|---|---|
| Unity editor version | Present | `ProjectSettings/ProjectVersion.txt`: 2021.3.15f1. |
| Canonical scene enabled in Build Settings | Present | `EditorBuildSettings.asset` enables `Assets/Scenes/KnightScene.unity`. |
| Scene boss NavMeshAgent and NavMesh data | Serialized | KnightScene references `Scenes/KnightScene/NavMesh.asset`; boss instance has Agent Type 0, radius 2.98, speed 2.27, stopping distance 3.71. Runtime NavMesh coverage was not tested. |
| Existing rule-based boss controller | Present | Animator controller/state behaviour scripts listed above. |
| Combat HP/damage/death callbacks | Present | `CombatManager`; 75 player damage, 95 boss damage, 1500/500 HP. |
| ML-Agents Unity package | **Absent** | No `com.unity.ml-agents` entry in manifest or lockfile. |
| `Agent` subclass / `BehaviorParameters` / `DecisionRequester` | **Absent** | Repository source/asset search found none. |
| Observations/action space | **Absent** | No agent contract implemented in Unity. |
| Reward callback / `EndEpisode()` | **Absent** | Existing telemetry terminal event is not an ML-Agents reward or Agent episode. |
| In-place Agent episode reset | **Absent** | Existing reset is `R` -> `SceneManager.LoadScene(..., Single)`. |
| PPO trainer YAML | **Absent** | No ML-Agents trainer configuration file found. |
| ONNX policy | **Absent** | No `.onnx` model found. `boss_model.pkl` is a legacy scikit-learn artifact. |
| Python ML-Agents environment | **Absent/unverified** | Repo contains scripts but no Python requirements/lock; machine Python environment was not inspected or altered. |
| Repeatable autonomous player opponent | **Absent** | `PlayerMovement`/`PlayerCombat` read manual Unity input. Human tests are not enough for scalable training. |
| Existing telemetry | Partial | `DataLogger` v2 stores 33 columns and terminal outcomes in unique files; reward column is presently `na`, and state/event logging is not a PPO data bridge. |

## 7. Dependency compatibility recommendation (for a later approved stage)

Do **not** install current/latest ML-Agents blindly. Unity-Technologies' official Release 20 pairs `com.unity.ml-agents` **2.3.0-exp.3** with Python `mlagents`/`mlagents-envs` **0.30.0** and states minimum Unity **2021.3**. That is the best historically version-matched candidate for this 2021.3.15f1 project; it meets the stated minimum but is not a claim of compilation on this exact editor patch. Newer official releases raised their minimum Unity version beyond this project (Release 21 to 2022.3; Release 22 to 2023.2; Release 23/4.x to Unity 6), so do not use the latest Unity package with this editor. [Release 20 compatibility matrix and minimums](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_20) · [Release 20 installation instructions](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Installation.md) · [official versioning guidance](https://unity-technologies.github.io/ml-agents/Versioning/).

Recommended later setup, isolated from existing Python tooling:

1. Create a project-local virtual environment using **64-bit Python 3.8.13** as the conservative Release 20 baseline. The Release 20 trainer metadata allows Python 3.8.13–3.10.8; 3.8 also matches the legacy Windows/PyTorch installation notes.
2. In Unity Package Manager, enable Preview Packages if required and add `com.unity.ml-agents` at `2.3.0-exp.3`. Let Unity resolve/lock dependencies; review, do not hand-edit the lockfile. The optional `com.unity.ml-agents.extensions` package is not needed for the proposed vector/discrete agent.
3. In the venv, install a CPU-capable PyTorch build following the pinned Release 20 Windows instructions, then install `mlagents==0.30.0` (which depends on `mlagents_envs==0.30.0`). The release installation guide's Windows command is `pip install torch~=1.7.1 -f https://download.pytorch.org/whl/torch_stable.html`; execute it only after verifying the required wheel exists for the selected 64-bit Python. Then run `python -m pip install mlagents==0.30.0` and verify with `mlagents-learn --help` plus version inspection. Release documentation says Windows needs PyTorch installed separately. Do not mix packages from different ML-Agents release lines.
4. Set trainer `torch_settings.device: cpu` for CPU-only training. Start with one Unity environment, small vector-only network and discrete action branch; CPU training speed will be limited by scene animation/physics/decision frequency. Release 20 documents this CPU setting and PPO YAML structure. [Release 20 trainer configuration](https://github.com/Unity-Technologies/ml-agents/blob/release_20/ml-agents/setup.py) · [CPU and PPO trainer YAML options](https://github.com/Unity-Technologies/ml-agents/blob/release_20/docs/Training-ML-Agents.md) · [Agent observations/actions/heuristics](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Learning-Environment-Design-Agents.md).

Exact legacy PyTorch wheel availability on the target Windows/Python install and exact 2021.3.15f1 package resolution remain to be confirmed in Stage 3.2 **before** dependency changes. Do not copy a current guide's Python/PyTorch pins into this older release.

## 8. Exact proposed Stage 3.2 file set

No files from this list were modified in Stage 3.1. Implement in small reviewable commits/steps:

| File | Stage 3.2 change proposal |
|---|---|
| `Packages/manifest.json` | Add only verified `com.unity.ml-agents` version `2.3.0-exp.3`; resolve lock through Unity Package Manager, not manual edits. |
| `Packages/packages-lock.json` | Unity-generated resolution for the package and its required runtime dependencies. |
| `Assets/Scripts/BossAgent.cs` *(new)* | Agent observations, action decoding, event-based reward application, heuristic policy, episode callback. No direct combat/movement implementation. |
| `Assets/Scripts/BossActionAdapter.cs` *(new)* | Validate/deconflict requests and map them to approved high-level `EnemyMovement` APIs. No HP/damage writes. |
| `Assets/Scripts/EnemyMovement.cs` | Add selectable control mode/request API and gate baseline-only reposition/movement logic when agent mode is active. Keep baseline default and swing/animation methods. |
| `Assets/Scripts/IdleState.cs` | Gate baseline timer/transitions in agent mode. |
| `Assets/Scripts/PatrolState.cs` | Gate baseline waypoint/timer/transitions in agent mode. |
| `Assets/Scripts/ChaseState.cs` | Gate baseline `SetDestination`, 20 speed, retreat and attack-distance decisions in agent mode. |
| `Assets/Scripts/AttackState.cs` | Preserve animation lifecycle/exit cleanup, but do not randomly overwrite agent-selected attack type or increment baseline-only miss decisions in agent mode. |
| `Assets/Scripts/CombatManager.cs` | Add explicit in-place episode reset interface and authoritative damage/terminal events for rewards, retaining existing HP/death implementation and R behavior for baseline. |
| `Assets/Scripts/RagDollManager.cs` | Expose/implement a deterministic ragdoll-off reset path used only by the episode reset coordinator. |
| `Assets/Scripts/PlayerMovement.cs`, `PlayerCombat.cs` | Only if needed for a scripted/replay opponent and clearing input/animation state at reset; do not change player controls/mechanics. Prefer adding a separate `TrainingPlayerDriver.cs` (new) if integration can stay isolated. |
| `Assets/Scenes/KnightScene.unity` | Add `BossAgent`, `BehaviorParameters`, `DecisionRequester`, adapter references/mode selector after script compile; keep baseline selected by default. Preserve existing references. |
| `Assets/Configs/BossPPO.yaml` *(new; later training stage if review separates it)* | Release 20-compatible PPO config with vector/discrete behavior, `torch_settings.device: cpu`, modest network/step sizes. |
| `Assets/Scripts/TrainingMetricsLogger.cs` *(optional new)* | Separate reward/action/episode metrics if StatsRecorder plus existing telemetry cannot provide evaluation needs. Do not alter v2 CSV schema. |

The key compatibility seam is the mode gate around the state behaviours. Adding `BossAgent` alone is unsafe: `ChaseState.OnStateUpdate` would continue calling `SetDestination` every frame and `AttackState.OnStateExit` would choose a random clip, overriding policy actions.

## 9. Risks, assumptions and unresolved questions

### Source-confirmed risks

- **Movement authority conflict:** Chase, patrol, root-motion Animator, root NavMeshAgent, and `EnemyMovement` direct Transform retreat all affect motion. The scene's Animator prefab override has `m_ApplyRootMotion: 1`; the NavMeshAgent is also active. An ML adapter must be the only command authority in agent mode. Verify actual root-motion/agent motion in Play Mode before training.
- **Action overwrite:** `AttackState.OnStateExit` always randomizes `attacks`; baseline StateMachineBehaviours currently do not know about a separate policy mode.
- **Timing:** attacks are animation-event-driven and boss successful damage speed changes Animator speed. Action intervals and masks must avoid requests during attack/recovery and keep one decision-to-swing association.
- **Reward integrity:** reward must key off HP before/after in `CombatManager`, not `EnemyCombat` contact attempts or player attack input attempts.
- **Reset complexity:** full reset includes `RagDollManager` toggles, character colliders/rigidbodies, NavMesh path and speed, Animator bool/float/speed, swing gate/IDs/failed count/enrage flags, `Time.timeScale`/hit-stop coroutine, UI and the profile. Scene reload is currently the only top-level reset API.
- **Training opponent:** current player requires keyboard/mouse. A scripted opponent or deterministic replay is needed for autonomous episodes and fair baseline comparisons.
- **Profile confounding:** `player_profile.json` changes baseline attack choice/speed/chase multiplier. Use the same fixed profile for paired evaluation, or disable profile variability in evaluation config.
- **Telemetry separation:** current v2 schema has reward column but no PPO reward writers, and it must not be silently widened.

### Assumptions / items not verifiable from static source

- The scene YAML references a baked NavMesh asset and boss Agent, but runtime NavMesh coverage, path validity, avoidance, root motion and actual attack-entry distance were not exercised.
- Unity 2021.3.15f1 is above Release 20's stated 2021.3 minimum, but Package Manager compile compatibility on this exact patch has not been tested.
- The 4-action contract's Claw request is supported by existing clip events and blend-tree threshold, but the runtime event receiver/hitbox reach for each clip should be confirmed in Play Mode. Horn should be deferred until separately verified.
- Team expectations around keeping the baseline player-profile adaptation enabled during training versus only for the baseline are unresolved. For an unbiased A/B result, freeze it identically in both arms.
- No Unity compilation, Play Mode, Python environment check, dependency installation or policy inference was performed for this audit.

## 10. Phased plan and verification

1. **3.2a — Dependency/compile gate:** create isolated Python venv; add only release-matched package; resolve package lock in Unity 2021.3.15f1; verify package/CLI versions and compile before adding an Agent. Keep current scene/controller playable.
2. **3.2b — Mode gate and deterministic reset:** add mode enum defaulting to RuleBased, implement agent-mode state-behaviour gates and a complete in-place `CombatManager` reset. Verify baseline against existing behavior and repeated reset correctness first.
3. **3.2c — Adapter + heuristic only:** add `BossAgent`/adapter, component settings and observations/actions. Exercise heuristic actions in Editor; assert every decision maps to at most one action request, all damage still passes through `EnemyCombat`/`CombatManager`, and mode switch back to RuleBased restores the baseline.
4. **3.2d — Rewards/telemetry:** subscribe to authoritative HP delta/outcome callbacks; verify reward once per damage/terminal event, no reward for alive time or repeated input; log metrics separately from v2 CSV.
5. **3.3 — PPO experiment:** add one CPU trainer config and run small smoke training; verify model export/import and deterministic inference. Preserve a clean baseline build and paired evaluation protocol.

### Verification before accepting an Agent-ready branch

- Compile project in the exact Unity editor version with no new warnings/errors; run KnightScene before and after switching modes.
- Exercise scripted heuristic sequence: approach, retreat/hold, basic attack, Claw; ensure no attack when dead/invalid, one swing ID per clip, hitbox gate still limits to one accepted hit/swing, and only CombatManager changes HP.
- Run repeated deterministic reset from each terminal state; verify player/boss spawn, HP, Animator state/parameters, NavMesh path/speed/isStopped, swing gate, attack IDs/miss counters/enrage flags, ragdoll colliders/rigidbodies, UI, time scale and new telemetry ID.
- Confirm agent rewards only respond to actual HP deltas/outcome and are recorded once; compare mode toggle to original rule baseline.
- For paired evaluation, hold player script/seed/profile/start transforms/timing constant across RuleBased and Agent episodes; compare win rate, boss/player damage, episode duration, hit/miss rate, action frequencies and reward trend over repeated seeds.

## Sources

- Project facts: current source files and YAML assets under `Assets/`, `Packages/manifest.json`, `Packages/packages-lock.json`, and `ProjectSettings/`.
- ML-Agents compatibility/version pairs: [Unity ML-Agents Release 20](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_20).
- Release 20 install/version pins: [Installation guide](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Installation.md), [Unity package metadata and dependency on Barracuda 3.0.0](https://github.com/Unity-Technologies/ml-agents/blob/release_20/com.unity.ml-agents/package.json), [Python trainer setup metadata](https://github.com/Unity-Technologies/ml-agents/blob/release_20/ml-agents/setup.py).
- CPU and trainer settings: [Release 20 training CLI/config guide](https://github.com/Unity-Technologies/ml-agents/blob/release_20/docs/Training-ML-Agents.md), [trainer configuration reference](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Training-Configuration-File.md).
- Agent design/heuristic mode: [Release 20 agent environment design](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Learning-Environment-Design-Agents.md).
