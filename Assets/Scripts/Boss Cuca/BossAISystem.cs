using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara
{
    [System.Serializable]
    public class Projetil
    {
        public GameObject projectilePrefab;
        public Transform firePoint;
        public float projectileSpeed = 20f;
        public float fireCooldown = 2f;
        private float fireCooldownTimer;
    }

    public class BossAISystem : MonoBehaviour
    {
        public AudioClip Risada;

        [SerializeField]
        private Projetil _projectile;
        private GameObject player;
        private AudioSource audio;
        private Animator animator;
        [SerializeField]
        private float detectionRadiusPlayer;
        private bool hasLaughed = false;
        [SerializeField]
        private float dashSpeed = 30f; // Velocidade do dash
        [SerializeField]
        private float dashDuration = 0.5f; // Duração do dash
        private bool isDashing = false;
        [SerializeField]
        private DamageBoss _damageBossCollider;

        [SerializeField] private float dashTimer = 2.5f;
        public bool AttackMelee = false;
        public RaycastHit hit;
        private Vector3 directionToPlayer;
        [SerializeField]
        private bool canDash = false;

        private NavMeshAgent agent;
        private EnemyAI _EnemyAI;

        public int AttackIndex = 3;
        void Start(){
            _EnemyAI = GetComponent<EnemyAI>();
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            audio = GetComponent<AudioSource>();
            player = GameObject.FindGameObjectWithTag("Player");
        }

        void Update()
        {
            // Faz o boss olhar para o jogador
            if (player != null)
            {
                directionToPlayer = (player.transform.position - transform.position).normalized;
                if (Physics.Raycast(transform.position, directionToPlayer, out hit, Mathf.Infinity)){
                    if (hit.collider.CompareTag("Player") && !isDashing &&
                        hasLaughed){
                        canDash = true;
                    }
                }
                // Vector3 directionToPlayer = (player.transform.position - transform.position).normalized;
                // if (directionToPlayer != Vector3.zero) // Previne divisão por zero
                // {
                //     Quaternion lookRotation = Quaternion.LookRotation(directionToPlayer);
                //     transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 5f); // Ajuste a suavidade
                // }
            }

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadiusPlayer);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player") && !audio.isPlaying && !hasLaughed)
                {
                    // Começa a risada ao detectar o player
                    audio.clip = Risada;
                    animator.SetTrigger("Risada");
                    audio.Play();
                    hasLaughed = true; // Previne múltiplas risadas durante o mesmo encontro
                    StartCoroutine(WaitForLaughToEnd());
                }
            }
        }

        private bool ridada = false;
        IEnumerator WaitForLaughToEnd()
        {
            // Espera até o fim da risada
            if (!ridada){
                yield return new WaitForSeconds(Risada.length);
                ridada = true;
            }
            else{
                yield return new WaitForSeconds(4f);
            }
            FireProjectile();
            //PlayShootAnimation();
            
            yield return new WaitForSeconds(3f); // Pequeno atraso antes de iniciar o dash
            DashTowardsPlayer();
        }

        void FireProjectile()
        {

            GameObject projectile = Instantiate(_projectile.projectilePrefab, _projectile.firePoint.position, _projectile.firePoint.rotation);
    

            Vector3 direction = (player.transform.position - _projectile.firePoint.position).normalized;


            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            rb.velocity = direction * _projectile.projectileSpeed;
            
            rb.useGravity = false; 
            
        }


        void PlayShootAnimation()
        {
            // Chamar a animação de disparo
           // animator.SetTrigger("Shoot");
        }

        public IEnumerator attackLoop(){
            yield return new WaitForSeconds(5);
            AttackMelee = true;
            _EnemyAI.isAttacking = false;
            _EnemyAI.index = 0;
            AttackIndex++;
        }
        public void MoveToRandomPositionIfClear(){
            float radius = 10f;
            
            Vector3 randowmDirection = Random.insideUnitSphere * radius;
            
            randowmDirection += transform.position;
            if (NavMesh.SamplePosition(randowmDirection, out NavMeshHit hit, radius, NavMesh.AllAreas)){
                transform.position = hit.position;

                

                StartCoroutine(attackLoop());

            }
            else{
                Debug.Log("Nehuma posição válida!");
            }
        }


        void DashTowardsPlayer()
        {
            Debug.Log("147");
            if (player != null && !isDashing){
                Debug.Log("Entrou 149");
                if (canDash)
                {
                    Debug.Log("Entrou 152");
                    animator.SetTrigger("DashCharge");
                    StartCoroutine(DashCoroutine(hit.point));
                }
                
            }
        }

        private bool hasDamagedPlayer = false; // Adicione esta variável

    IEnumerator DashCoroutine(Vector3 targetPosition)
    {
        Debug.Log("AA");
        _damageBossCollider.AtivarCollider();
        isDashing = true;

        // Manter a posição y da Cuca constante (ficando no chão)
        float fixedY = transform.position.y;
        Vector3 targetPositionFlat = new Vector3(targetPosition.x, fixedY, targetPosition.z); // Alvo no mesmo nível

        // Calcular a posição alvo com um offset
        Vector3 offset = (targetPositionFlat - transform.position).normalized * 4f; // Ajuste o valor para a distância que deseja passar
        Vector3 finalTargetPosition = targetPositionFlat + offset;

        while (Vector3.Distance(transform.position, finalTargetPosition) > 0.1f) // Tolerância de 0.1 unidades
        {
            // Move o boss na direção do alvo final
            Vector3 direction = (finalTargetPosition - transform.position).normalized;
            transform.position += direction * (dashSpeed * Time.deltaTime);

            // Aplique dano apenas uma vez durante o dash
            if (_damageBossCollider.canDamage && !hasDamagedPlayer)
            {
                Debug.Log("185");
                ApplyDamageToPlayer();
                hasDamagedPlayer = true; // Marca que o dano já foi aplicado
            }
            yield return null;
        }

        // Garantir que o boss pare exatamente na posição alvo
        transform.position = finalTargetPosition;

        // Resetar a variável para permitir dano em futuros dashes
        hasDamagedPlayer = false;

        isDashing = false;

        
        
        CallMethodBasedOnChance();
        _damageBossCollider.DesativarCollider();
    }

    IEnumerator TimeAttack(){
        yield return new WaitForSeconds(dashTimer);
        AttackMelee = true;
    }
    
    void CallMethodBasedOnChance(){
        StartCoroutine(TimeAttack());
    }
    



        void ApplyDamageToPlayer()
        {
            
            Debug.Log("Player sofreu dano!");
            // Exemplo:
            // player.GetComponent<PlayerHealth>().TakeDamage(damageAmount);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, detectionRadiusPlayer);
        }
    }
}
