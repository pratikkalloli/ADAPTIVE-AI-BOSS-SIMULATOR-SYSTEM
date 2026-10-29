using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RagDollManager : MonoBehaviour
{
    public GameObject player;
    public GameObject character;
    private CharacterController cc;
    private Animator anim;
    public Animator dragonAnim;

    public Collider[] ragDollColliders;
    public Rigidbody[] ragDollRigidbodies;

    private Transform[] ragdollTransforms;
    private Vector3[] initialLocalPositions;
    private Quaternion[] initialLocalRotations;

    CombatManager combatMan;
    private bool ragdollActive;
    private void Start()
    {
        //dragonAnim = GetComponent<Animator>();
        combatMan = GetComponent<CombatManager>();
        cc = player.GetComponent<CharacterController>();
        anim = character.GetComponent<Animator>();
        GetRagDollBits();
        CaptureInitialPose();
        RagdollOff();
    }

    private void Update()
    {
        if (combatMan != null && combatMan.getHealth() <= 0 && !ragdollActive)
        {
            RagdollOn();
        }
    }
    void GetRagDollBits()
    {
        ragDollColliders = character.GetComponentsInChildren<Collider>();
        ragDollRigidbodies = character.GetComponentsInChildren<Rigidbody>();
    }

    private void CaptureInitialPose()
    {
        ragdollTransforms = character.GetComponentsInChildren<Transform>(true);
        initialLocalPositions = new Vector3[ragdollTransforms.Length];
        initialLocalRotations = new Quaternion[ragdollTransforms.Length];
        for (int i = 0; i < ragdollTransforms.Length; i++)
        {
            initialLocalPositions[i] = ragdollTransforms[i].localPosition;
            initialLocalRotations[i] = ragdollTransforms[i].localRotation;
        }
    }

    public void ResetForAgentEpisode()
    {
        RagdollOff();
        for (int i = 0; i < ragDollRigidbodies.Length; i++)
        {
            ragDollRigidbodies[i].velocity = Vector3.zero;
            ragDollRigidbodies[i].angularVelocity = Vector3.zero;
            ragDollRigidbodies[i].isKinematic = true;
        }
        for (int i = 0; i < ragdollTransforms.Length; i++)
        {
            ragdollTransforms[i].localPosition = initialLocalPositions[i];
            ragdollTransforms[i].localRotation = initialLocalRotations[i];
        }
    }
    public void RagdollOn()
    {
        if (ragdollActive)
        {
            return;
        }

        ragdollActive = true;
        dragonAnim.SetBool("isPatrolling", false);
        dragonAnim.SetBool("isAttacking", false);
        dragonAnim.SetBool("isChasing", false);
        anim.enabled = false;
        cc.enabled = false;
        foreach (Collider col in ragDollColliders)
        {
            col.enabled = true;
        }

        foreach (Rigidbody rigid in ragDollRigidbodies)
        {
            rigid.isKinematic = false;
        }
    }
    public void RagdollOff()
    {
        ragdollActive = false;
        anim.enabled = true;
        cc.enabled = true;
        foreach (Collider col in ragDollColliders)
        {
            
            if(col.gameObject.name == "ToonRTS_demo_Knight" || col.gameObject.name == "Bip001 R Hand" || col.gameObject.name == "Sword_Trail" || col.gameObject.name == "CameraLookAt")
            {
                col.enabled = true;
            }
            else
            {
                col.enabled = false;
            }
        }
        foreach (Rigidbody rigid in ragDollRigidbodies)
        {
            rigid.isKinematic = true;
        }
    }
}
