using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Animator PlayerAnim;
    public GameObject playerGameObject;

    private bool canAttack = false;
    private readonly HashSet<Collider> bossHitboxesInRange = new HashSet<Collider>();

    PlayerMovement playermovement;
    CombatManager combatManager;

    private AudioSource source;
    public AudioClip clip;

    private DataLogger logger;
    private Transform bossTransform;
    private int attackSequenceId;
    private bool scriptedAttackInputPending;

    public bool IsBossInAttackRange
    {
        get { return canAttack; }
    }

    private void Start()
    {
        source = GetComponent<AudioSource>();

        if (source != null)
        {
            source.clip = clip;
        }

        playermovement = playerGameObject.GetComponent<PlayerMovement>();
        combatManager = GameObject.Find("GameManager").GetComponent<CombatManager>();
        GameObject bossObject = GameObject.Find("NightmareDragon");
        bossTransform = bossObject != null ? bossObject.transform : null;

        logger = DataLogger.Instance != null ? DataLogger.Instance : FindObjectOfType<DataLogger>();
    }

    private void Update()
    {
        PlayerAttack();
        LogExtraActions();
    }

    private bool CheckAnimation()
    {
        if (playermovement.PlayerAttack(true) &&
            (!PlayerAnim.GetCurrentAnimatorStateInfo(1).IsName("attack") &&
             !PlayerAnim.GetCurrentAnimatorStateInfo(1).IsName("attack_2")))
        {
            return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBossAttackCollider(other))
        {
            bossHitboxesInRange.Add(other);
            canAttack = bossHitboxesInRange.Count > 0;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (bossHitboxesInRange.Remove(other))
        {
            canAttack = bossHitboxesInRange.Count > 0;
        }
    }

    private bool IsBossAttackCollider(Collider other)
    {
        // The body hurtbox has no EnemyCombat component; the jaw and claw hitboxes do.
        return other != null &&
               other.transform.root.CompareTag("Enemy") &&
               (other.GetComponent<EnemyCombat>() != null || other.gameObject.name == "NightMareDragon");
    }

    public void ResetForAgentEpisode()
    {
        bossHitboxesInRange.Clear();
        canAttack = false;
        attackSequenceId = 0;
        scriptedAttackInputPending = false;
    }

    public void RequestScriptedAttack()
    {
        if (playermovement != null && playermovement.IsScriptedControlEnabled)
        {
            scriptedAttackInputPending = true;
        }
    }

    public void ClearScriptedAttackRequest()
    {
        scriptedAttackInputPending = false;
    }

    private bool IsPlayerAttackAnimationActive()
    {
        const int attackLayer = 1;
        AnimatorStateInfo currentState = PlayerAnim.GetCurrentAnimatorStateInfo(attackLayer);

        // The current state catches attack playback and transitions out of an attack.
        if (IsAttackState(currentState))
        {
            return true;
        }

        // During entry, the current state can still be Empty while the next state is an attack.
        return PlayerAnim.IsInTransition(attackLayer) &&
               IsAttackState(PlayerAnim.GetNextAnimatorStateInfo(attackLayer));
    }

    private bool IsAttackState(AnimatorStateInfo state)
    {
        return state.IsName("attack") || state.IsName("attack_2");
    }

    private void LogExtraActions()
    {
        if (logger == null)
            return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        bool jump = Input.GetKey(KeyCode.Space);
        bool sprint = Input.GetKey(KeyCode.LeftShift);
        bool block = Input.GetMouseButton(1);

        int playerHP = combatManager != null ? combatManager.getHealth() : 0;
        int bossHP = combatManager != null ? combatManager.getDragonHP() : 0;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            logger.LogPlayerState(
                "Jump",
                h,
                v,
                true,
                block,
                sprint,
                playerHP,
                bossHP
            );
        }

        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            logger.LogPlayerState(
                "Sprint",
                h,
                v,
                jump,
                block,
                true,
                playerHP,
                bossHP
            );
        }

        if (Input.GetMouseButtonDown(1))
        {
            logger.LogPlayerState(
                "Block",
                h,
                v,
                jump,
                true,
                sprint,
                playerHP,
                bossHP
            );
        }
    }

    public void PlayerAttack()
    {
        bool attackInput;
        if (playermovement != null && playermovement.IsScriptedControlEnabled)
        {
            attackInput = scriptedAttackInputPending;
            scriptedAttackInputPending = false;
        }
        else
        {
            attackInput = Input.GetMouseButtonDown(0);
        }
        int currentAttackSequenceId = attackInput ? ++attackSequenceId : attackSequenceId;
        if (attackInput && logger != null)
        {
            logger.LogPlayerAttackAttempt(
                currentAttackSequenceId,
                combatManager != null ? combatManager.getHealth() : -1,
                combatManager != null ? combatManager.getDragonHP() : -1);
        }
#if UNITY_EDITOR
        if (attackInput)
        {
            LogPlayerAttackSnapshot(currentAttackSequenceId, "before", "pending", "none");
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackInput sequence={currentAttackSequenceId} distance={GetBossDistance():F3}.");
        }
#endif

        if (combatManager == null || combatManager.getHealth() <= 0 || combatManager.getDragonHP() <= 0)
        {
            if (attackInput)
            {
                string reason = combatManager == null ? "combat manager unavailable" : combatManager.getHealth() <= 0 ? "player dead" : "boss dead";
                if (logger != null)
                {
                    logger.LogPlayerAttackOutcome(
                        currentAttackSequenceId, false, reason, null,
                        combatManager != null ? combatManager.getHealth() : -1,
                        combatManager != null ? combatManager.getDragonHP() : -1);
                }
#if UNITY_EDITOR
                Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackRejected sequence={currentAttackSequenceId} reason={reason} distance={GetBossDistance():F3}.");
                LogPlayerAttackSnapshot(currentAttackSequenceId, "after", "rejected", reason);
#endif
            }
            bossHitboxesInRange.Clear();
            canAttack = false;
            return;
        }

        if (!attackInput)
        {
            return;
        }

        if (IsPlayerAttackAnimationActive())
        {
            if (logger != null)
            {
                logger.LogPlayerAttackOutcome(currentAttackSequenceId, false, "attack animation active or transitioning", null, combatManager.getHealth(), combatManager.getDragonHP());
            }
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackRejected sequence={currentAttackSequenceId} reason=attack animation active or transitioning distance={GetBossDistance():F3}.");
            LogPlayerAttackSnapshot(currentAttackSequenceId, "after", "rejected", "attack animation active or transitioning");
#endif
            return;
        }

        if (!canAttack)
        {
            if (logger != null)
            {
                logger.LogPlayerAttackOutcome(currentAttackSequenceId, false, "no qualifying boss collider overlaps the player attack range", null, combatManager.getHealth(), combatManager.getDragonHP());
            }
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackRejected sequence={currentAttackSequenceId} reason=no qualifying boss collider overlaps the player attack range target=none distance={GetBossDistance():F3}.");
            LogPlayerAttackSnapshot(currentAttackSequenceId, "after", "rejected", "no qualifying boss collider overlaps the player attack range");
#endif
            return;
        }

        bool attackStarted = CheckAnimation();
        if (!attackStarted)
        {
            if (logger != null)
            {
                logger.LogPlayerAttackOutcome(currentAttackSequenceId, false, "attack animation did not start", null, combatManager.getHealth(), combatManager.getDragonHP());
            }
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackRejected sequence={currentAttackSequenceId} reason=attack animation did not start distance={GetBossDistance():F3}.");
            LogPlayerAttackSnapshot(currentAttackSequenceId, "after", "rejected", "attack animation did not start");
#endif
            return;
        }

        if (attackStarted)
        {
            if (source != null)
            {
                source.Play();
                source.volume = 0.2f;
            }

            int playerHP = combatManager.getHealth();
            int bossHP = combatManager.getDragonHP();

            string acceptedTargetName = null;
            foreach (Collider hitbox in bossHitboxesInRange)
            {
                if (hitbox != null)
                {
                    acceptedTargetName = hitbox.name;
                    break;
                }
            }

            if (logger != null)
            {
                logger.LogPlayerAttackOutcome(
                    currentAttackSequenceId, true, null, acceptedTargetName, playerHP, bossHP);
            }

#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackAccepted sequence={currentAttackSequenceId} target={acceptedTargetName ?? "unknown"} distance={GetBossDistance():F3} bossHPBefore={bossHP}.");
#endif
            combatManager.DragonTakeDamage(currentAttackSequenceId);
#if UNITY_EDITOR
            LogPlayerAttackSnapshot(currentAttackSequenceId, "after", "accepted", "none");
#endif
        }
    }

