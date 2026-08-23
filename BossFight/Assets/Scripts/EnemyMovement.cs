using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Cinemachine;
using Random = UnityEngine.Random;
public class EnemyMovement : MonoBehaviour
{
    public CinemachineFreeLook cinemachine;

    public Transform player;
    public Animator dragonAnim;
private float normalSpeed = 1.5f;
private float chaseMultiplier = 1f;
    private float dragonSlowSpeed = 0.3f;
    private float dragonFastSpeed = 1.5f;
    private bool finishedAttack;
    public GameObject[] trails;

    public AudioSource source;
    public AudioClip []clip;
    public float volume;

private CombatManager combatManager;
private bool enraged = false;
private bool berserk = false;

    private void Start()
{
    source.volume = volume;
    trailDeactivate();

    combatManager = GameObject.Find("GameManager")
                    .GetComponent<CombatManager>();
}
    void Update()
    {
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
if (combatManager.FailedAttacks >= 3)
{
    Debug.Log("Boss Repositioning");

    transform.position -= transform.forward * 2f;

    combatManager.FailedAttacks = 0;
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
        trailDeactivate();
        finishedAttack = false;
        dragonAnim.speed = dragonSlowSpeed;
    }
    public bool HornAttackFast()
    {
        dragonAnim.speed = dragonFastSpeed;
        return true;
    }

    public void Claw_fin()
    {
        //Debug.Log("claw_fin");
        finishedAttack= true;
    }
    public void Horn_fin()
    {
        //Debug.Log("horn_fin");
        finishedAttack= true;
    }
    public void Basic_fin()
    {
        //Debug.Log("Basic_fin");
        finishedAttack = true;
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
