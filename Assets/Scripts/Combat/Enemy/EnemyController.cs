using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    public enum EnemyStates { Idle, CombatMovement, Attack, RetreatAfterAttack, Dead, GettingHit }
    public class EnemyController : MonoBehaviour{
        public float vida = 100f;
        [field: SerializeField] public float Fov { get; private set; } = 180f;
        public List<MeeleFighter> TargetInRange { get; set; } = new List<MeeleFighter>();
        public float CombatMovementTimer { get; set; } = 0f;
        public MeeleFighter Target { get; set; }
        public StateMachineEnemy<EnemyController> StateMachineEnemy { get; private set; }

        Dictionary<EnemyStates, StateEnemy<EnemyController>> stateDict;

        public NavMeshAgent NavAgent { get; private set; }
        public CharacterController CharacterController { get; private set; }
        public Animator animator{ get; private set; }
        public MeeleFighter Fighter{ get; private set; }
        public SkinnedMeshHighlighter MeshHighlighter{ get; private set; }
        public VisionSensor VisionSensor { get; set; }
        private void Start()
        {
            CharacterController = GetComponent<CharacterController>();
            NavAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            Fighter = GetComponent<MeeleFighter>();
            MeshHighlighter = GetComponent<SkinnedMeshHighlighter>();
            stateDict = new Dictionary<EnemyStates, StateEnemy<EnemyController>>();
            stateDict[EnemyStates.Idle] = GetComponent<IdleState>();
            stateDict[EnemyStates.CombatMovement] = GetComponent<CombatMovementState>();
            stateDict[EnemyStates.Attack] = GetComponent<AttackState>();
            stateDict[EnemyStates.RetreatAfterAttack] = GetComponent<RetreatAfterAttackState>();
            stateDict[EnemyStates.Dead] = GetComponent<DeadState>();
            stateDict[EnemyStates.GettingHit] = GetComponent<GettingHitState>();
            
            StateMachineEnemy = new StateMachineEnemy<EnemyController>(this);
            StateMachineEnemy.ChangeState(stateDict[EnemyStates.Idle]);

            Fighter.OnGotHit += () => ChangeState(EnemyStates.GettingHit);
        }
        
        public void ChangeState(EnemyStates state)
        {
            StateMachineEnemy.ChangeState(stateDict[state]);
        }
        public bool IsInState(EnemyStates state)
        {
            return StateMachineEnemy.CurrentState == stateDict[state];
        }
        Vector3 prevPos;
        private void Update()
        {
            StateMachineEnemy.Execute();
            
            // v = dx / dt
            var deltaPos = animator.applyRootMotion? Vector3.zero : transform.position - prevPos;
           var velocity = deltaPos / Time.deltaTime;

            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            animator.SetFloat("forwardSpeed", forwardSpeed / NavAgent.speed, 0.2f, Time.deltaTime);

            float angle = Vector3.SignedAngle(transform.forward, velocity, Vector3.up);
            float strafeSpeed = Mathf.Sin(angle * Mathf.Deg2Rad);
            animator.SetFloat("strafeSpeed", strafeSpeed, 0.2f, Time.deltaTime);

            prevPos = transform.position;
            //animator.SetFloat("forwardSpeed", NavAgent.velocity.magnitude / NavAgent.speed);
        }
        public MeeleFighter FindTarget()
        {
            foreach (var target in TargetInRange)
            {
                var vecToTarget = target.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, vecToTarget);

                if (angle <= Fov / 2)
                {
                    return target;
                }
            }

            return null;
        }
    }
}
