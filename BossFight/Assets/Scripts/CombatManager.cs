using Newtonsoft.Json.Bson;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

public class CombatManager : MonoBehaviour
{
    public event System.Action<bool, int> DamageApplied;
    public event System.Action<string> FightEnded;

    public bool BossAttackHit = false;
public int FailedAttacks = 0;
    [Header("Dragon Stats")]

    [SerializeField] private GameObject DragonBody;
    [SerializeField] private int DragonHP;
    private Animator DragonAnim;

    [Header("Player Stats")]
    [SerializeField] private GameObject PlayerBody;
    [SerializeField] private int PlayerHP;
    [SerializeField] private int PlayerDamage = 75;
                     private bool PlayerIsDead;
    private bool DragonIsDead;
    private bool trainingEpisodeTerminated;
    [SerializeField] private KeyCode restartKey = KeyCode.R;
    private bool restartInProgress;

    //Script References
    PlayerMovement playerMovement;
    ScriptedPlayerPolicy scriptedPlayerPolicy;
    RagDollManager PlayerRagDoll;
    PlayerCombat playerCombat;
    EnemyCombat enemyCombat;
    Behaviour patrolBehaviour;
    DataLogger telemetry;


    public ParticleSystem bloodVfx, bloodVfx2, bloodVfx3;
    UI_Manager ui;

    bool waiting;
    private bool episodeReady;
    private Transform playerRoot;
    private Vector3 initialPlayerPosition;
    private Quaternion initialPlayerRotation;
    private Vector3 initialBossPosition;
    private Quaternion initialBossRotation;

    public bool IsCombatantDead { get { return PlayerIsDead || DragonIsDead || trainingEpisodeTerminated || PlayerHP <= 0 || DragonHP <= 0; } }
    public bool IsEpisodeReady { get { return episodeReady; } }

    private void Start()
    {
        Time.timeScale = 1f;
        bloodVfx.Stop();
        bloodVfx2.Stop();
        bloodVfx3.Stop();
        //patrolBehaviour = DragonAnim.GetComponent<Behaviour>();

        DragonAnim = GameObject.Find("NightmareDragon").GetComponent<Animator>();
        playerCombat = GameObject.Find("Bip001 R Hand").GetComponent<PlayerCombat>();
        enemyCombat = GameObject.Find("Jaw01").GetComponent<EnemyCombat>();

        DragonHP = 1500;
        PlayerHP = 500;
        DragonIsDead = false;
        PlayerIsDead = false;
        trainingEpisodeTerminated = false;

        ui = GameObject.Find("Healthbar_Canvas").GetComponent<UI_Manager>();
        playerMovement = GameObject.Find("Player").GetComponent<PlayerMovement>();
        scriptedPlayerPolicy = playerMovement != null ? playerMovement.GetComponent<ScriptedPlayerPolicy>() : null;
        playerRoot = playerMovement != null ? playerMovement.transform : null;
        PlayerRagDoll = GetComponent<RagDollManager>();
        if (playerRoot != null)
        {
            initialPlayerPosition = playerRoot.position;
            initialPlayerRotation = playerRoot.rotation;
        }
        if (DragonBody != null)
        {
            initialBossPosition = DragonBody.transform.position;
            initialBossRotation = DragonBody.transform.rotation;
        }

        telemetry = DataLogger.Instance != null ? DataLogger.Instance : FindObjectOfType<DataLogger>();
        if (telemetry != null)
        {
            telemetry.BeginEpisode(PlayerHP, DragonHP);
        }
        else
        {
            Debug.LogWarning("DataLogger is missing; this fight will not be written to telemetry CSV.");
        }

        episodeReady = true;
        
    }

    private void Update()
    {
        if (restartInProgress || (!PlayerIsDead && !DragonIsDead) || !Input.GetKeyDown(restartKey))
        {
            return;
        }

        RestartFight();
    }

    private void RestartFight()
    {
        if (restartInProgress || (!PlayerIsDead && !DragonIsDead))
        {
            return;
        }

        UnityEngine.SceneManagement.Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex < 0)
        {
            Debug.LogError("Cannot restart the fight because the active scene is not in Build Settings.");
            return;
        }

