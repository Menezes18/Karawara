using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.VFX;
using VTabs.Libs;

namespace RPGKarawara {
    public class CucaBoss : MonoBehaviour {
        [Header("VFX")]
        public string tamanhoProperty = "Tamanho"; // Nome da propriedade no VFX Graph
        public float multiplierSpeed = 1f; // Velocidade de multiplicação ao longo do tempo
        private VisualEffect vfxComponent; // Referência ao VisualEffect
        public GameObject VfxLaser;


        public Transform player; // Referência ao jogador
        public GameObject magicProjectile; // Prefab da magia
        public Transform magicSpawnPoint; // Ponto de origem das magias
        public float detectionRange = 10f; // Alcance para detectar o jogador
        public float stayCloseDuration = 3f; // Tempo que o jogador deve ficar no range para a Cuca reagir
        public float retreatDistance = 15f; // Distância para onde a Cuca recua
        private NavMeshAgent agent; // NavMeshAgent da Cuca
        [SerializeField] private float closeTimer = 0f; // Tempo acumulado dentro do range
        
        public float timer = 0f;
        [SerializeField] private bool atirando = true;
        public float timerAttack = 3f; // Intervalo para alternar entre os ataques
        
        public Transform laserOrigin;
        public float laserDuration = 4f; // Laser ficará ativo por 4 segundos
        public float laserSpeed = 10f;
        public LineRenderer lineRenderer;
        public LayerMask hitLayers;
        public bool isLaserActive = false;
        public float laserTimer = 0f;
        public float attackCooldown = 5f; // Tempo de espera entre ataques
        
        public float laserDelay = 1f; // Atraso antes do laser começar a seguir o jogador
        [SerializeField] private bool isActive = false; 
        public float activationRange = 20f;
        private bool isRetreating = false; // Indica se o boss está recuando
        private Animator animator;
        private HealthManager healthManager;
        private float delayTimer = 0f; // Timer para o delay
        public RaycastHit hit;
        public float cooldownTime = 2f;  // Tempo de cooldown em segundos
        private float lastDamageTime = 0f;
        private float originalLaserDuration; 
        private bool isLaserDurationReduced = false;
        private void Start() {
            animator = GetComponent<Animator>();
            agent = GetComponent<NavMeshAgent>();
            agent.updateRotation = false; 
            InitLaser();
            healthManager = GetComponent<HealthManager>();
            originalLaserDuration = laserDuration;
            Transform child = VfxLaser.transform.GetChild(0); // Obtém o primeiro filho
            if (child != null)
            {
                vfxComponent = child.GetComponent<VisualEffect>();
            }

            if (vfxComponent == null)
            {
                Debug.LogError("VisualEffect não encontrado no primeiro filho.");
            }
        }
        
        private void Update() {
            if (!isActive)
            {
                CheckActivationRange();
                return; 
            }
            TargetPlayer();
            AttackTimer();
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= detectionRange) {
                closeTimer += Time.deltaTime; 

                if (closeTimer >= stayCloseDuration) {
                    Retreat(); 
                    closeTimer = 0f; 
                }
            } else {
                closeTimer = 0f; 
            }

            // Verifica se o laser está ativo e o mantém ativo por 4 segundos
            if (isLaserActive) {
                LaserActivate();
            }

            LookAtPlayer();
        }

       public void LaserActivate()
{
    laserTimer += Time.deltaTime;

    if (laserTimer <= laserDuration)
    {
        // Adicionar o delay antes de calcular a direção e posição
        delayTimer += Time.deltaTime;

        if (delayTimer >= laserDelay) // Quando o delay for alcançado
        {
            
            Vector3 laserDirection = (player.position - laserOrigin.position).normalized;
            
            
            float distanceToPlayer = Vector3.Distance(laserOrigin.position, player.position);
            
            
            if (distanceToPlayer > 2f) 
            {
                
                laserProgress += Time.deltaTime * laserSpeed;
                if (vfxComponent != null)
                {
                    // Obtém o valor atual da propriedade 'Tamanho'
                    float currentSize = vfxComponent.GetFloat(tamanhoProperty);

                    // Calcula o novo valor multiplicado pelo tempo
                    float newSize = currentSize * (1 + multiplierSpeed * Time.deltaTime);

                    // Atualiza a propriedade 'Tamanho' no VFX Graph
                    if(newSize < distanceToPlayer - 3.5f)vfxComponent.SetFloat(tamanhoProperty, newSize);
                    Debug.Log("Tamanho" + tamanhoProperty + "valor" + tamanhoProperty.GetName());
                }
            }
            else
            {
                
                laserProgress += Time.deltaTime * laserSpeed * 0.5f;
            }

            // Atualizar a posição do laser
            Vector3 laserEnd = Vector3.Lerp(laserOrigin.position, player.position, laserProgress / distanceToPlayer);

            // Atualizar o LineRenderer com a nova posição
            lineRenderer.SetPosition(0, laserOrigin.position);
            lineRenderer.SetPosition(1, laserEnd);

            // Verificar colisão no caminho do laser
            if (Physics.Raycast(laserOrigin.position, laserDirection, out hit, laserProgress, hitLayers))
            {
                if (Time.time >= lastDamageTime + cooldownTime)
                {
                    lineRenderer.SetPosition(1, hit.point);

                    CharacterManager damageTarget = hit.collider.GetComponentInParent<CharacterManager>();
                    if (damageTarget != null)
                    {
                        ApplyLaserDamage(damageTarget);
                    }
                }
            }
        }
    }
    else
    {
        DeactivateLaser();
        laserProgress = 0f; // Resetar o progresso do laser
        delayTimer = 0f; // Resetar o timer do delay
    }
}

