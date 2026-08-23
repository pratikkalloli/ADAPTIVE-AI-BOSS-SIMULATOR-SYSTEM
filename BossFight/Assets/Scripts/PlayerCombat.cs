using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public Animator PlayerAnim;
    public GameObject playerGameObject;

    private bool canAttack = false;

    PlayerMovement playermovement;
    CombatManager combatManager;

    private AudioSource source;
    public AudioClip clip;

    private DataLogger logger;

    private void Start()
    {
        source = GetComponent<AudioSource>();

        if (source != null)
        {
            source.clip = clip;
        }

        playermovement = playerGameObject.GetComponent<PlayerMovement>();
        combatManager = GameObject.Find("GameManager").GetComponent<CombatManager>();

        logger = FindObjectOfType<DataLogger>();
    }

    private void Update()
    {
        PlayerAttack();
        LogExtraActions();
    }

    private bool CheckAnimation()
    {
        if (playermovement.PlayerAttack() &&
            (!PlayerAnim.GetCurrentAnimatorStateInfo(1).IsName("attack") &&
             !PlayerAnim.GetCurrentAnimatorStateInfo(1).IsName("attack_2")))
        {
            return true;
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            canAttack = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            canAttack = false;
        }
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

        // Temporary HP values
        int playerHP = 100;
        int bossHP = 100;

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
        if (CheckAnimation() && canAttack)
        {
            if (source != null)
            {
                source.Play();
                source.volume = 0.2f;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            bool jump = Input.GetKey(KeyCode.Space);
            bool sprint = Input.GetKey(KeyCode.LeftShift);
            bool block = Input.GetMouseButton(1);

            // Temporary HP values
            int playerHP = 100;
            int bossHP = 100;

            if (logger != null)
            {
                logger.LogPlayerState(
                    "Attack",
                    h,
                    v,
                    jump,
                    block,
                    sprint,
                    playerHP,
                    bossHP
                );
            }

            combatManager.DragonTakeDamage();
        }
    }
}