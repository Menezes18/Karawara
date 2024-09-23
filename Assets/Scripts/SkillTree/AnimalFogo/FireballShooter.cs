using System.Collections;
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

        // Calcula a direção do alvo e começa a trajetória parabólica
        StartCoroutine(LaunchFireball(fireball, target));
    }

    IEnumerator LaunchFireball(GameObject fireball, Transform target)
    {
        Vector3 startPos = fireball.transform.position;
        Vector3 targetPos = target.position;
        float time = 0;
        _loboAnim.SetTrigger("Fire");
        
        // A velocidade de lançamento é inversamente proporcional à duração
        float duration = Vector3.Distance(startPos, targetPos) / launchForce; // Ajuste da duração baseado na distância e força

        while (time < 1)
        {
            if (fireball == null || target == null)
            {
                yield break; // Sai da coroutine se algum dos objetos foi destruído
            }

            // Lerp entre o ponto de origem e o alvo
            time += Time.deltaTime / duration; // A velocidade agora depende da força
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, time);

            // Adiciona o efeito de arco para a parábola
            float arc = Mathf.Sin(time * Mathf.PI) * arcHeight;
            currentPos.y += arc;

            fireball.transform.position = currentPos;

            yield return null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
