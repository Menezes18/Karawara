using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AttackState : StateEnemy<EnemyController>
    {
        [SerializeField] float attackDistance = 1f;
        bool isAttacking;
        private EnemyController _enemyController;
        public override void Enter(EnemyController owner)
        {
            _enemyController = owner;
            _enemyController.NavAgent.stoppingDistance = attackDistance;
        }
        public override void Execute()
        {
            if(isAttacking) return;
            _enemyController.NavAgent.SetDestination(_enemyController.Target.transform.position);
            if (Vector3.Distance(_enemyController.Target.transform.position, _enemyController.transform.position) <=
                attackDistance + 0.03f)
            {
                StartCoroutine(Attack(Random.Range(0, _enemyController.Fighter.Attacks.Count + 1)));
            }

        }

        IEnumerator Attack(int comboCount = 1)
        {
            isAttacking = true;
            _enemyController.animator.applyRootMotion = true;
            
            _enemyController.Fighter.TryToAttack();
            for (int i = 1; i < comboCount; i++)
            {
                yield return new WaitUntil(() => _enemyController.Fighter._attackStates == AttackStates.Cooldown);
                _enemyController.Fighter.TryToAttack();
                // _enemyController.Fighter.TryToAttack(_enemyController.Target);
            }
            yield return new WaitUntil(() => _enemyController.Fighter._attackStates == AttackStates.Idle);
            
            _enemyController.animator.applyRootMotion = false;
            isAttacking = false;
            if(_enemyController.IsInState(EnemyStates.Attack))
                _enemyController.ChangeState(EnemyStates.RetreatAfterAttack);
        }

        public override void Exit()
        {
            _enemyController.NavAgent.ResetPath();
        }
    }
}
