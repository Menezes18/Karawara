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
        private void Update()
        {
            StateMachine.Execute();

            Animator.SetFloat("forwardSpeed", NavAgent.velocity.magnitude / NavAgent.speed);
        }
    }
}
