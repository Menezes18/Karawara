using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireballShooter : MonoBehaviour
{
    public GameObject fireballPrefab; 
    public Transform fireballSpawnPoint;
    public float launchForce = 5f; // Força de lançamento (ajustada para controlar a velocidade)
    public float arcHeight = 2f; // Altura da parábola
    public float detectionRadius = 10f; // Raio de detecção dos inimigos
    public LayerMask enemyLayer; 
    public string enemyTag = "Inimigo"; 
    public float fireballCooldown = 2f; // Tempo de espera entre disparos
    public Animator _loboAnim;
    private Transform targetEnemy;
    readonly HashSet<GameObject> ownedFireballs = new HashSet<GameObject>();
    private float nextFireTime = 0f; // Controla quando pode disparar novamente
    
    void Update()
    {
        DetectEnemies();

        if (targetEnemy != null && Time.time >= nextFireTime) // Verifica o cooldown
        {
            ShootFireball(targetEnemy);
            nextFireTime = Time.time + fireballCooldown; 
        }
    }

    void DetectEnemies()
    {
        Collider[] enemiesInRange = Physics.OverlapSphere(transform.position, detectionRadius);
        float closestDistance = Mathf.Infinity;
        targetEnemy = null;

        foreach (Collider enemy in enemiesInRange)
        {
            if (enemy.CompareTag(enemyTag)) 
            {
                float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);

                if (distanceToEnemy < closestDistance)
                {
                    closestDistance = distanceToEnemy;
                    targetEnemy = enemy.transform; // Seleciona o inimigo mais próximo
                }
            }
        }
    }

    void ShootFireball(Transform target)
    {
        
        GameObject fireball = Instantiate(fireballPrefab, fireballSpawnPoint.position, Quaternion.identity);
        ownedFireballs.Add(fireball);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        RPGKarawara.DevTools.Performance.PerformanceSkillRetentionSources.ProjectileCreated(this, fireballPrefab, fireball);
#endif

        // Calcula a direção do alvo e começa a trajetória parabólica
        StartCoroutine(LaunchFireball(fireball, target));
    }

    IEnumerator LaunchFireball(GameObject fireball, Transform target)
    {
        try
        {
            Vector3 startPos = fireball.transform.position;
            Vector3 targetPos = target.position;
            float time = 0;
            _loboAnim.SetTrigger("Fire");
            float duration = Vector3.Distance(startPos, targetPos) / launchForce;
            while (time < 1)
            {
                if (fireball == null || target == null) yield break;
                time += Time.deltaTime / duration;
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, time);
                float arc = Mathf.Sin(time * Mathf.PI) * arcHeight;
                currentPos.y += arc;
                fireball.transform.position = currentPos;
                yield return null;
            }
            // Preserve a real physics step at the authored endpoint before release.
            yield return new WaitForFixedUpdate();
            yield return null;
        }
        finally { ReleaseFireball(fireball); }
    }

    void ReleaseFireball(GameObject fireball)
    {
        if (!ownedFireballs.Remove(fireball)) return;
        if (fireball != null) Destroy(fireball);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        foreach (var fireball in ownedFireballs) if (fireball != null) Destroy(fireball);
        ownedFireballs.Clear();
    }

    void OnDestroy() => OnDisable();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
