using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace RPGKarawara
{
    public class CharacterAnimatorManager : MonoBehaviour
{
    public CharacterManager character;

    int vertical;
    int horizontal;

    [Header("Flags")]
    public bool applyRootMotion = false;

    [Header("Damage Animations")]
    public string lastDamageAnimationPlayed;

    [SerializeField] string hit_Forward_Medium_01 = "Hit_Forward_Medium_01";
    [SerializeField] string hit_Forward_Medium_02 = "Hit_Forward_Medium_02";

    [SerializeField] string hit_Backward_Medium_01 = "Hit_Backward_Medium_01";
    [SerializeField] string hit_Backward_Medium_02 = "Hit_Backward_Medium_02";

    [SerializeField] string hit_Left_Medium_01 = "Hit_Left_Medium_01";
    [SerializeField] string hit_Left_Medium_02 = "Hit_Left_Medium_02";

    [SerializeField] string hit_Right_Medium_01 = "Hit_Right_Medium_01";
    [SerializeField] string hit_Right_Medium_02 = "Hit_Right_Medium_02";
    private List<string> allHitAnimations = new List<string>();
    public List<string> forward_Medium_Damage = new List<string>();
    public List<string> backward_Medium_Damage = new List<string>();
    public List<string> left_Medium_Damage = new List<string>();
    public List<string> right_Medium_Damage = new List<string>();

    protected virtual void Awake()
    {
        character = GetComponent<CharacterManager>();

        vertical = Animator.StringToHash("Vertical");
        horizontal = Animator.StringToHash("Horizontal");
    }

    protected virtual void Start()
    {
        forward_Medium_Damage.Add(hit_Forward_Medium_01);
        forward_Medium_Damage.Add(hit_Forward_Medium_02);

        backward_Medium_Damage.Add(hit_Backward_Medium_01);
        backward_Medium_Damage.Add(hit_Backward_Medium_02);

        left_Medium_Damage.Add(hit_Left_Medium_01);
        left_Medium_Damage.Add(hit_Left_Medium_02);

        right_Medium_Damage.Add(hit_Right_Medium_01);
        right_Medium_Damage.Add(hit_Right_Medium_02);
        
        allHitAnimations.AddRange(forward_Medium_Damage);
        allHitAnimations.AddRange(backward_Medium_Damage);
        allHitAnimations.AddRange(left_Medium_Damage);
        allHitAnimations.AddRange(right_Medium_Damage);
    }

    public string GetRandomAnimationFromList(List<string> animationList)
    {
        List<string> finalList = new List<string>();

        foreach (var item in animationList)
        {
            finalList.Add(item);
        }

        finalList.Remove(lastDamageAnimationPlayed);

        for (int i = finalList.Count - 1; i > -1; i--)
        {
            if (finalList[i] == null)
            {
                finalList.RemoveAt(i);
            }
        }

        int randomValue = Random.Range(0, finalList.Count);

        return finalList[randomValue];
    }

    public void UpdateAnimatorMovementParameters(float horizontalMovement, float verticalMovement, bool isSprinting)
    {
        float snappedHorizontal;
        float snappedVertical;

        if (horizontalMovement > 0 && horizontalMovement <= 0.5f)
        {
            snappedHorizontal = 0.5f;
        }
        else if (horizontalMovement > 0.5f && horizontalMovement <= 1)
        {
            snappedHorizontal = 1;
        }
        else if (horizontalMovement < 0 && horizontalMovement >= -0.5f)
        {
            snappedHorizontal = -0.5f;
        }
        else if (horizontalMovement < -0.5f && horizontalMovement >= -1)
        {
            snappedHorizontal = -1;
        }
        else
        {
            snappedHorizontal = 0;
        }

        if (verticalMovement > 0 && verticalMovement <= 0.5f)
        {
            snappedVertical = 0.5f;
        }
        else if (verticalMovement > 0.5f && verticalMovement <= 1)
        {
            snappedVertical = 1;
        }
        else if (verticalMovement < 0 && verticalMovement >= -0.5f)
        {
            snappedVertical = -0.5f;
        }
        else if (verticalMovement < -0.5f && verticalMovement >= -1)
        {
            snappedVertical = -1;
        }
        else
        {
            snappedVertical = 0;
        }

        if (isSprinting)
        {
            snappedVertical = 2;
        }

        character.animator.SetFloat(horizontal, snappedHorizontal, 0.1f, Time.deltaTime);
        character.animator.SetFloat(vertical, snappedVertical, 0.1f, Time.deltaTime);
    }

    public virtual void PlayTargetActionAnimation(
        string targetAnimation, 
        bool isPerformingAction, 
        bool applyRootMotion = true, 
        bool canRotate = false, 
        bool canMove = false)
    {
       // Debug.Log("Hit_Forward_Medium_01");
       if (character.isBoss && allHitAnimations.Contains(targetAnimation))
       {
           Debug.Log("Boss não toma hit: " + targetAnimation);
           return; 
       }
        this.applyRootMotion = applyRootMotion;
        character.animator.CrossFade(targetAnimation, 0.2f);
        character.isPerformingAction = isPerformingAction;
        character.characterLocomotionManager.canRotate = canRotate;
        character.characterLocomotionManager.canMove = canMove;

        character.characterNetworkManager.NotifyTheServerOfActionAnimationServerRpc(NetworkManager.Singleton.LocalClientId, targetAnimation, applyRootMotion);
    }

    public virtual void PlayTargetAttackActionAnimation(AttackType attackType,
        string targetAnimation,
        bool isPerformingAction,
        bool applyRootMotion = true,
        bool canRotate = false,
        bool canMove = false)
    {
        character.characterCombatManager.currentAttackType = attackType;
        character.characterCombatManager.lastAttackAnimationPerformed = targetAnimation;
        this.applyRootMotion = applyRootMotion;
        character.animator.CrossFade(targetAnimation, 0.2f);
        character.isPerformingAction = isPerformingAction;
        character.characterLocomotionManager.canRotate = canRotate;
        character.characterLocomotionManager.canMove = canMove;

        character.characterNetworkManager.NotifyTheServerOfAttackActionAnimationServerRpc(NetworkManager.Singleton.LocalClientId, targetAnimation, applyRootMotion);
    }

   
}

}
