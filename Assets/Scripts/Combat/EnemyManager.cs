using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Random = System.Random;

namespace RPGKarawara
{
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager i { get; private set; }
        [SerializeField] private CombatController player;
        [SerializeField] Vector2 timeRangeBetweenAttacks = new Vector2(1, 4);
        public List<EnemyController> enemiesInRange = new List<EnemyController>();
        private float notAttackingTimer = 2;
        private float timer = 0;

        private void Awake()
        {
            i = this;
        }

        public void AddEnemyInRange(EnemyController enemy)
        {
            if(!enemiesInRange.Contains(enemy))
                enemiesInRange.Add(enemy);
        }
        
        public void RemoveEnemyInRange(EnemyController enemy)
        {
            
                enemiesInRange.Remove(enemy);
                if (enemy == player.TargetEnemy)
                {
                    enemy.MeshHighlighter?.HighlightMesh(false);
                    player.TargetEnemy = GetClosestEnemyToPlayerDir();
                    player.TargetEnemy?.MeshHighlighter?.HighlightMesh(true);
                }
        }

        private void Update()
        {
            if(enemiesInRange.Count == 0)
                return;
            if (!enemiesInRange.Any(e => e.IsInState(EnemyStates.Attack)))
            {
                if(notAttackingTimer > 0)
                    notAttackingTimer -= Time.deltaTime;

                if (notAttackingTimer <= 0)
                {
                    var attackingEnemy = SelectEnemyForAttack();
                    if (attackingEnemy != null)
                    {
                        attackingEnemy.ChangeState(EnemyStates.Attack);
                        notAttackingTimer = UnityEngine.Random.Range(timeRangeBetweenAttacks.x, timeRangeBetweenAttacks.y);
                        
                    }
                }
                
            }

            if (timer >= 0.1f)
            {
                timer = 0f;
                var closestEnemy = GetClosestEnemyToPlayerDir();
                if (closestEnemy != null && closestEnemy != player.TargetEnemy)
                {
                    var prevEnemy = player.TargetEnemy;
                    player.TargetEnemy = closestEnemy;
                    
                    
                    player?.TargetEnemy?.MeshHighlighter.HighlightMesh(true);
                    prevEnemy?.MeshHighlighter?.HighlightMesh(false);
                }
            }
            timer += Time.deltaTime;

        }

        EnemyController SelectEnemyForAttack()
        {
            return enemiesInRange.OrderByDescending(e => e.CombatMovementTimer).FirstOrDefault(e => e.Target != null);
        }
        public EnemyController GetAttackingEnemy()
        {
            return enemiesInRange.FirstOrDefault(e => e.IsInState(EnemyStates.Attack));
        }
        public EnemyController GetClosestEnemyToPlayerDir()
        {
            //fazer para depois no scrow mudar o target
            //mudar a logica
            var targetingDir = player.GetTargetingDir();

            float minDitance = Mathf.Infinity;
            EnemyController closestEnemy = null;
            foreach (var enemy in enemiesInRange)
            {
                var vecToEnemy = enemy.transform.position - player.transform.position;
                vecToEnemy.y = 0;
                float angle = Vector3.Angle(targetingDir, vecToEnemy);
                float distance = vecToEnemy.magnitude * Mathf.Sin(angle * Mathf.Deg2Rad);


                if (distance < minDitance)
                {
                    minDitance = distance;
                    closestEnemy = enemy;
                }
            }

            return closestEnemy;


        }
    }
}
