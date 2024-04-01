using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace RPGKarawara
{

    public enum AttackStates { Idle, Windup, Impact, Cooldown }
    public class MeeleFighter : MonoBehaviour
    {
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
                
                for(int i = 0; i < _hand.Length; i++)
                {
                   // Debug.Log(i);
                    _handCollider[i] = _hand[i].GetComponent<SphereCollider>();
                    _handCollider[i].enabled = false; 

                }
            }
        }

        
        public void TryToAttack()
        {
            if(!InAction)
            {
                //Arrumar para n andar, zerar a velocidade 
                StartCoroutine(Attack());
                
            }
        }
        public void FixedUpdate()
        {
            Debug.Log(attackState); 
        }
        IEnumerator Attack()
        {
            
            InAction = true;
            attackState = AttackStates.Windup;
            float impactStartTime = 0.33f; //Trocar quando for a animação *NOSSA*
            float impactEndTime = 0.55f;
            _animator.CrossFade("Slash", 0.2f);

            yield return null;

            var animState = _animator.GetNextAnimatorClipInfo(1);
            float timer = 0f;

            while (timer <= animState.Length)
            {
                timer += Time.deltaTime;
                float normalizedTime = timer / animState.Length;

                if (attackState == AttackStates.Windup)
                {
                    if (normalizedTime >= impactStartTime)
                    {
                        ForHandCollider(true);
                        attackState = AttackStates.Impact;

                    }
                }
                else if (attackState == AttackStates.Impact)
                {
                    if (normalizedTime >= impactEndTime)
                    {
                        Debug.Log("entrou2");
                          ForHandCollider(false);
    
                        attackState = AttackStates.Cooldown;
                    }
                }
                else if (attackState == AttackStates.Cooldown)
                {
                    // TODO: Handle Combos
                }
                yield return null;
            }       
                attackState = AttackStates.Idle;
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

            yield return new WaitForSeconds(animState.Length);

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
