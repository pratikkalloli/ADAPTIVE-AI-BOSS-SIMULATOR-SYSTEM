using System.Collections;
using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Optional ML-Agents controller. Actions are requests; EnemyMovement and the
/// existing Animator, NavMesh, hitbox, and CombatManager paths remain authoritative.
/// Observation order (12): local player direction X/Z, distance/20, player HP/500,
/// boss HP/1500, Idle/Patrol/Chase/Attack one-hot, can-attack, player-dead, boss-dead.
/// </summary>
public enum BossControlMode
{
    RuleBased = 0,
    Training = 1,
    Inference = 2
}

public sealed class BossAgent : Agent
{
    private const float InvalidActionPenalty = -0.002f;
    private const float DamageReward = 0.10f;
    private const float TerminalReward = 1f;
    private const float TimeoutReward = -0.1f;

    [Header("Boss Control Mode")]
    [SerializeField] private BossControlMode controlMode = BossControlMode.RuleBased;
    [SerializeField] private ScriptedPlayerPolicy scriptedPlayerPolicy;
    // Keep MaxStep under an explicit Agent-controlled terminal path so timeout
    // outcome, reward, statistics, and CSV records are emitted before reset.
    [Min(1)] [SerializeField] private int maxTrainingDecisionSteps = 1200;

    private EnemyMovement boss;
    private CombatManager combat;
    private Transform player;
    private bool episodeInitialized;
    private bool terminalRewardIssued;
    private bool endEpisodePending;
    private int episodeDecisionSteps;

