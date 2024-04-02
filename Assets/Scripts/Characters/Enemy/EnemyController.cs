using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    public enum EnemyStates { Idle, CombatMovement, Attack, RetreatAfterAttack, Dead, GettingHit }
    public class EnemyController : MonoBehaviour
    {
        [field: SerializeField] public float Fov { get; private set; } = 180f;
        public float CombatMovementTimer { get; set; } = 0f;
        [SerializeField] public List<MeeleFighter> TargetsInRange { get; set; } = new List<MeeleFighter>();
        public MeeleFighter Target { get; set; }
        public StateMachine<EnemyController> StateMachine { get; private set; }
        Dictionary<EnemyStates, State<EnemyController>> stateDict;
        public Animator Animator { get; private set; }
        public NavMeshAgent NavAgent { get; private set; }
        private void Start()
        {
            Animator = GetComponent<Animator>();
            NavAgent = GetComponent<NavMeshAgent>();
            stateDict = new Dictionary<EnemyStates, State<EnemyController>>();
            stateDict[EnemyStates.Idle] = GetComponent<IdleState>();
            stateDict[EnemyStates.CombatMovement] = GetComponent<CombatMovementState>();


            StateMachine = new StateMachine<EnemyController>(this);
            StateMachine.ChangeState(stateDict[EnemyStates.Idle]);
        }
        public void ChangeState(EnemyStates state)
        {
            StateMachine.ChangeState(stateDict[state]);
        }
        Vector3 prevPos;
        private void Update()
        {
            StateMachine.Execute();

            // v = dx / dt
            var deltaPos = Animator.applyRootMotion ? Vector3.zero : transform.position - prevPos;
            var velocity = deltaPos / Time.deltaTime;

            float forwardSpeed = Vector3.Dot(velocity, transform.forward);
            Animator.SetFloat("forwardSpeed", forwardSpeed / NavAgent.speed, 0.2f, Time.deltaTime);

            float angle = Vector3.SignedAngle(transform.forward, velocity, Vector3.up);
            float strafeSpeed = Mathf.Sin(angle * Mathf.Deg2Rad);
            Animator.SetFloat("strafeSpeed", strafeSpeed, 0.2f, Time.deltaTime);

            prevPos = transform.position;
        }
        public MeeleFighter FindTarget()
        {
            foreach (var target in TargetsInRange)
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
