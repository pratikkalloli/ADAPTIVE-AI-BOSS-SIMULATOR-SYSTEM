using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.AI;
using Cinemachine;
using Random = UnityEngine.Random;

public enum BossControllerMode
{
    RuleBased = 0,
    MLAgents = 1
}

public enum BossAgentAction
{
    Approach = 0,
    Retreat = 1,
    BasicAttack = 2,
    Hold = 3
}

public class EnemyMovement : MonoBehaviour
{
    [Header("Boss Controller")]
    [SerializeField] private BossControllerMode controllerMode = BossControllerMode.RuleBased;

    public BossControllerMode ControllerMode
    {
        get { return controllerMode; }
        set { controllerMode = value; }
    }

    public bool IsAgentControlled { get { return controllerMode == BossControllerMode.MLAgents; } }
    public bool IsAgentActionLocked
    {
        get
        {
            if (dragonAnim == null || dragonAnim.IsInTransition(0)) return true;
            return dragonAnim.GetCurrentAnimatorStateInfo(0).IsName("AttackState") && !finishedAttack;
        }
    }
    public CinemachineFreeLook cinemachine;

    public Transform player;
    public Animator dragonAnim;
private float chaseMultiplier = 1f;
    private float dragonSlowSpeed = 0.3f;
    private float dragonFastSpeed = 1.5f;
    private bool finishedAttack;
    private bool playerHitThisSwing = true;
    private bool bossDeathMovementStopped;
    private int attackSwingId;
    private string currentAttackName = "unknown";
    public GameObject[] trails;

    public AudioSource source;
    public AudioClip []clip;
    public float volume;

private CombatManager combatManager;
private bool enraged = false;
private bool berserk = false;
private float initialDragonFastSpeed = 1.5f;
private float initialNavMeshAgentSpeed;
private bool initialNavMeshAgentSpeedCaptured;

    private void Awake()
    {
        NavMeshAgent navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent != null)
        {
            initialNavMeshAgentSpeed = navMeshAgent.speed;
            initialNavMeshAgentSpeedCaptured = true;
        }
    }

    private void Start()
{
    source.volume = volume;
    trailDeactivate();

    combatManager = GameObject.Find("GameManager")
                    .GetComponent<CombatManager>();
    initialDragonFastSpeed = dragonFastSpeed;
}
    void Update()
    {
        if (combatManager != null && combatManager.getDragonHP() <= 0)
        {
            if (!bossDeathMovementStopped)
            {
                NavMeshAgent agent = GetComponent<NavMeshAgent>();
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }

                bossDeathMovementStopped = true;
            }

            return;
        }

        if (dragonAnim.GetBool("isChasing") == true && !dragonAnim.GetCurrentAnimatorStateInfo(0).IsName("die") )
        {
            LookAtPlayer();
        }
        // Enrage Mode (50% HP)
if (!enraged && combatManager.getDragonHP() <= 750)
{
    enraged = true;
    dragonFastSpeed = 2.0f;

    Debug.Log("BOSS ENRAGED!");
}

