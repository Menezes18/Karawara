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
            _enemyController.Target = _enemyController.FindTarget();
            if (_enemyController.Target != null){
                _enemyController.ChangeState(EnemyStates.CombatMovement);
            }
        }

        public override void Exit()
        {
            
        }
    }
}
