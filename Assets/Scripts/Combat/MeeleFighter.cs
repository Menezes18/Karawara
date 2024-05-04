using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara{
    public enum AttackStates{
        Idle,
        Windup,
        Impact,
        Cooldown
    }

    public class MeeleFighter : MonoBehaviour{
        [SerializeField] private List<AttackData> attacks;
        [SerializeField] private List<AttackData> longRangeAttacks;
        [SerializeField] private float longRangeAttacksThreshold = 1.5f;
        [SerializeField] private GameObject swordOrHand;
        private BoxCollider _swordOrHandSphereCollider;
        [SerializeField] SphereCollider leftHandCollider, rightHandCollider, leftFootCollider, rightFootCollider;
        public Animator _animator;
        [SerializeField] private float rotationspeed = 500f;
        public event Action OnGotHit;
        public event Action OnHitComplete;
        public bool InAction{ get; private set; } = false;
        public bool InCounter{ get; set; } = false;
        public bool moving = false;
        
        public AttackStates _attackStates{ get; private set; }
        private bool _doCombo;
        private int _comboCount = 0;

       // public bool _stopAttack = false

       private void Start(){
            if (swordOrHand != null){
                _swordOrHandSphereCollider = swordOrHand.GetComponent<BoxCollider>();
                leftHandCollider = _animator.GetBoneTransform(HumanBodyBones.LeftHand)
                    .GetComponentInChildren<SphereCollider>();
                rightHandCollider = _animator.GetBoneTransform(HumanBodyBones.RightHand)
                    .GetComponentInChildren<SphereCollider>();
                leftFootCollider = _animator.GetBoneTransform(HumanBodyBones.LeftFoot)
                    .GetComponentInChildren<SphereCollider>();
                rightFootCollider = _animator.GetBoneTransform(HumanBodyBones.RightFoot)
                    .GetComponentInChildren<SphereCollider>();
                DisableAllHitboxes();
            }
        }
        private void Awake(){
            _animator = GetComponentInChildren<Animator>();
        }

        public void TryToAttack(MeeleFighter target = null){
           // if (_stopAttack) return;
            if (!InAction){
                StartCoroutine(Attack(target));
            }
            else if (_attackStates == AttackStates.Impact || _attackStates == AttackStates.Cooldown){
                _doCombo = true;
            }
        }

        public void Update()
        {
            if (moving){
                AttackLong();
            } 
        }

        private void AttackLong(){
           
            //attack = longRangeAttacks[0];
            Vector3 targetPosition = Player.instancia._combatController.TargetEnemy.transform.position;
            Vector3 newPosition = Vector3.MoveTowards(transform.position, targetPosition, 2f * Time.fixedDeltaTime);

            //Debug.Log("Posi��o atual: " + transform.position);
            //Debug.Log("Posi��o alvo: " + targetPosition);


            this.transform.position = newPosition;
            if (Vector3.Distance(transform.position, targetPosition) <= 0.8f){
                resetvelocitycombat();
                moving = false;
            }
        }
        protected void resetvelocitycombat()
        {
            Vector3 playervelocity = Player.instancia.Rigidbody.velocity;

            playervelocity.x = 0f;
            playervelocity.z = 0f;
            Player.instancia.Rigidbody.velocity = playervelocity;
        }
        IEnumerator Attack(MeeleFighter target = null){
            InAction = true;
            Debug.Log("ATAQUE");
            _attackStates = AttackStates.Windup;

            var attack = attacks[_comboCount];

            var attackDir = transform.forward;
            if (target != null){
                var vecToTarget = target.transform.position - transform.position;
                vecToTarget.y = 0;

                attackDir = vecToTarget.normalized;

                float distance = vecToTarget.magnitude;

                if (distance > longRangeAttacksThreshold){
                    attack = longRangeAttacks[0];
                    moving = true;
                }
                
            }

            _animator.CrossFade(attack.AnimName, 0.2f);

            yield return null;

            var animState = _animator.GetNextAnimatorStateInfo(1);

            float timer = 0f;
            while (timer <= animState.length){
                timer += Time.deltaTime;
                float normalizedTime = timer / animState.length;

                if (attackDir != null){
                   
                    if (Player.instancia._combatController.TargetEnemy != null){
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(Player.instancia._combatController.TargetEnemy.transform.position - transform.position), rotationspeed * Time.deltaTime);
                        
                    }

                }

                if (_attackStates == AttackStates.Windup){
                    if (InCounter) break;
                    if (normalizedTime >= attack.ImpactStartTime){
                        _attackStates = AttackStates.Impact;
                        EnableHitbox(attack);
                    }
                }
                else if (_attackStates == AttackStates.Impact){
                    if (normalizedTime >= attack.ImpactEndTime){
                        _attackStates = AttackStates.Cooldown;
                        DisableAllHitboxes();
                    }
                }
                else if (_attackStates == AttackStates.Cooldown){
                    if (_doCombo){
                        _doCombo = false;
                        _comboCount = (_comboCount + 1) % attacks.Count;
                        StartCoroutine(Attack());
                        yield break;
                    }
                }

                yield return null;
            }

            _attackStates = AttackStates.Idle;
            _comboCount = 0;
            InAction = false;
        }

        private void OnTriggerEnter(Collider other){
            if (other.CompareTag("Hitbox") && !InAction){
                //StartCoroutine(Hit());
                
                //Debug.Log(other);
                StartCoroutine(PlayHitReaction(other.GetComponentInParent<MeeleFighter>().transform));
            }
        }
        // IEnumerator Hit(){
        //     _stopAttack = true;
        //     yield return new WaitForSeconds(0.1f);
        //     _stopAttack = false;
        //
        //
        //
        // }
        IEnumerator PlayHitReaction(Transform attacker){
            InAction = true;
            Debug.Log(("HIT"));
            var dispVec = attacker.position - transform.position;
            dispVec.y = 0;
            transform.rotation = Quaternion.LookRotation(dispVec);
            
            OnGotHit?.Invoke();
            _animator.CrossFade("SwordImpact", 0.2f);

            yield return null;

            var animState = _animator.GetNextAnimatorStateInfo(1);

            yield return new WaitForSeconds(0.1f);
            
            OnHitComplete?.Invoke();
            
            
            InAction = false;
        }

        public IEnumerator PerformCounterAttack(EnemyController opponent){
            InAction = true;

            InCounter = true;
            opponent.Fighter.InCounter = true;
            opponent.ChangeState(EnemyStates.Dead);

            var dispVec = opponent.transform.position - transform.position;
            dispVec.y = 0f;
            transform.rotation = Quaternion.LookRotation(dispVec);
            opponent.transform.rotation = Quaternion.LookRotation(-dispVec);

            var targetPos = opponent.transform.position - dispVec.normalized * 1f;
            _animator.CrossFade("CounterAttack", 0.2f);
            opponent.animator.CrossFade("CounterAttackVictim", 0.2f);
            yield return null;

            var animState = _animator.GetNextAnimatorStateInfo(1);
            float timer = 0f;
            while (timer <= animState.length){
                transform.position = Vector3.MoveTowards(transform.position, targetPos, 5 * Time.deltaTime);

                yield return null;

                timer += Time.deltaTime;
            }

            InCounter = false;
            opponent.Fighter.InCounter = false;
            InAction = false;
        }

        void EnableHitbox(AttackData attack){
            switch (attack.HitboxToUse){
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
                case AttackHitbox.Sword:
                    _swordOrHandSphereCollider.enabled = true;
                    break;
                default:
                    break;
            }
        }

        void DisableAllHitboxes(){
            if (_swordOrHandSphereCollider != null)
                _swordOrHandSphereCollider.enabled = false;
            if (leftHandCollider != null)
                leftHandCollider.enabled = false;
            if (rightHandCollider != null)
                rightHandCollider.enabled = false;
            if (leftFootCollider != null)
                leftFootCollider.enabled = false;
            if (rightFootCollider != null)
                rightFootCollider.enabled = false;
        }

        public List<AttackData> Attacks => attacks;

        public bool IsCounterable => _attackStates == AttackStates.Windup && _comboCount == 0;
    }
}