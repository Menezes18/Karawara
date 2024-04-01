using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RPGKarawara
{

    public enum AttackStates { Idle, Windup, Impact, Cooldown }
    public class MeeleFighter : MonoBehaviour
    {
        [SerializeField] List<AttackData> attacks;
        [SerializeField] GameObject _hand;
         SphereCollider leftHandCollider, rightHandCollider, leftFootCollider, rightFootCollider;
        public static MeeleFighter instance;
        Animator _animator;
        public bool InAction { get; set; } = false;
        public AttackStates attackState;
        public PlayerMovementStateMachine movementStateMachine;
        private void Awake()
        {
            instance = this;
            _animator = GetComponentInChildren<Animator>();
            
        }

        private void Start()
        {
            if(_hand != null)
            {
                leftHandCollider = _animator.GetBoneTransform(HumanBodyBones.LeftHand).GetComponentInChildren<SphereCollider>();
                rightHandCollider = _animator.GetBoneTransform(HumanBodyBones.RightHand).GetComponentInChildren<SphereCollider>();
                leftFootCollider = _animator.GetBoneTransform(HumanBodyBones.LeftFoot).GetComponentInChildren<SphereCollider>();
                rightFootCollider = _animator.GetBoneTransform(HumanBodyBones.RightFoot).GetComponentInChildren<SphereCollider>();

                DesativarAllHitboxes();

            }
        }

        bool doCombo;
        int ComboCount = 0;
        public void TryToAttack()
        {
            if(!InAction)
            {
                
                StartCoroutine(Attack());
                
            }else if(attackState == AttackStates.Impact || attackState == AttackStates.Cooldown)
            {
                doCombo = true;
            }
        }
        IEnumerator Attack()
        {
            
            InAction = true;
            attackState = AttackStates.Windup;
            _animator.CrossFade(attacks[ComboCount].AnimName, 0.2f);

            yield return null;

            var animState = _animator.GetNextAnimatorClipInfo(1);
            float timer = 0f;


            while (timer <= animState.Length)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / animState.Length;

                if (attackState == AttackStates.Windup)
                {
                    if (normalizedTime >= attacks[ComboCount].ImpactStartTime)
                    {
                        attackState = AttackStates.Impact;
                        AtivarHitbox(attacks[ComboCount]);

                    }
                }
                else if (attackState == AttackStates.Impact)
                {
                    if (normalizedTime >= attacks[ComboCount].ImpactEndTime)
                    {
                        attackState = AttackStates.Cooldown;
                        DesativarAllHitboxes();
                    }
                }
                else if (attackState == AttackStates.Cooldown)
                {
                   if(doCombo)
                    {
                        doCombo = false;
                        ComboCount = (ComboCount + 1) % attacks.Count;

                        StartCoroutine(Attack());
                        yield break;
                    }
                }
                yield return null;
            }       
                attackState = AttackStates.Idle;
                ComboCount = 0;
                InAction = false;
        }
        private void OnTriggerEnter(Collider other)
        {
            if(other.tag == "Hitbox" && !InAction)
            {
                
                StartCoroutine(PlayerHitReaction());
            }
        }
        IEnumerator PlayerHitReaction()
        {
            InAction = true;
            _animator.CrossFade("SwordImpact", 0.2f);

            yield return null;

            var animState = _animator.GetNextAnimatorClipInfo(1);

            yield return new WaitForSeconds(0.1f);

            InAction = false;
        }
        void AtivarHitbox(AttackData attack)
        {
            switch (attack.HitboxToUse)
            {
                case AttackHitbox.LeftHand:
                    leftHandCollider.enabled = true;
                    break;
                case AttackHitbox.RightHand:
                    rightHandCollider.enabled = true;
                    break;
                case AttackHitbox.LeftFoot:
                    leftFootCollider.enabled = true;
                    break;
                case AttackHitbox.RightFoot:
                    rightFootCollider.enabled = true;
                    break;
                default:
                    break;
            }
        }
        private void DesativarAllHitboxes()
        {
            leftHandCollider.enabled = false;
            rightHandCollider.enabled = false;
            leftFootCollider.enabled = false;
            rightFootCollider.enabled = false;
        }
    }
}
