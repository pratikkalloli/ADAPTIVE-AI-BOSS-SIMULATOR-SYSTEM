using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AttackState : StateMachineBehaviour
{
    EnemyMovement enemyMovement;
    NavMeshAgent agent;
    Transform player;
    CombatManager combatManager;

    private float[] attackOptions = new float[] { 0f, 0.5f, 1f };

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        enemyMovement = GameObject.Find("NightmareDragon").GetComponent<EnemyMovement>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        agent = animator.GetComponent<NavMeshAgent>();
        enemyMovement.EndAttackSwing("AttackState.OnStateEnter");
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossAttackStateEntered", enemyMovement.CurrentSwingId, "AttackState");
        }
#if UNITY_EDITOR
        string pathStatus = agent != null && agent.isOnNavMesh ? agent.pathStatus.ToString() : "offNavMesh";
        Debug.Log($"[CombatDiag] t={Time.time:F3} AttackStateEntered distance={Vector3.Distance(player.position, animator.transform.position):F3} agentSpeed={(agent != null ? agent.speed : -1f):F2} stopDistance={(agent != null ? agent.stoppingDistance : -1f):F3} enterRange={(agent != null ? agent.stoppingDistance + 0.5f : -1f):F3} pathStatus={pathStatus}.");
#endif
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (enemyMovement != null && enemyMovement.IsAgentControlled) return;

        float distance = Vector3.Distance(player.position, animator.transform.position);

        if (distance > agent.stoppingDistance + 0.75f)
        {
            animator.SetBool("isAttacking", false);
        }
    }

   public int ChooseDragonAttack()
{
    if(BossProfileReader.CurrentPlayerType == "Aggressive")
    {
        // More aggressive attacks
        return Random.Range(1, 3);
    }

    else if(BossProfileReader.CurrentPlayerType == "Defensive")
    {
        // Mix attacks
        return Random.Range(0, 2);
    }

    else
    {
        // Normal behavior
        return Random.Range(0, 3);
    }
}

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {   CombatManager cm = GameObject.Find("GameManager").GetComponent<CombatManager>();
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossAttackStateExited", enemyMovement.CurrentSwingId, "AttackState");
        }

#if UNITY_EDITOR
        float exitDistance = Vector3.Distance(player.position, animator.transform.position);
        string exitReason = cm.getDragonHP() <= 0
            ? "boss death transition"
            : agent != null && exitDistance > agent.stoppingDistance + 0.75f
                ? "outside attack exit range"
                : "Animator transition";
        string pathStatus = agent != null && agent.isOnNavMesh ? agent.pathStatus.ToString() : "offNavMesh";
        Debug.Log($"[CombatDiag] t={Time.time:F3} AttackStateExited reason={exitReason} distance={exitDistance:F3} agentSpeed={(agent != null ? agent.speed : -1f):F2} stopDistance={(agent != null ? agent.stoppingDistance : -1f):F3} exitRange={(agent != null ? agent.stoppingDistance + 0.75f : -1f):F3} pathStatus={pathStatus} bossHitThisState={cm.BossAttackHit} playerHP={cm.getHealth()} bossHP={cm.getDragonHP()}.");
#endif

        if (cm.getDragonHP() <= 0)
        {
            cm.BossAttackHit = false;
            enemyMovement.RestoreAnimatorSpeed();
            return;
        }

        if (enemyMovement != null && enemyMovement.IsAgentControlled)
        {
            cm.BossAttackHit = false;
            enemyMovement.RestoreAnimatorSpeed();
            return;
        }

        if (!cm.BossAttackHit)
        {
            int failedAttacksBefore = cm.FailedAttacks;
            cm.FailedAttacks++;
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} FailedAttackCounterChanged value={failedAttacksBefore}->{cm.FailedAttacks} reason=AttackState exit without accepted boss hit distance={Vector3.Distance(player.position, animator.transform.position):F3}.");
#endif
        }

        cm.BossAttackHit = false;
        enemyMovement.RestoreAnimatorSpeed();
        
        animator.SetFloat("attacks", attackOptions[ChooseDragonAttack()]);
    Debug.Log("Current Player Type = " +
          BossProfileReader.CurrentPlayerType);
    }
}
