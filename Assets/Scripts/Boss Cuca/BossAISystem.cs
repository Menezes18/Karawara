using System.Collections;
using UnityEngine;

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
        void Start()
        {
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


        IEnumerator WaitForLaughToEnd()
        {
            // Espera até o fim da risada
            yield return new WaitForSeconds(Risada.length);
            FireProjectile();
            //PlayShootAnimation();
            
            yield return new WaitForSeconds(2.5f); // Pequeno atraso antes de iniciar o dash
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
            //animator.SetTrigger("Shoot");
        }

        void DashTowardsPlayer()
        {
            if (player != null && !isDashing)
            {
               
                    if (canDash)
                    {
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
        yield return new WaitForSeconds(1000f);
        AttackMelee = true;
    }
    void CallMethodBasedOnChance(){
        StartCoroutine(TimeAttack());
        float chance = Random.value; // Gera um número entre 0.0 e 1.0
        if (chance <= 0.3f) // 30% de chance
        {
            MethodA();
        }
        else // 70% de chance
        {
            MethodB();
        }
    }

    void MethodA()
    {
        Debug.Log("Método A chamado!"); // Método que será chamado 30% das vezes
        // Adicione aqui a lógica que você deseja para o Método A
    }

    void MethodB()
    {
        Debug.Log("Método B chamado!"); // Método que será chamado 70% das vezes
        // Adicione aqui a lógica que você deseja para o Método B
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
