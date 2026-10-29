using UnityEngine;

/// <summary>
/// Optional deterministic opponent used to run unattended boss-training episodes.
/// It requests movement and attacks through the existing player components; it
/// never applies damage or changes animation states directly.
/// </summary>
public sealed class ScriptedPlayerPolicy : MonoBehaviour
{
    [SerializeField] private bool trainingEnabled;
    [SerializeField] private float attackRequestInterval = 0.9f;
    [SerializeField] private float evadeCooldown = 1.2f;

    private PlayerMovement playerMovement;
    private PlayerCombat playerCombat;
    private CombatManager combat;
    private EnemyMovement boss;
    private float nextAttackRequestTime;
    private float nextEvadeTime;

    private void Awake()
    {
        ResolveReferences();
        ApplyControlMode();
    }

    private void OnEnable()
    {
        ApplyControlMode();
    }

    private void OnDisable()
    {
        if (playerMovement != null) playerMovement.SetScriptedControl(false);
        if (playerCombat != null) playerCombat.ClearScriptedAttackRequest();
    }

    private void Update()
    {
        if (!trainingEnabled)
        {
            return;
        }

        if (!ResolveReferences())
        {
            return;
        }

        Vector3 toBoss = boss.transform.position - transform.position;
        toBoss.y = 0f;
        float distance = toBoss.magnitude;
        Vector3 towardBoss = distance > 0.001f ? toBoss / distance : transform.forward;
        Vector3 awayFromBoss = -towardBoss;
        Vector3 moveDirection = Vector3.zero;
        bool dodge = false;

        if (combat.IsCombatantDead)
        {
            playerMovement.SetScriptedMovement(Vector3.zero, towardBoss, false);
            return;
        }

        bool bossAttacking = boss.dragonAnim != null &&
                             (boss.dragonAnim.GetBool("isAttacking") ||
                              boss.dragonAnim.GetCurrentAnimatorStateInfo(0).IsName("AttackState") ||
                              (boss.dragonAnim.IsInTransition(0) &&
                               boss.dragonAnim.GetNextAnimatorStateInfo(0).IsName("AttackState")));
        if (bossAttacking && Time.time >= nextEvadeTime)
        {
            moveDirection = (awayFromBoss + transform.right * 0.65f).normalized;
            dodge = true;
            nextEvadeTime = Time.time + Mathf.Max(0.1f, evadeCooldown);
        }
        else if (playerCombat.IsBossInAttackRange && distance >= 1.2f)
        {
            if (Time.time >= nextAttackRequestTime)
            {
                playerCombat.RequestScriptedAttack();
                nextAttackRequestTime = Time.time + Mathf.Max(0.1f, attackRequestInterval);
            }
        }
        else if (distance < 1.2f)
        {
            moveDirection = awayFromBoss;
        }
        else
        {
            moveDirection = towardBoss;
        }

        playerMovement.SetScriptedMovement(moveDirection, towardBoss, dodge);
    }

    public void SetTrainingEnabled(bool enabled)
    {
        trainingEnabled = enabled;
        ResolveReferences();
        ApplyControlMode();
        if (enabled)
        {
            ResetForAgentEpisode();
        }
    }

    public void ResetForAgentEpisode()
    {
        nextAttackRequestTime = Time.time + 0.25f;
        nextEvadeTime = Time.time;
        if (playerMovement != null)
        {
            playerMovement.SetScriptedMovement(Vector3.zero, boss != null ? boss.transform.position - transform.position : Vector3.zero, false);
        }
        if (playerCombat != null) playerCombat.ClearScriptedAttackRequest();
    }

    private void ApplyControlMode()
    {
        if (playerMovement != null) playerMovement.SetScriptedControl(trainingEnabled && isActiveAndEnabled);
        if (!trainingEnabled && playerCombat != null) playerCombat.ClearScriptedAttackRequest();
    }

    private bool ResolveReferences()
    {
        if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
        if (playerCombat == null) playerCombat = FindObjectOfType<PlayerCombat>();
        if (combat == null)
        {
            GameObject manager = GameObject.Find("GameManager");
            if (manager != null) combat = manager.GetComponent<CombatManager>();
        }
        if (boss == null)
        {
            GameObject bossObject = GameObject.Find("NightmareDragon");
            if (bossObject != null) boss = bossObject.GetComponent<EnemyMovement>();
        }

        return playerMovement != null && playerCombat != null && combat != null && boss != null;
    }
}
