using System.Collections;
using System.Collections.Generic;
using RPGKarawara;
using UnityEngine;

namespace RPGKarawara{
    public class GettingHitState : StateEnemy<EnemyController>{
        [field: SerializeField] float stunnTime = 0.5f;

        EnemyController enemy;

        public override void Enter(EnemyController owner){
            //StopAllCoroutines();
            Debug.Log("OU STUNN");
            enemy = owner;
            enemy.Fighter.OnHitComplete += () => StartCoroutine(GoToCombatMovement());
        }

        IEnumerator GoToCombatMovement(){
            yield return new WaitForSeconds(stunnTime);
            enemy.ChangeState(EnemyStates.CombatMovement);
        }
    }
}