        private List<CharacterManager> charactersDamaged = new List<CharacterManager>();

        private void CheckActivationRange()
        {
            if (player == null) return;

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= activationRange)
            {
                ActivateBoss();
                
            }
        }

        private void ActivateBoss()
        {
            isActive = true;
            healthManager.AtivarHealthBar();
            Debug.Log("Boss ativado!");
        }
        private void ApplyLaserDamage(CharacterManager damageTarget){

                if (!isLaserDurationReduced) {
                    StartCoroutine(ReduceLaserDurationTemporarily());
                }
                TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
                damageEffect.physicalDamage = 5f; 
                damageEffect.contactPoint = hit.point; // Ponto de contato do laser

                damageTarget.characterEffectsManager.ProcessInstantEffect(damageEffect);

        }
        private IEnumerator ReduceLaserDurationTemporarily() {
            isLaserDurationReduced = true; 
            laserDuration = originalLaserDuration / 2f; 

            yield return new WaitForSeconds(3f); 

            laserDuration = originalLaserDuration; 
            isLaserDurationReduced = false;
        }
        public void InitLaser() {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.1f;
            lineRenderer.endWidth = 0.1f;
            lineRenderer.startColor = Color.clear;
            lineRenderer.endColor = Color.clear;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.enabled = false;
        }

        public void ActivateLaser() { 

            // Ativa o laser após o delay
            isLaserActive = true;
            laserTimer = 0f;  
            lineRenderer.enabled = true;
        }

        public void DeactivateLaser() {
            VfxLaser.SetActive(false);
            vfxComponent.SetFloat(tamanhoProperty, 2f);
            atirando = true;
            isLaserActive = false;
            lineRenderer.enabled = false;
            lineRenderer.SetPosition(0, Vector3.zero); // Limpa o LineRenderer
            lineRenderer.SetPosition(1, Vector3.zero);
            animator.SetBool("Attack02", false);
            timer = attackCooldown; // Reseta o timer para evitar ataques consecutivos
        }

        public void AttackTimer() {
            if (isRetreating) return; 

            timer += Time.deltaTime;

            if (timer >= attackCooldown) {
                if (!isLaserActive) { // Certifique-se de que o laser não está ativo
                    if (atirando) {
                        AnimacaoAttack(1); // Atira o projétil mágico
                    } else{
                        AnimacaoAttack(2);
                    }

                    
                    
                    timer = 0f;
                }
            }
        }
        private float laserProgress = 0f; 

        public void AnimacaoAttack(int num){
            if (num == 1){
                
                animator.SetTrigger("Attack01");
            }else if (num == 2){
                animator.SetBool("Attack02", true);
            }
        }

        public void EventAttack(int number){
            if (number == 1){
                AttackPlayer();
            }else if (number == 2){
                StartCoroutine(ActivateLaserWithDelay());
                VfxLaser.SetActive(true);
            }
        }

        private IEnumerable TemporailyReduceLaserDuration(float duration, float reducendDuration){
            float originalLaserDuration = laserDuration;
            laserDuration = reducendDuration;
            yield return new WaitForSeconds(reducendDuration);
            laserDuration = originalLaserDuration;
        }

        private IEnumerator ActivateLaserWithDelay() {
            yield return new WaitForSeconds(laserDelay);
            ActivateLaser();
            yield return new WaitForSeconds(laserDuration); // Espera o laser durar seu tempo
            DeactivateLaser(); // Desativa o laser depois do tempo definido
        }

        private void LookAtPlayer() {
            // Rotacionar a Cuca para olhar na direção do jogador
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0; // Manter a rotação no plano horizontal
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation =
                Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); // Rotação suave
        }

        private void Retreat() {
            isRetreating = true; // O boss está recuando

            // Recuar para longe do jogador
            Vector3 retreatDirection = (transform.position - player.position).normalized;
            Vector3 retreatPosition = transform.position + retreatDirection * retreatDistance;

            // Garantir que o destino seja válido no NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(retreatPosition, out hit, 5f, NavMesh.AllAreas)) {
                agent.SetDestination(hit.position); // Definir o destino no NavMesh
            }

            // Interromper recuo após alcançar o destino
            StartCoroutine(StopRetreatingAfterDelay(2f)); // Adapte o tempo, se necessário
        }

        private IEnumerator StopRetreatingAfterDelay(float delay) {
            yield return new WaitForSeconds(delay);
            isRetreating = false; // O boss parou de recuar
        }

        public void TargetPlayer(){
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
        private void AttackPlayer() {
            StartCoroutine(ShootProjectiles());
        }

        private IEnumerator ShootProjectiles() {
            float spreadAngle = 10f; // A quantidade de spread dos projeteis
            Vector3 direction = (player.position - magicSpawnPoint.position).normalized;

            for (int i = 0; i < 3; i++) {
                Vector3 spreadDirection = Quaternion.Euler(0, (i - 1) * spreadAngle, 0) * direction;

                // Instanciar o projétil mágico
                GameObject primeProjectile = Instantiate(magicProjectile, magicSpawnPoint.position, Quaternion.identity);
                Rigidbody rb = primeProjectile.GetComponent<Rigidbody>();
                rb.velocity = spreadDirection * 20f;
                yield return new WaitForSeconds(0.1f);
            }
            atirando = false;
        }

        private void OnDrawGizmosSelected() {
            // Desenhar o range de detecção no editor
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, retreatDistance);
        }
    }
}
