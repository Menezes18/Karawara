using UnityEngine;
using UnityEngine.AI;

public class WolfAI : MonoBehaviour
{
    public Transform player; // Referência ao transform do player
    public float followDistance = 6.87f; // Distância mínima para seguir o player
    public float enemyDetectionRange = 10f; // Distância para detectar inimigos
    public float attackRange = 2f; // Distância para começar a atacar
    public int damage = 10; // Dano causado por ataque
    public float attackCooldown = 1f; // Tempo de espera entre ataques
    private Transform target; // Alvo atual do lobo (inimigo ou player)
    private float lastAttackTime; // Tempo do último ataque realizado
    private Animator animator; // Referência ao componente Animator
    public NavMeshAgent agent; // Referência ao NavMeshAgent
    public Collider colliderAttack;

    void Start()
    {
        GameObject playerGameObject = GameObject.FindGameObjectWithTag("Player");
        player = playerGameObject.transform;
        animator = GetComponent<Animator>(); // Inicializa o Animator
        agent = GetComponent<NavMeshAgent>(); // Inicializa o NavMeshAgent
        target = player; // Define o player como alvo inicial
    }

    void Update()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position); // Calcula a distância até o player

        // Se o player estiver fora da distância mínima, seguir o player
        if (distanceToPlayer > followDistance)
        {
            MoveTowards(player);
        }
        else
        {
            
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, enemyDetectionRange);
            Transform closestEnemy = null;
            float closestDistanceToPlayer = float.MaxValue; // Inicializa com o maior valor possível

            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Inimigo"))
                {
                    float distanceToEnemy = Vector3.Distance(player.position, hitCollider.transform.position); // Calcula a distância do inimigo ao player
                    
                    // Verifica se o inimigo é o mais próximo
                    if (distanceToEnemy < closestDistanceToPlayer)
                    {
                        closestDistanceToPlayer = distanceToEnemy;
                        closestEnemy = hitCollider.transform;
                    }
                }
            }

            // Se um inimigo for encontrado
            if (closestEnemy != null)
            {
                target = closestEnemy; // Define o inimigo mais próximo ao jogador como alvo
                float distanceToEnemy = Vector3.Distance(transform.position, target.position); // Calcula a distância até o inimigo

                // Se o inimigo estiver fora do alcance de ataque, mover-se em direção a ele
                if (distanceToEnemy > attackRange)
                {
                    MoveTowards(target);
                }
                else
                {
                    LookAtTarget(target); // Olhar para o inimigo
                    AttackTarget(); // Atacar o inimigo
                }
            }
            else
            {
                colliderAttack.enabled = false;
                animator.SetBool("isMoving", false);
                animator.SetTrigger("Idle");
            }
        }
    }

    void MoveTowards(Transform target)
    {
        animator.SetBool("isMoving", true); 
        agent.SetDestination(target.position); // Define o destino do NavMeshAgent para o alvo
    
        LookAtTarget(target); // Faz o lobo olhar para o alvo enquanto se move
    }

    // Função para fazer o lobo olhar para o alvo
    void LookAtTarget(Transform target)
    {
        Vector3 direction = (target.position - transform.position).normalized; // Calcula a direção até o alvo
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z)); // Calcula a rotação necessária
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); // Rotaciona suavemente em direção ao alvo
    }

    // Função para atacar o alvo
    void AttackTarget()
    {
        // Verifica se o tempo de cooldown já passou desde o último ataque
        if (Time.time - lastAttackTime >= attackCooldown){
            colliderAttack.enabled = true;
            animator.SetTrigger("Attack"); 
            Debug.Log("Atacando " + target.name + " e causando " + damage + " de dano."); 
            lastAttackTime = Time.time; 
        }
    }

    // Função para desenhar gizmos no editor, ajudando a visualizar os ranges
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, followDistance); 

        Gizmos.color = Color.red; 
        Gizmos.DrawWireSphere(transform.position, attackRange); 

        Gizmos.color = Color.yellow; 
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange); 
    }
}
