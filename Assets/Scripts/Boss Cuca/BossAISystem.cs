using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using RPGKarawara;
public class BossAISystem : MonoBehaviour{
    public GameObject projectilePrefab; // O prefab do projétil
    public Transform firePoint; // Ponto de origem dos tiros
    public float range = 10f; // A distância de detecção do player
    public float heightOffset = 1f; // Quanto mais alto o projétil deve começar
    public int maxShots = 3; // Número máximo de tiros permitidos
    public float shotCooldown = 2f; // Tempo de cooldown entre os tiros
    public float minDistanceBetweenShots = 1.5f; // Distância mínima entre as posições dos projéteis

    [SerializeField] private int currentShots = 0; // Quantidade de tiros já disparados
    private float cooldownTimer = 0f; // Timer para controle do cooldown
    private List<Vector3> previousPositions = new List<Vector3>(); // Armazena as posições dos projéteis anteriores
    private bool canFire = false;


    //Navmesh
    public float moveDistance = 2.0f;
    public NavMeshAgent agent;
    public float rayDistance = 1.0f; // Distância do raycast para detectar obstáculos
    public LayerMask obstacleLayer; // Layer dos obstáculos
    public float flyHeight = 2;
    public float flySpeed = 0.5f;


    //CircleExplosion 
    public GameObject circlePrefab;
    // Raio da explosão
    public float explosionRadius = 5f;
    public float explosionForce = 700f;
    
    private CharacterController playerController;

    // Força aplicada no player
    public float playerPushForce = 10f;


    private void Update(){
        // Verifica se o cooldown permite o próximo tiro
        if (cooldownTimer > 0){
            cooldownTimer -= Time.deltaTime;
        }

        if (Keyboard.current.jKey.wasPressedThisFrame){
            CircleExplosion();
        }

        if (Keyboard.current.pKey.wasPressedThisFrame){

            if (!IsObstacleBehind()){
                MoveBackward();
            }
        }

        // Verifica se o botão foi pressionado e se ainda pode disparar
        if (canFire && currentShots < maxShots && cooldownTimer <= 0){


            if (!IsObstacleBehind()){
                MoveBackward();
            }

            if (IsPlayerInRange()){
                ShootProjectile(GetPlayerPosition());
                currentShots++; // Incrementa o contador de tiros
                cooldownTimer = shotCooldown; // Reinicia o cooldown

                // Se atingiu o máximo de tiros, inicia o cooldown para ativar os projéteis
                if (currentShots >= maxShots){
                    StartCoroutine(ShotCooldown());
                }
            }
        }
    }

