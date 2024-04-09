using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara
{
    public enum AICombatStates { Idle, Chase, Circling }
    public class CombatMovementState : StateEnemy<EnemyController>
    {
        [SerializeField] float ciclingSpeed = 20f;
        [SerializeField] private float distanceToStand = 3f;
        [SerializeField] float adjustDistanceThreshold = 1f;
        [SerializeField] Vector2 idleTimeRange = new Vector2(2, 5);
        [SerializeField] Vector2 circlingTimeRange = new Vector2(3, 6);
        private EnemyController _enemyController;
        private AICombatStates _state;

        private float timer = 0;
        int circlingDir = 1;
        public override void Enter(EnemyController owner)
        
        {
            _enemyController = owner;
            _enemyController.NavAgent.stoppingDistance = distanceToStand;
            _enemyController.CombatMovementTimer = 0f;
        }

        public override void Execute()
        {
            if (Vector3.Distance(_enemyController.Target.transform.position, _enemyController.transform.position) >
                distanceToStand + adjustDistanceThreshold)
            {
                StartChase();
            }
            if (_state == AICombatStates.Idle)
            {
                if (timer <= 0)
                {
                    if (Random.Range(0, 2) == 0)
                    {
                        StartIdle();
                    }
                    else
                    {
                        StartCircling();
                    }
                }
            }else if (_state == AICombatStates.Chase)
            {
                if (Vector3.Distance(_enemyController.Target.transform.position, _enemyController.transform.position) <=
                    distanceToStand + 0.03f)
                {
                    StartIdle();
                }
                _enemyController.NavAgent.SetDestination(_enemyController.Target.transform.position);
                
            }else if (_state == AICombatStates.Circling)
            {
                if (timer <= 0)
                {
                    StartIdle();
                    return;
                    
                }
                transform.RotateAround(_enemyController.Target.transform.position, Vector3.up, ciclingSpeed * circlingDir * Time.deltaTime);

                var vecToTarget = _enemyController.transform.position - _enemyController.Target.transform.position;
                var rotatedPos = Quaternion.Euler(0, ciclingSpeed * circlingDir * Time.deltaTime, 0) * vecToTarget;
                
                
                _enemyController.NavAgent.Move(rotatedPos - vecToTarget);
                _enemyController.transform.rotation = Quaternion.LookRotation(-rotatedPos);
            }

            if (timer > 0)
            {
                timer -= Time.deltaTime;
            }

            _enemyController.CombatMovementTimer += Time.deltaTime;

        }

        void StartCircling()
        {
            _state = AICombatStates.Circling;
            _enemyController.NavAgent.ResetPath();
            timer = Random.Range(circlingTimeRange.x, circlingTimeRange.y);

            circlingDir = Random.Range(0, 2) == 0 ? 1 : -1;
        }
        void StartChase()
        {
            _state = AICombatStates.Chase;
            _enemyController.animator.SetBool("combatMode", false);
        }
        void StartIdle()
        {
            _state = AICombatStates.Idle;
            timer = Random.Range(idleTimeRange.x, idleTimeRange.y);
            _enemyController.animator.SetBool("combatMode", true);
        }
        public override void Exit()
        {
            _enemyController.CombatMovementTimer = 0f;
        }
    }
}
