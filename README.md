# ⚔️ Adaptive AI Boss Simulator

### An Experimental Reinforcement Learning Approach to Adaptive Enemy AI in Unity

[![Unity](https://img.shields.io/badge/Unity-2021.3-black?logo=unity)](https://unity.com/)
[![C#](https://img.shields.io/badge/Language-C%23-68217A?logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![Python](https://img.shields.io/badge/Python-AI%20Training-3776AB?logo=python)](https://www.python.org/)
[![ML-Agents](https://img.shields.io/badge/Unity-ML--Agents-blue)](https://github.com/Unity-Technologies/ml-agents)
[![Reinforcement Learning](https://img.shields.io/badge/AI-PPO%20Reinforcement%20Learning-orange)](https://github.com/Unity-Technologies/ml-agents)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**Adaptive AI Boss Simulator** is a Unity-based combat game developed as a three-member team project. It explores how reinforcement learning can be integrated into a boss-fight environment to investigate adaptive enemy decision-making.

The project extends a traditional game AI architecture with a **Proximal Policy Optimization (PPO) training pipeline using Unity ML-Agents**. It includes a configurable boss agent, a scripted training opponent, training configurations, and an ONNX model export workflow.

The goal is to move beyond purely predefined combat rules and explore how a boss can learn combat decisions through interaction with its environment.

---

## 📌 Table of Contents

- [Project Overview](#-project-overview)
- [Key Features](#-key-features)
- [AI Architecture](#-ai-architecture)
- [Reinforcement Learning Approach](#-reinforcement-learning-approach)
- [Technology Stack](#-technology-stack)
- [Project Structure](#-project-structure)
- [Getting Started](#-getting-started)
- [Training the Boss Agent](#-training-the-boss-agent)
- [ONNX Model Workflow](#-onnx-model-workflow)
- [Testing and Validation](#-testing-and-validation)
- [Current Limitations](#-current-limitations)
- [Future Improvements](#-future-improvements)
- [Team](#-team)
- [Acknowledgements](#-acknowledgements)
- [License](#-license)

---

## 🎮 Project Overview

Traditional game enemies frequently rely on finite state machines, distance thresholds, timers, and predefined attack-selection rules.

Although these approaches are useful, their behavior is primarily determined by rules written by developers.

This project investigates an alternative approach: **training a boss agent with reinforcement learning**.

The Unity environment provides the combat simulation, while ML-Agents connects the environment to a Python-based PPO trainer. The agent is designed to receive observations about the combat state, select actions, and learn through rewards during training.

The project combines existing game mechanics with an experimental learning-based AI pipeline.

### Project Objectives

- Develop an engaging Unity boss-fight environment.
- Implement and retain traditional game AI behavior.
- Integrate Unity ML-Agents with a PPO training configuration.
- Define observations and discrete actions for the boss agent.
- Establish a repeatable training workflow.
- Export a trained policy to ONNX format.
- Investigate whether a trained policy can support adaptive combat behavior.

---

## ✨ Key Features

### 🎯 Game and Combat System

- Real-time character movement.
- Player dodge mechanics.
- Boss combat and attack behavior.
- Unity NavMesh-based enemy navigation.
- Animator-driven character behavior.
- Health management and combat UI.
- Combat state management.

### 🧠 AI and Reinforcement Learning

- Traditional state-based enemy AI.
- A Unity ML-Agents `BossAgent` integration.
- Configurable rule-based, training, and inference control modes.
- PPO training configurations.
- A scripted player opponent for training experiments.
- A Python-based training workflow.
- ONNX model export and model-file verification.
- A training-readiness validation script.

### 📊 Experimentation and Documentation

- Smoke-training configuration for an initial training run.
- A separate PPO configuration for longer experiments.
- Training artifacts and model organization.
- Project audit documentation.
- A workflow for checking whether the environment is ready for training.

> **Research note:** The PPO integration establishes the experimental training pipeline. Its existence does not, by itself, demonstrate that the learned policy outperforms the original rule-based AI.

---

## 🏗️ AI Architecture

The project combines the Unity combat environment with a reinforcement learning training pipeline.

```text
                   UNITY COMBAT ENVIRONMENT
                             |
                 Player and Boss Simulation
                             |
                 Combat State and Observations
                             |
                         BossAgent
                             |
                 +-----------+-----------+
                 |                       |
             Rule-Based                PPO Agent
                 |                       |
          Existing AI Logic       ML-Agents Trainer
                                         |
                                  Python / PPO
                                         |
                                  Trained Policy
                                         |
                                     ONNX
                                         |
                             Unity Inference Workflow
```

The diagram represents the intended system workflow. Successful model export is documented separately from validation of the model's behavior inside Unity.

### 1. Unity Combat Environment

The environment contains the player, boss, movement system, combat mechanics, animations, health management, and navigation.

### 2. Boss Agent

The `BossAgent` integration connects the boss to ML-Agents and supports configurable control modes.

- **Rule-Based:** Uses the existing scripted behavior.
- **Training:** Allows the agent to interact with the environment during training.
- **Inference:** Provides a mode for evaluating an exported policy.

### 3. PPO Training

The Python training process uses PPO to optimize the agent's policy based on observations, actions, and rewards.

### 4. ONNX Export

The trained policy can be exported to ONNX format for use in a compatible Unity inference workflow.

The final gameplay quality of the policy must be established through actual inference tests and comparisons.

---

## 🧠 Reinforcement Learning Approach

### Proximal Policy Optimization (PPO)

The project uses **Proximal Policy Optimization**, a policy-gradient reinforcement learning algorithm supported by Unity ML-Agents.

The agent learns through repeated interactions with the environment.

The general training loop is:

1. Observe the current combat state.
2. Select an action from the available action space.
3. Execute the action in Unity.
4. Receive a reward based on the configured reward function.
5. Collect experience.
6. Update the policy through PPO optimization.
7. Repeat the process across training episodes.

### Observation Space

The agent uses a configured vector observation space to represent relevant combat information.

The current configuration defines **12 vector observations**.

The exact meaning, normalization, and correctness of each observation should be checked against the current `BossAgent.cs` implementation.

### Action Space

The current configuration defines **one discrete action branch containing four possible actions**.

These actions are mapped to the boss's available behaviors by the agent implementation.

### Reward Design

The reward function determines which behaviors the agent is encouraged to learn.

Potential objectives include:

- Dealing damage to the player.
- Avoiding damage from the player.
- Selecting effective combat actions.
- Winning or losing a combat episode.
- Avoiding unnecessary delays.

The actual reward terms and their weights are determined by the implementation in `BossAgent.cs`. They should be inspected before making claims about the precise reward function.

### Training Opponent

A scripted player opponent provides a repeatable environment for initial training experiments.

A limitation of this approach is that a policy trained against one scripted opponent may not generalize to different player strategies.

---

## 🛠️ Technology Stack

| Technology | Purpose |
|---|---|
| Unity 2021.3 | Game engine and combat simulation |
| C# | Game mechanics and agent implementation |
| Unity ML-Agents | Communication between Unity and the training process |
| PPO | Reinforcement learning algorithm |
| Python | Training and experiment tooling |
| ONNX | Exported policy representation |
| Unity NavMesh | Enemy navigation |
| Animator | Character animation and state transitions |
| Git | Version control |
| GitHub | Source code hosting and collaboration |
| Visual Studio | C# development and debugging |

The repository includes ML-Agents package integration. Use the package versions and Python dependencies specified by the project rather than assuming that every current release is compatible.

---

## 📂 Project