        restartInProgress = true;
        bool playerMovementWasEnabled = playerMovement != null && playerMovement.enabled;
        bool playerCombatWasEnabled = playerCombat != null && playerCombat.enabled;
        bool playerRagdollWasEnabled = PlayerRagDoll != null && PlayerRagDoll.enabled;
        EnemyMovement bossMovement = DragonBody != null ? DragonBody.GetComponent<EnemyMovement>() : null;
        bool bossMovementWasEnabled = bossMovement != null && bossMovement.enabled;
        bool bossCombatWasEnabled = enemyCombat != null && enemyCombat.enabled;
        UnityEngine.AI.NavMeshAgent bossAgent = DragonBody != null
            ? DragonBody.GetComponent<UnityEngine.AI.NavMeshAgent>()
            : null;
        bool bossAgentWasStopped = bossAgent != null && bossAgent.isOnNavMesh && bossAgent.isStopped;
        float bossAnimatorSpeed = DragonAnim != null ? DragonAnim.speed : 1f;
        Animator playerAnimator = playerCombat != null ? playerCombat.PlayerAnim : null;
        float playerAnimatorSpeed = playerAnimator != null ? playerAnimator.speed : 1f;

        try
        {
            StopAllCoroutines();
            waiting = false;
            Time.timeScale = 0f;
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
            if (PlayerRagDoll != null) PlayerRagDoll.enabled = false;
            if (bossMovement != null) bossMovement.enabled = false;
            if (enemyCombat != null) enemyCombat.enabled = false;
            if (bossAgent != null && bossAgent.isOnNavMesh) bossAgent.isStopped = true;
            if (DragonAnim != null) DragonAnim.speed = 0f;
            if (playerAnimator != null) playerAnimator.speed = 0f;
            if (ui != null) ui.StopAllCoroutines();

            SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Time.timeScale = 1f;
            restartInProgress = false;
            if (playerMovement != null) playerMovement.enabled = playerMovementWasEnabled;
            if (playerCombat != null) playerCombat.enabled = playerCombatWasEnabled;
            if (PlayerRagDoll != null) PlayerRagDoll.enabled = playerRagdollWasEnabled;
            if (bossMovement != null) bossMovement.enabled = bossMovementWasEnabled;
            if (enemyCombat != null) enemyCombat.enabled = bossCombatWasEnabled;
            if (bossAgent != null && bossAgent.isOnNavMesh) bossAgent.isStopped = bossAgentWasStopped;
            if (DragonAnim != null) DragonAnim.speed = bossAnimatorSpeed;
            if (playerAnimator != null) playerAnimator.speed = playerAnimatorSpeed;
            Debug.LogError("Fight restart failed: " + exception.Message);
        }
    }

    public void ResetForAgentEpisode()
    {
        if (!episodeReady || DragonBody == null || playerRoot == null)
        {
            Debug.LogError("Cannot reset the ML-Agents episode because combat references are not ready.");
            return;
        }

        EnemyMovement bossMovement = DragonBody.GetComponent<EnemyMovement>();
        UnityEngine.AI.NavMeshAgent bossAgent = DragonBody.GetComponent<UnityEngine.AI.NavMeshAgent>();
        Animator playerAnimator = playerCombat != null ? playerCombat.PlayerAnim : null;
        CharacterController playerController = playerRoot.GetComponent<CharacterController>();

        StopAllCoroutines();
        waiting = false;
        Time.timeScale = 1f;

        if (playerController != null) playerController.enabled = false;
        playerRoot.SetPositionAndRotation(initialPlayerPosition, initialPlayerRotation);
        if (bossAgent != null && bossAgent.isOnNavMesh)
        {
            bossAgent.isStopped = true;
            bossAgent.ResetPath();
            bossAgent.Warp(initialBossPosition);
            bossAgent.velocity = Vector3.zero;
            DragonBody.transform.rotation = initialBossRotation;
        }
        else
        {
            DragonBody.transform.SetPositionAndRotation(initialBossPosition, initialBossRotation);
        }

        PlayerHP = 500;
        DragonHP = 1500;
        PlayerIsDead = false;
        DragonIsDead = false;
        trainingEpisodeTerminated = false;
        BossAttackHit = false;
        FailedAttacks = 0;

        if (PlayerRagDoll != null)
        {
            PlayerRagDoll.enabled = true;
            PlayerRagDoll.ResetForAgentEpisode();
        }
        if (playerController != null) playerController.enabled = true;
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
            playerMovement.ResetForAgentEpisode();
        }
        if (scriptedPlayerPolicy != null) scriptedPlayerPolicy.ResetForAgentEpisode();
        if (playerCombat != null)
        {
            playerCombat.enabled = true;
            playerCombat.ResetForAgentEpisode();
        }
        if (enemyCombat != null)
        {
            enemyCombat.enabled = true;
            enemyCombat.ResetForAgentEpisode();
        }
        if (bossMovement != null)
        {
            bossMovement.enabled = true;
            bossMovement.ResetForAgentEpisode();
        }

        if (DragonAnim != null)
        {
            DragonAnim.enabled = true;
            DragonAnim.speed = 1f;
            DragonAnim.Rebind();
            DragonAnim.SetBool("isPatrolling", false);
            DragonAnim.SetBool("isChasing", false);
            DragonAnim.SetBool("isAttacking", false);
            DragonAnim.SetBool("dead", false);
            DragonAnim.SetFloat("attacks", 0f);
            DragonAnim.Update(0f);
        }
        if (playerAnimator != null)
        {
            playerAnimator.enabled = true;
            playerAnimator.speed = 1f;
            playerAnimator.Rebind();
            int attackLayer = playerAnimator.GetLayerIndex("Attack layer");
            if (attackLayer >= 0) playerAnimator.SetLayerWeight(attackLayer, 0f);
            playerAnimator.Update(0f);
        }
        if (bossAgent != null && bossAgent.isOnNavMesh) bossAgent.isStopped = false;
        if (bloodVfx != null) bloodVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (bloodVfx2 != null) bloodVfx2.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (bloodVfx3 != null) bloodVfx3.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (ui != null) ui.ResetForAgentEpisode();

        if (telemetry != null)
        {
            telemetry.BeginNextEpisode(PlayerHP, DragonHP);
        }
    }

    private void bleed()
    {
        int bloodNumber = Random.Range(0, 2);

        switch (bloodNumber)
        {
            case 0:
                bloodVfx.Play();
                Debug.Log(bloodVfx);
                break;
            case 1:
                bloodVfx2.Play();
                Debug.Log(bloodVfx2);
                break;
            case 3:
                bloodVfx3.Play();
                Debug.Log(bloodVfx3);
                break;
        }
    }

    private void wait(float duration)
    {
        if (waiting)
        {
            return;
        }
        Time.timeScale = 0f;
        StartCoroutine(HitStop(duration));
    }
    IEnumerator HitStop(float duration)
    {
        waiting = true;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
        waiting= false;
    }


    public void DragonTakeDamage()
    {
        DragonTakeDamage(-1);
    }

    public void DragonTakeDamage(int attackSequenceId)
    {
        if (DragonIsDead || trainingEpisodeTerminated || DragonHP <= 0)
        {
            if (telemetry != null)
            {
                telemetry.LogDamageEvent("PlayerDamageRejected", "player", attackSequenceId, -1, "Attack", "NightmareDragon", PlayerHP, PlayerHP, DragonHP, DragonHP, trainingEpisodeTerminated ? "episode already ended" : "boss already dead");
            }
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerDamageRejected sequence={attackSequenceId} reason={(trainingEpisodeTerminated ? "episode already ended" : "boss already dead")} playerHP={PlayerHP} bossHP={DragonHP}.");
#endif
            return;
        }

        int bossHpBefore = DragonHP;
        DragonHP = Mathf.Clamp(DragonHP - PlayerDamage, 0, 1500);
        ui.UpdateHealthBar(1500, DragonHP);
        bleed();
        setDragonHP(DragonHP);
        if (telemetry != null)
        {
            telemetry.LogDamageEvent("PlayerDamageApplied", "player", attackSequenceId, -1, "Attack", "NightmareDragon", PlayerHP, PlayerHP, bossHpBefore, DragonHP);
        }
        DamageApplied?.Invoke(true, bossHpBefore - DragonHP);
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerDamageApplied sequence={attackSequenceId} bossHP={bossHpBefore}->{DragonHP} damage={PlayerDamage} playerHP={PlayerHP}.");
#endif

        if (DragonHP <= 0)
        {
            DragonIsDead = true;
            DragonAnim.SetBool("dead", true);
            DragonAnim.SetBool("isAttacking", false);
            DragonAnim.SetBool("isChasing", false);
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} BossDeathTransition reason=HP reached 0 playerHP={PlayerHP} bossHP={DragonHP}.");
#endif
            if (telemetry != null)
            {
                telemetry.LogEpisodeEnd("player_win", PlayerHP, DragonHP);
            }
            FightEnded?.Invoke("player_win");

           // SceneManager.LoadScene(SceneManager.GetActiveScene().name);
          //Debug.Log("Dragon is Dead");
        }

    }
    private void setDragonHP(int dragonHealth)
    {
        DragonHP= dragonHealth;
    }
    public int getDragonHP()
    {
        return DragonHP;
    }


    public void PlayerTakeDamage()
    {
        PlayerTakeDamage(-1);
    }

    public void PlayerTakeDamage(int swingSequenceId)
    {
        if (PlayerIsDead || trainingEpisodeTerminated || PlayerHP <= 0)
        {
            if (telemetry != null)
            {
                telemetry.LogDamageEvent("BossDamageRejected", "boss", -1, swingSequenceId, "BossAttack", "Player", PlayerHP, PlayerHP, DragonHP, DragonHP, trainingEpisodeTerminated ? "episode already ended" : "player already dead");
            }
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} BossDamageRejected swing={swingSequenceId} reason={(trainingEpisodeTerminated ? "episode already ended" : "player already dead")} playerHP={PlayerHP} bossHP={DragonHP}.");
#endif
            return;
        }

        int playerHpBefore = PlayerHP;
        PlayerHP = Mathf.Clamp(PlayerHP - 95, 0, 500);
        setHealth(PlayerHP);
        if (telemetry != null)
        {
            telemetry.LogDamageEvent("BossDamageApplied", "boss", -1, swingSequenceId, "BossAttack", "Player", playerHpBefore, PlayerHP, DragonHP, DragonHP);
        }
        DamageApplied?.Invoke(false, playerHpBefore - PlayerHP);
