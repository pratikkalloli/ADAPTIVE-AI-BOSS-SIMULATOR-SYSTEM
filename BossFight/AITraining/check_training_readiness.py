#!/usr/bin/env python
"""Read-only, lightweight readiness checks for the AdaptiveBoss ML-Agents setup."""

from __future__ import annotations

import importlib
import importlib.metadata
import os
import re
import shutil
import socket
import sys
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
DOCUMENTED_PROJECT_PATH = Path(
    r"C:\Users\Pratik Kalloli\OneDrive\Desktop\Internship\Boss-Fight_Unity_Engine-main\BossFight"
)
EXPECTED_FILES = (
    "Assets/Scenes/KnightScene.unity",
    "Assets/Scripts/BossAgent.cs",
    "Assets/Scripts/EnemyMovement.cs",
    "Assets/Scripts/CombatManager.cs",
    "AITraining/BossPPO.smoke.yaml",
    "AITraining/BossPPO.yaml",
    "AITraining/README.md",
    "STAGE4_TRAINING_READINESS_REPORT.md",
)


class Report:
    def __init__(self) -> None:
        self.results: list[tuple[str, str]] = []

    def add(self, status: str, message: str) -> None:
        self.results.append((status, message))
        print("{:<4} {}".format(status, message))

    def finish(self) -> int:
        counts = {key: sum(1 for status, _ in self.results if status == key)
                  for key in ("PASS", "WARN", "FAIL")}
        print("\nSummary: {PASS} PASS, {WARN} WARN, {FAIL} FAIL".format(**counts))
        print("Static readiness only; this report does not establish Unity runtime readiness.")
        return 1 if counts["FAIL"] else 0


def check_runtime(report: Report) -> None:
    version = "{}.{}.{}".format(*sys.version_info[:3])
    report.add("PASS", "Active Python {} at {}".format(version, sys.executable))
    if sys.version_info[:2] == (3, 9):
        report.add("PASS", "Python major/minor matches the documented 3.9 environment")
    else:
        report.add("WARN", "Project setup documents Python 3.9; verify compatibility for this interpreter")
    if ".venv-mlagents" in str(Path(sys.prefix)).lower():
        report.add("PASS", "Active interpreter is inside .venv-mlagents")
    else:
        report.add("WARN", "Active interpreter is not inside the project .venv-mlagents")

    packages = (
        ("mlagents", "mlagents"),
        ("mlagents_envs", "mlagents-envs"),
        ("torch", "torch"),
        ("tensorboard", "tensorboard"),
    )
    expected_versions = {
        "mlagents": "0.30.0",
        "mlagents-envs": "0.30.0",
        "torch": "1.11.0",
    }
    for module_name, distribution_name in packages:
        try:
            importlib.import_module(module_name)
            package_version = importlib.metadata.version(distribution_name)
            expected = expected_versions.get(distribution_name)
            if expected is not None and package_version != expected:
                report.add(
                    "WARN",
                    "{} available ({}) but the documented setup uses {}".format(
                        distribution_name, package_version, expected
                    ),
                )
            else:
                report.add("PASS", "{} available ({})".format(distribution_name, package_version))
        except Exception as exc:  # Dependency imports can fail because of native DLLs too.
            report.add("FAIL", "{} unavailable: {}".format(distribution_name, exc))

    try:
        torch = importlib.import_module("torch")
        report.add(
            "PASS",
            "PyTorch import works ({}; CUDA available: {})".format(
                torch.__version__, torch.cuda.is_available()
            ),
        )
    except Exception as exc:
        report.add("FAIL", "PyTorch runtime check failed: {}".format(exc))

    trainer_cli = "mlagents-learn.exe" if os.name == "nt" else "mlagents-learn"
    cli_in_venv = Path(sys.prefix) / ("Scripts" if os.name == "nt" else "bin") / trainer_cli
    if cli_in_venv.is_file():
        report.add("PASS", "mlagents-learn CLI is installed in the active environment")
    elif shutil.which("mlagents-learn"):
        report.add("PASS", "mlagents-learn CLI is available on PATH")
    else:
        report.add("WARN", "mlagents-learn CLI not found; reinstall/check the active ML-Agents environment")


def check_project_files(report: Report) -> None:
    expected = os.path.normcase(os.path.normpath(str(DOCUMENTED_PROJECT_PATH)))
    actual = os.path.normcase(os.path.normpath(str(PROJECT_ROOT)))
    if actual == expected:
        report.add("PASS", "Project root matches the documented Windows path")
    else:
        report.add(
            "WARN",
            "Project is at {}; documented path is {}".format(PROJECT_ROOT, DOCUMENTED_PROJECT_PATH),
        )

    missing = [relative for relative in EXPECTED_FILES if not (PROJECT_ROOT / relative).is_file()]
    if missing:
        report.add("FAIL", "Expected project files missing: {}".format(", ".join(missing)))
    else:
        report.add("PASS", "Expected scene, scripts, PPO configs, README, and Stage 4 report exist")


