using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    [SerializeField] Vector2 timeRangeBetweenAttacks = new Vector2(1, 4);

    [SerializeField] CombatController player;

    public static EnemyManager i { get; private set; }
    private void Awake()
    {
        i = this;
    }

    List<EnemyController> enemiesInRange = new List<EnemyController>();
    float notAttackingTimer = 2;

    public void AddEnemyInRange(EnemyController enemy)
    {
        if (!enemiesInRange.Contains(enemy))
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

    float timer = 0f;
    private void Update()
    {
        if (enemiesInRange.Count == 0) return;

        if (!enemiesInRange.Any(e => e.IsInState(EnemyStates.Attack)))
        {
            if (notAttackingTimer > 0)
                notAttackingTimer -= Time.deltaTime;

            if (notAttackingTimer <= 0)
            {
                // Attack the player
                var attackingEnemy = SelectEnemyForAttack();

                if (attackingEnemy != null)
                {
                    attackingEnemy.ChangeState(EnemyStates.Attack);
                    notAttackingTimer = Random.Range(timeRangeBetweenAttacks.x, timeRangeBetweenAttacks.y);
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
        float minDistance = Mathf.Infinity;
        EnemyController closestEnemy = null;

        foreach (var enemy in enemiesInRange)
        {
            var vecToEnemy = enemy.transform.position - player.transform.position;
            vecToEnemy.y = 0;

            // Distance to the targetingDir line will be v * sin(theta)
            float angle = Vector3.Angle(direction, vecToEnemy);
            float distance = vecToEnemy.magnitude * Mathf.Sin(angle * Mathf.Deg2Rad);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = enemy;
            }
        }

        return closestEnemy;
    }
}
