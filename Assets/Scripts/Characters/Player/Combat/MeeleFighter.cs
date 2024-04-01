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
        [SerializeField] GameObject[] _hand;
        [SerializeField] SphereCollider[] _handCollider;
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
                _handCollider = new SphereCollider[_hand.Length];
                for (int i = 0; i < _hand.Length; i++)
                {
                   // Debug.Log(i);
                    _handCollider[i] = _hand[i].GetComponent<SphereCollider>();
                    _handCollider[i].enabled = false; 

                }
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
                        ForHandCollider(true);
                        attackState = AttackStates.Impact;

                    }
                }
                else if (attackState == AttackStates.Impact)
                {
                    if (normalizedTime >= attacks[ComboCount].ImpactEndTime)
                    {
                        
                          ForHandCollider(false);
    
                        attackState = AttackStates.Cooldown;
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
                Debug.Log("BATEU");
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

        private void ForHandCollider(bool condicao)
        {
            for (int i = 0; i < _hand.Length; i++)
            {
                _handCollider[i].enabled = condicao;
            }
        }
    }
}