#if UNITY_EDITOR
        Debug.Log($"[CombatDiag] t={Time.time:F3} BossDamageApplied swing={swingSequenceId} playerHP={playerHpBefore}->{PlayerHP} damage=95 bossHP={DragonHP}.");
#endif

        if (PlayerHP <= 0)
        {
            PlayerIsDead = true;
            if (playerMovement != null)
            {
                playerMovement.enabled = false;
            }
#if UNITY_EDITOR
            Debug.Log($"[CombatDiag] t={Time.time:F3} PlayerDeathTransition reason=HP reached 0 playerHP={PlayerHP} bossHP={DragonHP}.");
#endif
            if (telemetry != null)
            {
                telemetry.LogEpisodeEnd("boss_win", PlayerHP, DragonHP);
            }
            FightEnded?.Invoke("boss_win");
           // SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            wait(0.08f);
        }
        
    }
    public bool EndTrainingEpisodeForTimeout()
    {
        EnemyMovement bossMovement = DragonBody != null ? DragonBody.GetComponent<EnemyMovement>() : null;
        if (bossMovement == null || !bossMovement.IsAgentControlled || trainingEpisodeTerminated ||
            PlayerIsDead || DragonIsDead || PlayerHP <= 0 || DragonHP <= 0)
        {
            return false;
        }

        trainingEpisodeTerminated = true;
        if (telemetry != null)
        {
            telemetry.LogEpisodeEnd("timeout", PlayerHP, DragonHP);
        }
        FightEnded?.Invoke("timeout");
        return true;
    }

    void setHealth(int playerHealth)
    {
        PlayerHP = Mathf.Clamp(playerHealth, 0, 500);
    }
    public int getHealth()
    {
        return PlayerHP;
    }

    

    













}
