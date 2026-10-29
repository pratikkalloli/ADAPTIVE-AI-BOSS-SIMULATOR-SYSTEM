# Stage 3.2 — ML-Agents Integration Foundation: Compatibility Gate

## Outcome

Implementation is **blocked at the dependency compatibility gate**. No source, scene, package, Python, or model files were changed. In particular, `BossAgent.cs` was not added because it would reference an ML-Agents API that is not installed and the matching trainer runtime is unavailable. Proceeding without first establishing that version-matched toolchain would leave the integration unbuildable or encourage an unsupported package mix.

## Verified project and environment state

- `ProjectSettings/ProjectVersion.txt` identifies Unity **2021.3.15f1**.
- `Packages/manifest.json` and `Packages/packages-lock.json` contain no `com.unity.ml-agents`, Barracuda, or ML-Agents entries.
- No existing `Agent` implementation is present; the Stage 3.1 audit identifies the boss's rule-based state behaviours as the current controller.
- The discovered Python executables are 64-bit **3.11.9** and **3.14.0**. `py -0p` did not enumerate an installed interpreter. The local Python installation directory contains only Python 3.11.
- `pip show` reports `mlagents`, `mlagents-envs`, and `torch` are not installed for either discovered interpreter.
- The existing working-tree changes were inspected and preserved. No files were staged, reverted, or committed.

## Candidate compatible version set (not installed)

The Stage 3.1 audit's historical candidate is ML-Agents Release 20:

- Unity package: `com.unity.ml-agents` **2.3.0-exp.3**; its package metadata declares Unity **2021.3** support and depends on Barracuda **3.0.0**.
- Python packages: `mlagents==0.30.0` and matching `mlagents-envs==0.30.0`.
- Release 20 trainer metadata supports Python **3.8.13 through 3.10.8**. Neither installed Python 3.11.9 nor 3.14.0 is in that range.
- The Release 20 Windows installation guide specifies `torch~=1.7.1` as a separate installation step. Before using it, verify a compatible wheel for the chosen 64-bit Python and CPU; the project currently has no PyTorch installation.

This is a release-matched candidate, not a claim that Unity compilation has been verified on editor patch 2021.3.15f1. Do not install the latest ML-Agents release into this project: later release lines raise their minimum Unity version.

### Exact setup to perform after the compatibility prerequisite is addressed

1. Install a separate 64-bit **Python 3.8.13** interpreter (the conservative baseline used by the Stage 3.1 audit) and create a project-local virtual environment with that interpreter. Do not use the machine's Python 3.11/3.14 environments for Release 20.
2. Confirm a Windows CPU wheel is available for the documented `torch~=1.7.1` requirement. Install it in that isolated environment only after verifying wheel/Python compatibility.
3. Install `mlagents==0.30.0` in the same environment and verify `mlagents`, `mlagents_envs`, and `mlagents-learn --help` report the expected release.
4. In Unity Package Manager, add `com.unity.ml-agents` **2.3.0-exp.3** (enable preview packages if necessary); let Unity resolve `packages-lock.json` and verify the resolved Barracuda dependency is **3.0.0**. Do not hand-edit the lockfile.
5. Compile the project in Unity 2021.3.15f1 before adding integration code. For CPU training later, configure the Release 20 trainer with `torch_settings.device: cpu`.

No installation commands were executed because the required supported Python interpreter is absent and wheel availability has not been verified for this machine.

## Integration changes deferred

The Stage 3.1 architecture audit documents that `IdleState`, `PatrolState`, `ChaseState`, and `AttackState` issue rule-based transitions, movement, or random attack selection. Adding an agent without gating those commands would create competing controllers. A safe foundation therefore needs a reviewed controller-mode boundary and adapter in addition to `BossAgent`; it should retain `RuleBased` as the default and preserve the existing hitbox/`EnemyCombat`/`CombatManager` damage path. A deterministic in-place episode reset is also not present; the current `R` restart reloads the scene. Those changes are not attempted in this compatibility-blocked stage.

The candidate observation/action/reward proposal and the source list for the next implementation step remain in [STAGE3_1_MLAGENTS_ARCHITECTURE_AUDIT.md](STAGE3_1_MLAGENTS_ARCHITECTURE_AUDIT.md). No PPO configuration, reward logic, episode reset, or heuristic policy was added.

## Checks performed

- Read-only inspection of Stage 3.1 audit, Unity version, package manifest and lockfile.
- Read-only `git status --short`; pre-existing changes remained untouched.
- Interpreter discovery and version/bitness checks for the two discovered Python executables.
- `pip show mlagents mlagents-envs torch` against both executables (all three packages absent).
- Search of the package manifest and lockfile for ML-Agents/Barracuda references (none found).
- No Unity compilation, Play Mode run, package installation, training, or automated tests were performed.

## References

- [Unity ML-Agents Release 20](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_20)
- [Release 20 installation guide](https://github.com/Unity-Technologies/ml-agents/blob/release_20_docs/docs/Installation.md)
- [Release 20 Unity package metadata](https://github.com/Unity-Technologies/ml-agents/blob/release_20/com.unity.ml-agents/package.json)
- [Release 20 Python trainer metadata](https://github.com/Unity-Technologies/ml-agents/blob/release_20/ml-agents/setup.py)