    private bool CanAccessSceneObjects()
    {
        // ML-Agents calls CollectObservations from Agent.OnDisable while the
        // GameObject is being shut down. Do not query the scene in that path.
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    public override void Initialize()
    {
        ResolveReferences();
        if (scriptedPlayerPolicy == null) scriptedPlayerPolicy = FindObjectOfType<ScriptedPlayerPolicy>();

        if (HasTrainingCommandLineFlag())
        {
            controlMode = BossControlMode.Training;
        }

        BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();
        if (controlMode == BossControlMode.Inference &&
            (behaviorParameters == null || behaviorParameters.Model == null))
        {
            Debug.LogError("BossAgent is set to Inference, but no trained ONNX model is assigned to Behavior Parameters. Falling back to RuleBased mode.");
            controlMode = BossControlMode.RuleBased;
        }

        if (controlMode == BossControlMode.Inference && behaviorParameters != null &&
            behaviorParameters.BehaviorType != BehaviorType.InferenceOnly)
        {
            // The assigned BehaviorParameters.Model is the supported ML-Agents inference model slot.
            behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
        }

        if (boss != null)
        {
            boss.ControllerMode = controlMode == BossControlMode.RuleBased
                ? BossControllerMode.RuleBased
                : BossControllerMode.MLAgents;
        }

        if (scriptedPlayerPolicy != null)
        {
            scriptedPlayerPolicy.SetTrainingEnabled(controlMode == BossControlMode.Training);
        }

        if (combat != null)
        {
            combat.DamageApplied += OnDamageApplied;
            combat.FightEnded += OnFightEnded;
        }
    }

    private static bool HasTrainingCommandLineFlag()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length; i++)
        {
            if (string.Equals(arguments[i], "--boss-training", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    protected override void OnDisable()
    {
        // A delayed death-presentation coroutine must not end/reset an Agent
        // after Unity has disabled it. The base callback still sends ML-Agents'
        // terminal notification; CollectObservations handles that safely.
        endEpisodePending = false;
        base.OnDisable();
    }

    private void OnDestroy()
    {
        if (combat != null)
        {
            combat.DamageApplied -= OnDamageApplied;
            combat.FightEnded -= OnFightEnded;
        }
    }

    private bool ResolveReferences()
    {
        if (!CanAccessSceneObjects())
        {
            return false;
        }

        if (boss == null)
        {
            GameObject bossObject = GameObject.Find("NightmareDragon");
            if (bossObject != null && bossObject.activeInHierarchy) boss = bossObject.GetComponent<EnemyMovement>();
        }
        if (combat == null)
        {
            GameObject managerObject = GameObject.Find("GameManager");
            if (managerObject != null && managerObject.activeInHierarchy) combat = managerObject.GetComponent<CombatManager>();
        }
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null && playerObject.activeInHierarchy) player = playerObject.transform;
        }

        return boss != null && boss.gameObject.activeInHierarchy &&
               combat != null && combat.gameObject.activeInHierarchy &&
               player != null && player.gameObject.activeInHierarchy;
    }

    public override void OnEpisodeBegin()
    {
        if (!CanAccessSceneObjects())
        {
            return;
        }

        if (!ResolveReferences())
        {
            return;
        }
        episodeDecisionSteps = 0;
        endEpisodePending = false;
        if (!episodeInitialized)
        {
            episodeInitialized = true;
            terminalRewardIssued = false;
            return;
        }

        if (boss != null && boss.IsAgentControlled && combat != null)
        {
            terminalRewardIssued = false;
            combat.ResetForAgentEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Agent.OnDisable() asks for a final terminal observation after this
        // component has become inactive. Keep the fixed observation size while
        // avoiding Unity object access during teardown.
        if (!CanAccessSceneObjects())
        {
            AddEmptyObservations(sensor);
            return;
        }

        if (!ResolveReferences() || boss == null || !boss.gameObject.activeInHierarchy || combat == null ||
            !combat.gameObject.activeInHierarchy || player == null ||
            !player.gameObject.activeInHierarchy || boss.dragonAnim == null ||
            !boss.dragonAnim.isActiveAndEnabled)
        {
            AddEmptyObservations(sensor);
            return;
        }

        Vector3 relative = player.position - boss.transform.position;
        relative.y = 0f;
        Vector3 localDirection = boss.transform.InverseTransformDirection(relative.normalized);
        sensor.AddObservation(Mathf.Clamp(localDirection.x, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(localDirection.z, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp01(relative.magnitude / 20f));
        sensor.AddObservation(Mathf.Clamp01(combat.getHealth() / 500f));
        sensor.AddObservation(Mathf.Clamp01(combat.getDragonHP() / 1500f));

        AnimatorStateInfo state = boss.dragonAnim.GetCurrentAnimatorStateInfo(0);
        sensor.AddObservation(state.IsName("IdleState") ? 1f : 0f);
        sensor.AddObservation(state.IsName("PatrolState") ? 1f : 0f);
        sensor.AddObservation(state.IsName("ChaseState") ? 1f : 0f);
        sensor.AddObservation(state.IsName("AttackState") ? 1f : 0f);
        sensor.AddObservation(boss.CanAgentAttack() ? 1f : 0f);
        sensor.AddObservation(combat.getHealth() <= 0 ? 1f : 0f);
        sensor.AddObservation(combat.getDragonHP() <= 0 ? 1f : 0f);
    }

    private static void AddEmptyObservations(VectorSensor sensor)
    {
        for (int i = 0; i < 12; i++) sensor.AddObservation(0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!CanAccessSceneObjects())
        {
            return;
        }

        if (!ResolveReferences() || boss == null || !boss.IsAgentControlled || combat == null ||
            combat.IsCombatantDead || terminalRewardIssued || endEpisodePending)
        {
            return;
        }

        if (episodeDecisionSteps >= Mathf.Max(1, maxTrainingDecisionSteps))
        {
            combat.EndTrainingEpisodeForTimeout();
            return;
        }
        episodeDecisionSteps++;

        if (boss.IsAgentActionLocked)
        {
            return;
        }

        int actionIndex = actions.DiscreteActions.Length > 0 ? actions.DiscreteActions[0] : -1;
        bool accepted = actionIndex >= 0 && actionIndex <= (int)BossAgentAction.Hold &&
                        boss.RequestAgentAction((BossAgentAction)actionIndex);
        if (!accepted)
        {
            AddReward(InvalidActionPenalty);
        }

        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction(
                "AgentAction", -1, actionIndex >= 0 ? ((BossAgentAction)actionIndex).ToString() : "invalid",
                accepted, accepted ? null : "request unavailable", "boss controller");
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;
        if (discreteActions.Length == 0)
        {
            return;
        }

        if (!CanAccessSceneObjects())
        {
            discreteActions[0] = (int)BossAgentAction.Hold;
            return;
        }

        int action = (int)BossAgentAction.Approach;
        if (!ResolveReferences() || boss == null || combat == null || player == null || combat.IsCombatantDead)
        {
            action = (int)BossAgentAction.Hold;
        }
        else
        {
            Vector3 delta = player.position - boss.transform.position;
            delta.y = 0f;
            if (delta.magnitude < 1.5f)
            {
                action = (int)BossAgentAction.Retreat;
            }
            else if (boss.CanAgentAttack())
            {
                action = (int)BossAgentAction.BasicAttack;
            }
        }

        discreteActions[0] = action;
    }

    private void OnDamageApplied(bool bossWasDamaged, int effectiveDamage)
    {
        if (!CanAccessSceneObjects() || boss == null || !boss.gameObject.activeInHierarchy ||
            !boss.IsAgentControlled || effectiveDamage <= 0)
        {
            return;
        }

        float normalized = bossWasDamaged ? effectiveDamage / 75f : effectiveDamage / 95f;
        AddReward((bossWasDamaged ? -1f : 1f) * DamageReward * Mathf.Clamp01(normalized));
    }

    private void OnFightEnded(string outcome)
    {
        if (!CanAccessSceneObjects() || boss == null || !boss.gameObject.activeInHierarchy ||
            !boss.IsAgentControlled || terminalRewardIssued)
        {
            return;
        }

        terminalRewardIssued = true;
        float terminalReward = outcome == "boss_win"
            ? TerminalReward
            : outcome == "timeout" ? TimeoutReward : -TerminalReward;
        AddReward(terminalReward);
        Academy.Instance.StatsRecorder.Add("Combat/BossWin", outcome == "boss_win" ? 1f : 0f, StatAggregationMethod.MostRecent);
        Academy.Instance.StatsRecorder.Add("Combat/PlayerWin", outcome == "player_win" ? 1f : 0f, StatAggregationMethod.MostRecent);
        Academy.Instance.StatsRecorder.Add("Combat/Timeout", outcome == "timeout" ? 1f : 0f, StatAggregationMethod.MostRecent);
        Academy.Instance.StatsRecorder.Add("Combat/EpisodeLength", StepCount, StatAggregationMethod.MostRecent);
        endEpisodePending = true;
        StartCoroutine(EndEpisodeAfterTerminalDelay(outcome == "timeout" ? 0f : 1.5f));
    }

    private IEnumerator EndEpisodeAfterTerminalDelay(float delaySeconds)
    {
        // Preserve the death presentation; timeouts have no death presentation to wait for.
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }
        else
        {
            // Keep the timeout terminal guard active until the current event
            // callback and frame finish; avoid a reentrant EndEpisode/reset.
            yield return null;
        }
        if (!endEpisodePending)
        {
            yield break;
        }

        if (!CanAccessSceneObjects())
        {
            endEpisodePending = false;
            yield break;
        }

        if (endEpisodePending)
        {
            endEpisodePending = false;
            EndEpisode();
        }
    }
}
