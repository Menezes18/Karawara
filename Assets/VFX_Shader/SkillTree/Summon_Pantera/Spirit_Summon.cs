using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
    public class Spirit_Summon : MonoBehaviour
    {
    public Transform player; // Referência ao transform do player
    public float followDistance = 2f; // Distância mínima para seguir o player
    public float enemyDetectionRange = 10f; // Distância para detectar inimigos
    public float attackRange = 2f; // Distância para começar a atacar
    public int damage = 10; // Dano causado por ataque
    public float attackCooldown = 1f; // Tempo de espera entre ataques
    public float speed = 5f; // Velocidade de movimento do lobo
    private Transform target; // Alvo atual do lobo (inimigo ou player)
    private float lastAttackTime; // Tempo do último ataque realizado
    private Animator animator; // Referência ao componente Animator
    public float erodeRate = 0.03f;
    public float erodeRefreshRate = 0.01f;
    public float erodeDelay = 1.25f;
    public SkinnedMeshRenderer erodeObject;
    void Start()
    {
        animator = GetComponent<Animator>(); // Inicializa o Animator
        target = player; // Define o player como alvo inicial
        StartCoroutine(ErodeObject());
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
            // Detecta inimigos dentro do alcance de detecção
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
                // Se não houver inimigos, o lobo fica parado
                animator.SetBool("isMoving", false);
                animator.SetTrigger("Idle");
            }
        }
    }
    IEnumerator ErodeObject(){
        yield return new WaitForSeconds(erodeDelay);

        float t = 1;
        while (t > 0)
        {
            t -= erodeRate;
            erodeObject.material.SetFloat("_Erode", t);
            yield return new WaitForSeconds(erodeRefreshRate);
        }
        yield return new WaitForSeconds(5f);
        while (t < 1.2)
        {
            t += erodeRate;
            erodeObject.material.SetFloat("_Erode", t);
            yield return new WaitForSeconds(erodeRefreshRate);
        }
    }
    void MoveTowards(Transform target)
    {
        animator.SetBool("isMoving", true); // Ativa a animação de movimento
    
        // Move-se na direção do alvo
        transform.position = Vector3.MoveTowards(transform.position, target.position, Time.deltaTime * speed);
    
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
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            animator.SetTrigger("Attack"); // Aciona a animação de ataque
            Debug.Log("Atacando " + target.name + " e causando " + damage + " de dano."); // Log do ataque para depuração
            lastAttackTime = Time.time; // Atualiza o tempo do último ataque
        }
    }

    // Função para desenhar gizmos no editor, ajudando a visualizar os ranges
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta; // Define a cor do gizmo para a distância de seguir
        Gizmos.DrawWireSphere(transform.position, followDistance); // Desenha o gizmo da distância de seguir

        Gizmos.color = Color.red; // Define a cor do gizmo para o alcance de ataque
        Gizmos.DrawWireSphere(transform.position, attackRange); // Desenha o gizmo do alcance de ataque

        Gizmos.color = Color.yellow; // Define a cor do gizmo para o alcance de detecção de inimigos
        Gizmos.DrawWireSphere(transform.position, enemyDetectionRange); // Desenha o gizmo do alcance de detecção de inimigos
    }
}
