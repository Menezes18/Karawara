using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;


public class BossAISystem : MonoBehaviour {
    public GameObject projectilePrefab; // O prefab do projétil
    public Transform firePoint; // Ponto de origem dos tiros
    public float range = 10f; // Distância de detecção do player
    public float heightOffset = 1f; // Altura inicial do projétil
    public int maxShots = 3; // Número máximo de tiros permitidos
    public float shotCooldown = 2f; // Cooldown entre os tiros
    public float minDistanceBetweenShots = 1.5f; // Distância mínima entre os projéteis
    public float minDistanceFromPlayer = 3f; // Distância mínima do player

    private int currentShots = 0; // Tiros disparados atualmente
    private float cooldownTimer = 0f; // Timer para o cooldown dos tiros
    private bool canFire = false;

    public float moveDistance = 2.0f; // Distância de recuo do boss
    public NavMeshAgent agent; // Agente de navegação
    public float rotationRadius = 5f; // Distância para o boss orbitar ao redor do player
    public float rotationSpeed = 2f; // Velocidade de órbita
    private float angle = 0f;

    public float randomMoveInterval = 5f; // Intervalo para mudar de direção
    public float idleTime = 2f; // Tempo em que o boss para
    private float nextMoveTime = 0f; // Quando o boss mudará de movimento
    private bool isMoving = true; // Flag para controle de movimento

    private Transform playerTransform; // Cache do player
    private List<Vector3> previousPositions = new List<Vector3>(); // Armazena as posições dos projéteis

    private void Start() {
        agent = GetComponent<NavMeshAgent>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            playerTransform = player.transform;
        }

        nextMoveTime = Time.time + randomMoveInterval;
    }

    private void Update() {
        if (cooldownTimer > 0) {
            cooldownTimer -= Time.deltaTime;
        }

        if (playerTransform == null) {
            Debug.LogWarning("Player não encontrado.");
            return;
        }

        if (canFire){

            
        }
       
        HandleShooting();
    }

    void HandleShooting() {
        // Atirar projéteis se o player estiver ao alcance
        if (canFire && currentShots < maxShots && cooldownTimer <= 0 && IsPlayerInRange()) {
            Vector3 targetPosition = GetPlayerPosition();
            if (CanShoot(targetPosition)) {
                ShootProjectile(targetPosition);
                currentShots++;
                cooldownTimer = shotCooldown;

                if (currentShots >= maxShots) {
                    StartCoroutine(ShotCooldown());
                }
            }
        }

        // Voltar a uma posição mais baixa após atirar
        // if (currentShots >= maxShots) {
        //     // Aqui você pode definir uma posição desejada para o boss voltar, por exemplo, na altura do chão
        //     Vector3 lowerPosition = new Vector3(transform.position.x, 0f, transform.position.z); // Ajuste a altura conforme necessário
        //     agent.SetDestination(lowerPosition);
        //     isMoving = true; // Permitir movimento após o disparo
        // }
    }

    void OrbitAroundPlayer() {
        // Calcular a posição desejada ao redor do player, mantendo a distância mínima
        Vector3 direction = (transform.position - playerTransform.position).normalized;
        Vector3 targetPosition = playerTransform.position + direction * (rotationRadius + minDistanceFromPlayer);

        // Definir o destino no NavMesh para orbitar o player
        agent.SetDestination(targetPosition);

        // Rotacionar para olhar para o player
        Vector3 lookDirection = (playerTransform.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(lookDirection.x, 0, lookDirection.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f);
    }

    void StopMovement() {
        // Parar o boss no local atual
        agent.SetDestination(transform.position);
    }

    private bool IsPlayerInRange() {
        // Verifica se o player está dentro do alcance
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, range);
        foreach (var hitCollider in hitColliders) {
            if (hitCollider.CompareTag("Player")) {
                return true;
            }
        }
        return false;
    }

    private Vector3 GetPlayerPosition() {
        return playerTransform.position;
    }

    private void ShootProjectile(Vector3 targetPosition) {
        // Dispara o projétil na direção do player
        Vector3 spawnPosition = firePoint.position + Vector3.up * heightOffset;
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        previousPositions.Add(spawnPosition);

        // Limita o número de projéteis anteriores armazenados
        if (previousPositions.Count > maxShots) {
            previousPositions.RemoveAt(0);
        }
    }

    private bool CanShoot(Vector3 targetPosition) {
        // Verifica se o novo projétil está a uma distância mínima dos projéteis anteriores
        foreach (var pos in previousPositions) {
            if (Vector3.Distance(pos, targetPosition) < minDistanceBetweenShots) {
                return false; // Não pode disparar se estiver muito próximo
            }
        }
        return true; // Pode disparar se não houver nenhum projétil próximo
    }

    IEnumerator ShotCooldown() {
        // Controla o cooldown entre tiros
        yield return new WaitForSeconds(shotCooldown);
        currentShots = 0;
        canFire = false;
    }

    void OnDrawGizmosSelected() {
        // Desenha um gizmo para mostrar o alcance de detecção
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