    bool IsObstacleBehind(){
        Ray ray = new Ray(transform.position, -transform.forward); // Raycast para trás
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, rayDistance, obstacleLayer)){
            // Obstáculo detectado
            return true;
        }

        // Nenhum obstáculo detectado
        return false;
    }

    private Coroutine flyCoroutine;

    // Função para mover o player para trás
    void MoveBackward(){
        // Calcula a nova posição movendo para trás
        Vector3 moveToPosition = transform.position - transform.forward * moveDistance;

        // Verifica se a nova posição é válida no NavMesh
        if (NavMesh.SamplePosition(moveToPosition, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)){
            // Define o destino do agente para a nova posição
            agent.SetDestination(hit.position);
            agent.updateRotation = false;
            // Inicia a corrotina de voo para ajustar a altura gradualmente
            if (flyCoroutine != null){
                StopCoroutine(flyCoroutine); // Para qualquer voo anterior
            }

            flyCoroutine = StartCoroutine(FlyToHeight());
        }
    }

    IEnumerator FlyToHeight(){
        float startHeight = agent.baseOffset; // Pega a altura atual do agente
        float elapsedTime = 0f;

        // Enquanto não atingir a altura desejada
        while (Mathf.Abs(agent.baseOffset - flyHeight) > 0.01f){
            // Interpola a altura ao longo do tempo
            elapsedTime += Time.deltaTime;
            agent.baseOffset = Mathf.Lerp(startHeight, flyHeight, elapsedTime * flySpeed);

            yield return null; // Espera até o próximo frame
        }

        // Assegura que a altura final seja exatamente igual a flyHeight
        agent.baseOffset = flyHeight;
        canFire = true;
    }

    private bool IsPlayerInRange(){
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, range);
        foreach (var hitCollider in hitColliders){
            if (hitCollider.CompareTag("Player")){
                return true; // O player está dentro do alcance
            }
        }

        return false; // O player não está dentro do alcance
    }

    private Vector3 GetPlayerPosition(){
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, range);
        foreach (var hitCollider in hitColliders){
            if (hitCollider.CompareTag("Player")){
                return hitCollider.transform.position; // Retorna a posição do player
            }
        }

        return Vector3.zero; // Retorna um vetor nulo se não houver jogador
    }

    private List<GameObject> projectiles = new List<GameObject>();

    private void ShootProjectile(Vector3 targetPosition){
        // Tentativa de gerar uma posição válida
        Vector3 newPosition;
        int maxAttempts = 10; // Limite de tentativas
        int attempts = 0;

        do{
            // Gera uma posição aleatória dentro do intervalo especificado
            var positionVAR = new Vector3(Random.Range(-2.0f, 2.0f), Random.Range(0.3f, 0.5f),
                Random.Range(-0.1f, 0.3f));
            newPosition = firePoint.position + Vector3.up * heightOffset + positionVAR;

            // Incrementa o número de tentativas
            attempts++;
        }
        // Continua tentando até encontrar uma posição suficientemente distante ou atingir o número máximo de tentativas
        while (IsPositionTooClose(newPosition) && attempts < maxAttempts);

        // Instancia o projétil na nova posição
        GameObject projectile = Instantiate(projectilePrefab, newPosition, Quaternion.identity);
        projectiles.Add(projectile);
        // Armazena a nova posição
        previousPositions.Add(newPosition);

        // Remove posições antigas da lista (mantém só as últimas 'maxShots' posições)
        if (previousPositions.Count > maxShots){
            Debug.Log("Max shots reached");
            previousPositions.RemoveAt(0);
        }
    }

    IEnumerator ShotCooldown(){
        yield return new WaitForSeconds(shotCooldown); // Aguarda o cooldown total
        ActiveProjectiles();
        currentShots = 0; // Reseta os tiros após o cooldown
        projectiles.Clear();
        canFire = false;
    }

    private void ActiveProjectiles(){
        foreach (var obs in projectiles){
            obs.GetComponent<ProjectileMovement>().Initialize();
        }
    }

    private bool IsPositionTooClose(Vector3 newPosition){
        foreach (var pos in previousPositions){
            // Verifica se a distância é menor do que a mínima permitida
            if (Vector3.Distance(newPosition, pos) < minDistanceBetweenShots){
                return true;
            }
        }

        return false;
    }

    public void CircleExplosion(){
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerController  = player.GetComponent<CharacterController>();
        Vector3 circlePosition = transform.position;
        circlePosition.y = 0; // Para manter o círculo no chão
        Instantiate(circlePrefab, circlePosition, Quaternion.identity);

        // Chamar a função de explosão
        Explode(circlePosition);
    }

    void Explode(Vector3 explosionPosition){
        // Encontrar todos os objetos com física no raio da explosão
        Collider[] colliders = Physics.OverlapSphere(explosionPosition, explosionRadius);

        foreach (Collider hit in colliders){
            // Verificar se é o player (com Character Controller)
                if (hit.gameObject.CompareTag("Player"))
    {
        // Calcular a direção para empurrar o player
        Vector3 direction = hit.transform.position - explosionPosition;
        direction.y = 0; // Evitar empurrar para cima ou para baixo

        // Aplicar o movimento manualmente ao Character Controller
        playerController.Move(direction.normalized * playerPushForce * Time.deltaTime);

        // Garantir que o player fique no chão
        RaycastHit groundHit;
        if (Physics.Raycast(hit.transform.position, Vector3.down, out groundHit, Mathf.Infinity))
        {
            // Alinhar a posição Y do player com o chão
            hit.transform.position = new Vector3(hit.transform.position.x, groundHit.point.y, hit.transform.position.z);
        }

        // Chamar a animação de hit no estilo Rennala Queen
        CharacterAnimatorManager animatorManager = hit.GetComponent<CharacterAnimatorManager>();
        if (animatorManager != null)
        {
            // Ajustar a posição do player para a base do Character Controller
            Vector3 colliderCenter = playerController.center; // Centro do Character Controller
            float colliderHeight = playerController.height;   // Altura do Character Controller
            
            // Ajustar a posição Y para a base do collider
            hit.transform.position = new Vector3(hit.transform.position.x, groundHit.point.y + (colliderHeight / 2), hit.transform.position.z);

            // Configurações estilo Rennala
            string targetAnimation = "Hit_And_Fall";  // A animação que você quer que o player execute
            bool isPerformingAction = true;           // Indica que o player está realizando uma ação (tomando o hit)
            bool applyRootMotion = true;              // O movimento deve ser influenciado pela animação
            bool canRotate = false;                   // O player não pode girar durante o hit
            bool canMove = false;                     // O player não pode se mover durante o hit

            // Chama a animação com os parâmetros estilo Rennala Queen
            animatorManager.PlayTargetActionAnimation(targetAnimation, isPerformingAction, applyRootMotion, canRotate, canMove);
        }
    }   
            else{
                // Verificar se o objeto tem um Rigidbody
                Rigidbody rb = hit.GetComponent<Rigidbody>();

                if (rb != null){
                    // Aplicar força para empurrar o objeto para longe
                    rb.AddExplosionForce(explosionForce, explosionPosition, explosionRadius);
                }
            }
        }
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, range);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
