using UnityEngine;
using UnityEngine.AI;

public class RandomNPCMovement : MonoBehaviour
{
    public float moveInterval = 3f;  // Intervalo entre os movimentos aleatórios
    public float moveRange = 10f;    // O alcance máximo de movimentação do NPC
    public float idleChance = 0.5f;  // Probabilidade de o NPC ficar parado (50%)
    public float idleMinTime = 3f;   // Tempo mínimo de idle
    public float idleMaxTime = 6f;   // Tempo máximo de idle

    private NavMeshAgent navMeshAgent;
    private Animator animator;
    private float timeToMove;
    private float idleTime;
    private bool isIdle;

    void Start()
    {
        // Obtém os componentes NavMeshAgent e Animator
        navMeshAgent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        timeToMove = moveInterval;
    }

    void Update()
    {
        // Se o NPC estiver em idle, espera o tempo de idle
        if (isIdle)
        {
            idleTime -= Time.deltaTime;

            if (idleTime <= 0)
            {
                isIdle = false;
                MoveToRandomPosition();  // Faz o NPC se mover depois de "idle"
            }

            // Certifica-se de que a animação está como "idle"
            animator.SetBool("isWalking", false);
            return; // Retorna para não continuar a lógica de movimento enquanto estiver idle
        }

        // Decrementa o contador de tempo para o próximo movimento
        timeToMove -= Time.deltaTime;

        // Quando o contador chega a zero, o NPC decide se vai se mover ou ficar em idle
        if (timeToMove <= 0)
        {
            if (Random.value < idleChance)
            {
                // Decide ficar em idle por um tempo aleatório
                isIdle = true;
                idleTime = Random.Range(idleMinTime, idleMaxTime);
                animator.SetBool("isWalking", false); // Define a animação de "parado"
            }
            else
            {
                // Se não for em idle, o NPC se move para uma nova posição aleatória
                MoveToRandomPosition();
            }

            timeToMove = moveInterval;
        }
        else
        {
            // Se o NPC estiver em movimento, atualiza a animação para "andando"
            if (navMeshAgent.velocity.magnitude > 0.1f) // Verifica se o NPC está se movendo
            {
                animator.SetBool("isWalking", true); // Ativa animação "andando"
            }
            else
            {
                animator.SetBool("isWalking", false); // Caso contrário, desativa animação "andando"
            }
        }
    }

    // Move o NPC para uma posição aleatória dentro de um alcance determinado
    private void MoveToRandomPosition()
    {
        // Gera uma posição aleatória dentro de um raio do NPC
        Vector3 randomDirection = Random.insideUnitSphere * moveRange;
        randomDirection += transform.position;

        // Garante que a posição está dentro do NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, moveRange, NavMesh.AllAreas))
        {
            // Comanda o NavMeshAgent a se mover para a posição aleatória encontrada
            navMeshAgent.SetDestination(hit.position);
        }
    }
}