def parse_trainer_configs(report: Report) -> None:
    try:
        from mlagents.plugins.trainer_type import register_trainer_plugins
        from mlagents.trainers.cli_utils import load_config
        from mlagents.trainers.settings import RunOptions

        register_trainer_plugins()
    except Exception as exc:
        report.add("FAIL", "Could not initialize installed ML-Agents config parser: {}".format(exc))
        return

    for relative in ("AITraining/BossPPO.smoke.yaml", "AITraining/BossPPO.yaml"):
        path = PROJECT_ROOT / relative
        try:
            raw = load_config(str(path))
            options = RunOptions.from_dict(raw)
            behavior_names = list(options.behaviors.keys())
            if behavior_names != ["AdaptiveBoss"]:
                raise ValueError("expected only behavior AdaptiveBoss, got {}".format(behavior_names))
            if options.torch_settings.device != "cpu":
                raise ValueError("expected CPU device, got {}".format(options.torch_settings.device))
            report.add("PASS", "{} parsed and validated (AdaptiveBoss, CPU)".format(relative))
        except Exception as exc:
            report.add("FAIL", "{} failed ML-Agents validation: {}".format(relative, exc))


def check_scene_contract(report: Report) -> None:
    scene_path = PROJECT_ROOT / "Assets/Scenes/KnightScene.unity"
    readme_path = PROJECT_ROOT / "AITraining/README.md"
    agent_path = PROJECT_ROOT / "Assets/Scripts/BossAgent.cs"
    try:
        scene = scene_path.read_text(encoding="utf-8", errors="replace")
        readme = readme_path.read_text(encoding="utf-8", errors="replace")
        agent = agent_path.read_text(encoding="utf-8", errors="replace")

        behavior = re.findall(r"^\s*m_BehaviorName:\s*(\S+)\s*$", scene, re.MULTILINE)
        observations = re.findall(r"^\s*VectorObservationSize:\s*(\d+)\s*$", scene, re.MULTILINE)
        branches = re.findall(
            r"m_ActionSpec:\s*\n(?:[^\n]*\n)*?\s*BranchSizes:\s*\n\s*-\s*(\d+)",
            scene,
        )
        periods = re.findall(r"^\s*DecisionPeriod:\s*(\d+)\s*$", scene, re.MULTILINE)
        expected_values = (
            len(behavior) == 1 and behavior[0] == "AdaptiveBoss",
            len(observations) == 1 and int(observations[0]) == 12,
            len(branches) == 1 and int(branches[0]) == 4,
            len(periods) == 1 and int(periods[0]) == 10,
        )
        docs_match = all(
            text in readme
            for text in (
                "behavior name `AdaptiveBoss`",
                "12 vector observations",
                "one discrete action branch of size 4",
                "period 10",
            )
        )
        observation_method = re.search(
            r"public override void CollectObservations\(VectorSensor sensor\)(.*?)"
            r"private static void AddEmptyObservations",
            agent,
            re.DOTALL,
        )
        source_observation_count = (
            len(re.findall(r"sensor\.AddObservation\s*\(", observation_method.group(1)))
            if observation_method
            else -1
        )
        if all(expected_values) and docs_match and source_observation_count == 12:
            report.add(
                "PASS",
                "STATIC scene/docs/source contract matches: AdaptiveBoss, 12 observations, "
                "one branch of 4, DecisionPeriod 10 (not live Unity validation)",
            )
        else:
            report.add(
                "FAIL",
                "STATIC contract mismatch: behavior={}, observations={}, branches={}, "
                "periods={}, source observation calls={}, docs_match={}".format(
                    behavior, observations, branches, periods, source_observation_count, docs_match
                ),
            )
    except Exception as exc:
        report.add("FAIL", "Could not inspect documented scene contract: {}".format(exc))


def check_port(report: Report) -> None:
    readme = (PROJECT_ROOT / "AITraining/README.md").read_text(encoding="utf-8", errors="replace")
    ports = set(re.findall(r"--base-port\s+(\d+)", readme))
    if len(ports) != 1:
        report.add("WARN", "Could not determine one documented trainer port; defaulting to 5004")
        port = 5004
    else:
        port = int(next(iter(ports)))
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=0.5):
            report.add(
                "WARN",
                "TCP port {} accepts a local connection; this does NOT verify a Unity communicator connection".format(port),
            )
    except OSError:
        report.add(
            "WARN",
            "No listener detected on TCP port {}; start the trainer before connecting Unity".format(port),
        )


def check_artifacts(report: Report) -> None:
    model_roots = (PROJECT_ROOT / "Assets/ML-Agents", PROJECT_ROOT / "AITraining")
    models = sorted(
        path for root in model_roots if root.exists() for path in root.rglob("*.onnx") if path.is_file()
    )
    if models:
        report.add("PASS", "ONNX artifacts found: {}".format(", ".join(str(p.relative_to(PROJECT_ROOT)) for p in models)))
    else:
        report.add("WARN", "No ONNX model artifacts found; inference requires a trained/exported model")

    results_root = PROJECT_ROOT / "AITraining/Results"
    if not results_root.is_dir():
        report.add("WARN", "Training results directory does not exist yet: AITraining/Results")
        return
    runs = sorted(path.name for path in results_root.iterdir() if path.is_dir())
    if runs:
        report.add("PASS", "Training result directories found: {}".format(", ".join(runs)))
    else:
        report.add("WARN", "AITraining/Results exists but contains no run directories")


def main() -> int:
    print("AdaptiveBoss training readiness diagnostic")
    print("Read-only checks; scene/config contract checks are static, not Unity runtime validation.\n")
    report = Report()
    check_runtime(report)
    check_project_files(report)
    parse_trainer_configs(report)
    check_scene_contract(report)
    check_port(report)
    check_artifacts(report)
    return report.finish()


if __name__ == "__main__":
    raise SystemExit(main())
