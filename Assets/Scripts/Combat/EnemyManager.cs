using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

using UnityEngine.InputSystem;
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
                    player.TargetEnemy = GetClosestEnemyToDirection(player.GetTargetingDir());
                    player.TargetEnemy?.MeshHighlighter?.HighlightMesh(true);
                }
        }
        private bool hasClickedThisFrame = false;
        private void Update()
        {
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
               
                if (!hasClickedThisFrame)
                {
                    SelectNextEnemy();
                    hasClickedThisFrame = true;
                }
            }
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
            {
                hasClickedThisFrame = false;
            }
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
                var closestEnemy = GetClosestEnemyToDirection(player.GetTargetingDir());
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


        private void SelectNextEnemy()
        {
            int currentIndex = enemiesInRange.IndexOf(player.TargetEnemy);
            if (currentIndex == -1)
            {
                return;
            }

            if (currentIndex == enemiesInRange.Count - 1)
            {
                player.TargetEnemy = enemiesInRange[0];
            }
            else
            {
                player.TargetEnemy = enemiesInRange[currentIndex + 1];
            }

            UpdateTargetingVisuals();
        }
        
        private void UpdateTargetingVisuals()
        {
            foreach (var enemy in enemiesInRange)
            {
                enemy.MeshHighlighter.HighlightMesh(enemy == player.TargetEnemy);
            }
        }
        EnemyController SelectEnemyForAttack()
        {
            return enemiesInRange.OrderByDescending(e => e.CombatMovementTimer).FirstOrDefault(e => e.Target != null && e.IsInState(EnemyStates.CombatMovement));
        }
        public EnemyController GetAttackingEnemy()
        {
            return enemiesInRange.FirstOrDefault(e => e.IsInState(EnemyStates.Attack));
        }
        public EnemyController GetClosestEnemyToDirection(Vector3 direction)
        {
            //fazer para depois no scrow mudar o target
            //mudar a logica
            //var targetingDir = player.GetTargetingDir();

            float minDitance = Mathf.Infinity;
            EnemyController closestEnemy = null;
            foreach (var enemy in enemiesInRange)
            {
                var vecToEnemy = enemy.transform.position - player.transform.position;
                vecToEnemy.y = 0;
                float angle = Vector3.Angle(direction, vecToEnemy);
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