#if UNITY_EDITOR
    private void LogPlayerAttackSnapshot(int sequenceId, string phase, string decision, string reason)
    {
        bool isInTransition = false;
        string currentStateName = "unavailable";
        string nextStateName = "none";
        int currentShortNameHash = 0;
        int currentFullPathHash = 0;
        float currentNormalizedTime = -1f;
        int nextShortNameHash = 0;
        int nextFullPathHash = 0;
        float nextNormalizedTime = -1f;
        int layer = -1;

        if (PlayerAnim != null)
        {
            layer = PlayerAnim.GetLayerIndex("Attack layer");
            if (layer >= 0)
            {
                isInTransition = PlayerAnim.IsInTransition(layer);
                AnimatorStateInfo currentState = PlayerAnim.GetCurrentAnimatorStateInfo(layer);
                currentStateName = GetDiagnosticStateName(currentState);
                currentShortNameHash = currentState.shortNameHash;
                currentFullPathHash = currentState.fullPathHash;
                currentNormalizedTime = currentState.normalizedTime;

                if (isInTransition)
                {
                    AnimatorStateInfo nextState = PlayerAnim.GetNextAnimatorStateInfo(layer);
                    nextStateName = GetDiagnosticStateName(nextState);
                    nextShortNameHash = nextState.shortNameHash;
                    nextFullPathHash = nextState.fullPathHash;
                    nextNormalizedTime = nextState.normalizedTime;
                }
            }
        }

        int bossHP = combatManager != null ? combatManager.getDragonHP() : -1;
        Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerAttackDiagnostic phase={phase} sequence={sequenceId} decision={decision} reason={reason} layer={layer} currentState={currentStateName} currentShortNameHash={currentShortNameHash} currentFullPathHash={currentFullPathHash} currentNormalizedTime={currentNormalizedTime:F3} isInTransition={isInTransition} nextState={nextStateName} nextShortNameHash={nextShortNameHash} nextFullPathHash={nextFullPathHash} nextNormalizedTime={nextNormalizedTime:F3} bossHP={bossHP}.");
    }

    private string GetDiagnosticStateName(AnimatorStateInfo state)
    {
        if (state.IsName("attack") || state.IsName("Attack layer.attack")) return "attack";
        if (state.IsName("attack_2") || state.IsName("Attack layer.attack_2")) return "attack_2";
        if (state.IsName("Empty") || state.IsName("Attack layer.Empty")) return "Empty";

        return "hash_" + state.fullPathHash;
    }
#endif

    private float GetBossDistance()
    {
        if (bossTransform == null || playerGameObject == null)
        {
            return -1f;
        }

        return Vector3.Distance(playerGameObject.transform.position, bossTransform.position);
    }
}