// Berserk Mode (25% HP)
if (!berserk && combatManager.getDragonHP() <= 375)
{
    berserk = true;
    dragonFastSpeed = 2.8f;

    Debug.Log("BOSS BERSERK MODE!");
}
if (!IsAgentControlled && combatManager.FailedAttacks >= 3)
{
    Debug.Log("Boss Repositioning");

    Vector3 positionBefore = transform.position;
#if UNITY_EDITOR
    Debug.Log($"[CombatDiag] t={Time.time:F3} BossRepositionStart failedAttacks={combatManager.FailedAttacks} distance={GetCombatDistance():F3} position={positionBefore} {GetAgentDiagnostic()}.");
#endif
    transform.position -= transform.forward * 2f;
#if UNITY_EDITOR
    Debug.Log($"[CombatDiag] t={Time.time:F3} BossRepositionEnd distance={GetCombatDistance():F3} position={positionBefore}->{transform.position} {GetAgentDiagnostic()}.");
#endif

    int failedAttacksBeforeReset = combatManager.FailedAttacks;
    combatManager.FailedAttacks = 0;
#if UNITY_EDITOR
    Debug.Log($"[CombatDiag] t={Time.time:F3} FailedAttackCounterChanged value={failedAttacksBeforeReset}->0 reason=reposition distance={GetCombatDistance():F3}.");
#endif
}
    }

    public bool CanAgentAttack()
    {
        if (!IsAgentControlled || dragonAnim == null || player == null || combatManager == null || combatManager.IsCombatantDead)
        {
            return false;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh || dragonAnim.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo state = dragonAnim.GetCurrentAnimatorStateInfo(0);
        float distance = Vector3.Distance(transform.position, player.position);
        return state.IsName("ChaseState") && distance >= 1.5f && distance <= agent.stoppingDistance + 0.5f &&
               !dragonAnim.GetBool("isAttacking") &&
               (!dragonAnim.GetCurrentAnimatorStateInfo(0).IsName("AttackState"));
    }

    public bool RequestAgentAction(BossAgentAction action)
    {
        if (!IsAgentControlled || dragonAnim == null || player == null || combatManager == null || combatManager.IsCombatantDead)
        {
            return false;
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null || !agent.isOnNavMesh)
        {
            return false;
        }

        AnimatorStateInfo currentState = dragonAnim.GetCurrentAnimatorStateInfo(0);
        bool inAttackState = currentState.IsName("AttackState");
        if (dragonAnim.IsInTransition(0))
        {
            return false;
        }

        if (inAttackState)
        {
            if (action == BossAgentAction.BasicAttack || !finishedAttack)
            {
                return false;
            }
            dragonAnim.SetBool("isAttacking", false);
        }

        agent.isStopped = false;
        agent.speed = 20f;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 direction = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : transform.forward;

        switch (action)
        {
            case BossAgentAction.Approach:
                dragonAnim.SetBool("isPatrolling", false);
                dragonAnim.SetBool("isAttacking", false);
                dragonAnim.SetBool("isChasing", true);
                agent.SetDestination(player.position);
                return true;

            case BossAgentAction.Retreat:
                dragonAnim.SetBool("isPatrolling", false);
                dragonAnim.SetBool("isAttacking", false);
                dragonAnim.SetBool("isChasing", true);
                agent.SetDestination(transform.position - direction * 2f);
                return true;

            case BossAgentAction.BasicAttack:
                if (!CanAgentAttack())
                {
                    return false;
                }
                agent.ResetPath();
                dragonAnim.SetFloat("attacks", 0f);
                dragonAnim.SetBool("isChasing", true);
                dragonAnim.SetBool("isAttacking", true);
                return true;

            case BossAgentAction.Hold:
                agent.isStopped = true;
                agent.ResetPath();
                dragonAnim.SetBool("isAttacking", false);
                dragonAnim.SetBool("isChasing", false);
                dragonAnim.SetBool("isPatrolling", false);
                return true;

            default:
                return false;
        }
    }

    public void ResetForAgentEpisode()
    {
        finishedAttack = false;
        playerHitThisSwing = true;
        bossDeathMovementStopped = false;
        attackSwingId = 0;
        currentAttackName = "unknown";
        enraged = false;
        berserk = false;
        dragonFastSpeed = initialDragonFastSpeed;
        if (trails != null)
        {
            foreach (GameObject trail in trails)
            {
                if (trail != null) trail.SetActive(false);
            }
        }

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (initialNavMeshAgentSpeedCaptured)
            {
                agent.speed = initialNavMeshAgentSpeed;
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
    }


    void trailActivate()
    {
        foreach (GameObject trail in trails)
        {
            trail.SetActive(true);
        }
    }
    void trailDeactivate()
    {
        if(BossProfileReader.CurrentPlayerType == "Aggressive")
{
    dragonFastSpeed = 2.0f;
    Debug.Log("Aggressive Player -> Faster Boss Attacks");
}
else if(BossProfileReader.CurrentPlayerType == "HitAndRun")
{
    chaseMultiplier = 2f;
    Debug.Log("HitAndRun Player -> Faster Boss Chase");
}
else if(BossProfileReader.CurrentPlayerType == "CloseRangeFighter")
{
    dragonFastSpeed = 1.8f;
    Debug.Log("CloseRange Fighter -> Combo Mode");
}
else if(BossProfileReader.CurrentPlayerType == "Defensive")
{
    dragonFastSpeed = 1.7f;
    Debug.Log("Defensive Player -> Heavy Attack Mode");
}
        foreach (GameObject trail in trails)
        {
            trail.SetActive(false);
        }
    }

    public void ClawAttackSlow()
    {
        BeginAttackSwing("Claw");
        finishedAttack = false;
        dragonAnim.speed = dragonSlowSpeed;
        trailActivate();
    }
    public bool ClawAttackFast()
    {
        dragonAnim.speed = dragonFastSpeed;
        return true;
    }
    private void claw_end()
    {
        //Debug.Log("claw_end");
        trailDeactivate();
    }
    public void BasicAttackSlow()
    {
        BeginAttackSwing("Basic");
        trailDeactivate();
        finishedAttack = false;
        
        source.clip = clip[Random.Range(0, 1)];
        source.Play();
        dragonAnim.speed = dragonSlowSpeed;
    }
    public bool BasicAttackFast()
    {
        dragonAnim.speed = dragonFastSpeed;
        
        return true;
    }
    public void HornAttackSlow()
    {
        BeginAttackSwing("Horn");
        trailDeactivate();
        finishedAttack = false;
        dragonAnim.speed = dragonSlowSpeed;
    }
    public bool HornAttackFast()
    {
        dragonAnim.speed = dragonFastSpeed;
        return true;
    }

    public void SetAttackFast()
    {
        dragonAnim.speed = dragonFastSpeed;
    }

    public void RestoreAnimatorSpeed()
    {
        dragonAnim.speed = 1f;
    }

    public int CurrentSwingId
    {
        get { return attackSwingId; }
    }

    public string CurrentAttackName
    {
        get { return currentAttackName; }
    }

    public void BeginAttackSwing(string attackName)
    {
        attackSwingId++;
        currentAttackName = attackName;
        playerHitThisSwing = false;
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossAttackStarted", attackSwingId, attackName);
        }
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossSwingStarted swing={attackSwingId} attack={attackName} distance={GetCombatDistance():F3} {GetAgentDiagnostic()}.");
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitGateOpened swing={attackSwingId} source={attackName}AttackSlow distance={GetCombatDistance():F3}.");
#endif
    }

    public void EndAttackSwing()
    {
        EndAttackSwing("unspecified");
    }

    public void EndAttackSwing(string source)
    {
        bool wasOpen = !playerHitThisSwing;
        playerHitThisSwing = true;
#if UNITY_EDITOR
        if (wasOpen)
        {
            Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitGateClosed swing={attackSwingId} source={source} reason=window ended distance={GetCombatDistance():F3}.");
        }
#endif
    }

    public bool TryRegisterPlayerHitThisSwing()
    {
        if (playerHitThisSwing)
        {
            return false;
        }

        playerHitThisSwing = true;
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitGateClosed swing={attackSwingId} source=accepted hit reason=one hit consumed distance={GetCombatDistance():F3}.");
#endif
        return true;
    }

    private float GetCombatDistance()
    {
        return player != null ? Vector3.Distance(transform.position, player.position) : -1f;
    }

#if UNITY_EDITOR
    private string GetAgentDiagnostic()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            return "agent=missing";
        }

        return $"agentSpeed={agent.speed:F2} stoppingDistance={agent.stoppingDistance:F2} pathStatus={(agent.isOnNavMesh ? agent.pathStatus.ToString() : "offNavMesh")}";
    }
