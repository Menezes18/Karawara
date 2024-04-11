using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



namespace RPGKarawara
{
    public class IdleState : StateEnemy<EnemyController>
    {
        private EnemyController _enemyController;
        public override void Enter(EnemyController owner)
        {
            _enemyController = owner;
            _enemyController.animator.SetBool("combatMode", false);
        }

        public override void Execute()
        {
            foreach (var target in _enemyController.TargetInRange)
            {
                var vecToTarget = target.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, vecToTarget);
                if (angle <= _enemyController.Fov / 2)
                {
                    _enemyController.Target = target;
                    _enemyController.ChangeState(EnemyStates.CombatMovement);
                    break;
                }
            }
        }

        public override void Exit()
        {
            
        }
    }
}
