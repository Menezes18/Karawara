using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.AI;


    public class AguaraHeal : MonoBehaviour
    {
    public Transform player; // Referência ao transform do player
    public float followDistance = 6.87f; // Distância mínima para seguir o player
    public float attackRange = 2f; // Distância para começar a atacar
    public int damage = 10; // Dano causado por ataque
    public float attackCooldown = 1f; // Tempo de espera entre ataques
    private Transform target; // Alvo atual do lobo (inimigo ou player)
    private float lastAttackTime; // Tempo do último ataque realizado
    private Animator animator; // Referência ao componente Animator
    public NavMeshAgent agent; // Referência ao NavMeshAgent
    public float erodeRate = 0.03f;
    public float erodeRefreshRate = 0.01f;
    public float erodeDelay = 1.25f;
    public SkinnedMeshRenderer erodeObject;
    public List<GameObject> _objectsToDetach = new List<GameObject>();

    void Start()
    {
        GameObject playerGameObject = GameObject.FindGameObjectWithTag("Player");
        player = playerGameObject.transform;
        animator = GetComponent<Animator>(); // Inicializa o Animator
        agent = GetComponent<NavMeshAgent>(); // Inicializa o NavMeshAgent
        target = player; // Define o player como alvo inicial
        //erodeObject = GetComponent<SkinnedMeshRenderer>(); // Inicializa o MeshRenderer
        StartCoroutine("Eroding");
        StartCoroutine("Detach");
    }
    IEnumerator Eroding()
    {
        float t = erodeObject.material.GetFloat("_Erode");
        while (t > 0)
        {
            t -= erodeRate;
            erodeObject.material.SetFloat("_Erode", t);
            yield return new WaitForSeconds(erodeRefreshRate);
        }
    }
    IEnumerator Detach()
        {
            yield return new WaitForSeconds(1f);

            for (int i=0; i < _objectsToDetach.Count; i++)
            {   
                _objectsToDetach[i].transform.parent = null;
                Destroy(_objectsToDetach[i], 1f);
            }
        
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
            AttackTarget();
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
            animator.SetTrigger("Heal"); 
            Debug.Log("Curando ");
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

    }
}