#endif

    public void Claw_fin()
    {
        //Debug.Log("claw_fin");
        finishedAttack= true;
        EndAttackSwing("Claw_fin");
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossAttackFinished", attackSwingId, "Claw");
        }
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossSwingFinished swing={attackSwingId} attack=Claw distance={GetCombatDistance()}.");
#endif
    }
    public void Horn_fin()
    {
        //Debug.Log("horn_fin");
        finishedAttack= true;
        EndAttackSwing("Horn_fin");
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossAttackFinished", attackSwingId, "Horn");
        }
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossSwingFinished swing={attackSwingId} attack=Horn distance={GetCombatDistance()}.");
#endif
    }
    public void Basic_fin()
    {
        //Debug.Log("Basic_fin");
        finishedAttack = true;
        EndAttackSwing("Basic_fin");
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossAttackFinished", attackSwingId, "Basic");
        }
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossSwingFinished swing={attackSwingId} attack=Basic distance={GetCombatDistance()}.");
#endif
    }

    public bool returnFinishedAttack()
    {
        return finishedAttack;
    }
    public bool death()
    {
        dragonAnim.speed = dragonFastSpeed;
        this.GetComponent<RigBuilder>().enabled = false;
        return true;
    }
    void LookAtPlayer()
{
    Vector3 direction = (player.position - transform.position).normalized;

    Quaternion lookRotation = Quaternion.LookRotation(
        new Vector3(direction.x, 0, direction.z));

    transform.rotation = Quaternion.Slerp(
        transform.rotation,
        lookRotation,
        Time.deltaTime * (2f * chaseMultiplier));
}
}
