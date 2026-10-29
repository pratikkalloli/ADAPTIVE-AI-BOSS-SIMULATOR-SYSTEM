using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
public class EnemyCombat : MonoBehaviour
{
    [Header("Dragon Attack Bones")]
    [SerializeField] private GameObject ClawAttackBone;
    [SerializeField] private GameObject HeadAttackBone;

    [Header("Dragon Animataions")]

    AttackState DragonAttackState;
    EnemyMovement DragonMovement;
    CombatManager combatManager;
    UI_Manager uimanager;

    //private bool DragonAttackFlag = false;
    public Animator DragonAnim;
    public Animator PlayerAnim;
    int playerHP;
#if UNITY_EDITOR
    private int lastLoggedSwingId = int.MinValue;
#endif
    private int lastTelemetryAttemptSwingId = int.MinValue;
    private void Start()
    {
        uimanager = GameObject.Find("Healthbar_Canvas").GetComponent<UI_Manager>();
        combatManager = GameObject.Find("GameManager").GetComponent<CombatManager>();
        DragonMovement = GameObject.Find("NightmareDragon").GetComponent<EnemyMovement>();
        DragonAttackState = DragonAnim.GetBehaviour<AttackState>();
    }

    public void ResetForAgentEpisode()
    {
        lastTelemetryAttemptSwingId = int.MinValue;
#if UNITY_EDITOR
        lastLoggedSwingId = int.MinValue;
#endif
    }
    private void OnTriggerEnter(Collider other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider other)
    {
        if (other.gameObject.name != "ToonRTS_demo_Knight")
        {
            return;
        }

        int currentSwingId = DragonMovement != null ? DragonMovement.CurrentSwingId : -1;
        bool logTelemetryAttempt = lastTelemetryAttemptSwingId != currentSwingId;
        if (logTelemetryAttempt)
        {
            lastTelemetryAttemptSwingId = currentSwingId;
            if (DataLogger.Instance != null)
            {
                DataLogger.Instance.LogBossAction("BossHitAttempt", currentSwingId, name, null, null, other.name);
            }
        }

#if UNITY_EDITOR
        int swingId = currentSwingId;
        bool logAttempt = lastLoggedSwingId != swingId;
        if (logAttempt)
        {
            lastLoggedSwingId = swingId;
            float distance = PlayerAnim != null
                ? Vector3.Distance(transform.position, other.transform.position)
                : -1f;
            Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitAttempt swing={swingId} source={name} playerCollider={other.name} distance={distance:F3} playerHP={(combatManager != null ? combatManager.getHealth() : -1)} bossHP={(combatManager != null ? combatManager.getDragonHP() : -1)}.");
        }
#endif

        string rejectionReason = null;
        if (!DragonAnim.GetCurrentAnimatorStateInfo(0).IsName("AttackState"))
        {
            rejectionReason = "Animator is outside AttackState";
        }
        else if (combatManager.getDragonHP() <= 0)
        {
            rejectionReason = "boss is dead";
        }
        else if (combatManager.getHealth() <= 0)
        {
            rejectionReason = "player is dead";
        }
        else if (DragonMovement.returnFinishedAttack())
        {
            rejectionReason = "attack finish event closed the gate";
        }

        if (rejectionReason != null)
        {
            if (logTelemetryAttempt && DataLogger.Instance != null)
            {
                DataLogger.Instance.LogBossAction("BossHitRejected", currentSwingId, name, false, rejectionReason, other.name);
            }
#if UNITY_EDITOR
            if (logAttempt)
            {
                Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitRejected swing={(DragonMovement != null ? DragonMovement.CurrentSwingId : -1)} source={name} playerCollider={other.name} distance={Vector3.Distance(transform.position, other.transform.position):F3} reason={rejectionReason} playerHP={(combatManager != null ? combatManager.getHealth() : -1)} bossHP={(combatManager != null ? combatManager.getDragonHP() : -1)}.");
            }
#endif
            return;
        }

        if (!DragonMovement.TryRegisterPlayerHitThisSwing())
        {
            if (logTelemetryAttempt && DataLogger.Instance != null)
            {
                DataLogger.Instance.LogBossAction("BossHitRejected", currentSwingId, name, false, "swing gate already consumed", other.name);
            }
#if UNITY_EDITOR
            if (logAttempt)
            {
                Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitRejected swing={(DragonMovement != null ? DragonMovement.CurrentSwingId : -1)} source={name} playerCollider={other.name} distance={Vector3.Distance(transform.position, other.transform.position):F3} reason=swing gate already consumed playerHP={(combatManager != null ? combatManager.getHealth() : -1)} bossHP={(combatManager != null ? combatManager.getDragonHP() : -1)}.");
            }
#endif
            return;
        }

        DragonMovement.SetAttackFast();
        combatManager.BossAttackHit = true;
        if (DataLogger.Instance != null)
        {
            DataLogger.Instance.LogBossAction("BossHitAccepted", currentSwingId, DragonMovement.CurrentAttackName, true, null, other.name);
        }
        int failedAttacksBefore = combatManager.FailedAttacks;
        combatManager.FailedAttacks = 0;
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossHitAcceptedByGate swing={DragonMovement.CurrentSwingId} source={name} playerCollider={other.name} distance={Vector3.Distance(transform.position, other.transform.position):F3}.");
        if (failedAttacksBefore != 0)
        {
            Debug.Log($"[CombatDiag] t={Time.time:F3} FailedAttackCounterChanged value={failedAttacksBefore}->0 reason=accepted boss hit distance={Vector3.Distance(transform.position, other.transform.position):F3}.");
        }
#endif
        combatManager.PlayerTakeDamage(DragonMovement.CurrentSwingId);
        PlayerAnim.SetTrigger("getHit");
        uimanager.StartCoroutine(uimanager.DoFade());
    }
}
