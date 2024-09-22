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

        void Start()
        {
            animator = GetComponent<Animator>();
            audio = GetComponent<AudioSource>();
            player = GameObject.FindGameObjectWithTag("Player");
        }

        void Update()
        {
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
            PlayShootAnimation();
            
            yield return new WaitForSeconds(1f); // Pequeno atraso antes de iniciar o dash
            DashTowardsPlayer();
        }

        void FireProjectile()
        {
            // Instanciar o projétil
            GameObject projectile = Instantiate(_projectile.projectilePrefab, _projectile.firePoint.position, _projectile.firePoint.rotation);

            // Calcular a direção do projétil em direção ao jogador
            Vector3 direction = (player.transform.position - _projectile.firePoint.position).normalized;

            // Aplicar movimento ao projétil
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            rb.velocity = direction * _projectile.projectileSpeed;
        }

        void PlayShootAnimation()
        {
            // Chamar a animação de disparo
            animator.SetTrigger("Shoot");
        }

        void DashTowardsPlayer()
        {
            if (player != null && !isDashing)
            {
                // Realiza um raycast para garantir que o jogador está na linha de visão
                RaycastHit hit;
                if (Physics.Raycast(transform.position, (player.transform.position - transform.position).normalized, out hit))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        StartCoroutine(DashCoroutine(hit.point));
                    }
                }
            }
        }

        IEnumerator DashCoroutine(Vector3 targetPosition)
        {
            isDashing = true;

            // Manter a posição y da Cuca constante (ficando no chão)
            float fixedY = transform.position.y;
            Vector3 targetPositionFlat = new Vector3(targetPosition.x, fixedY, targetPosition.z); // Alvo no mesmo nível
            Vector3 direction = (targetPositionFlat - transform.position).normalized;

            float elapsedTime = 0f;

            while (elapsedTime < dashDuration)
            {
                // Move apenas no plano x e z
                transform.position += direction * dashSpeed * Time.deltaTime;

                elapsedTime += Time.deltaTime;

                // Verifica se passou pelo jogador para aplicar dano
                if (Vector3.Distance(transform.position, player.transform.position) < 1f)
                {
                    ApplyDamageToPlayer();
                }

                yield return null;
            }

            isDashing = false;
        }

        void ApplyDamageToPlayer()
        {
            // Lógica de aplicação de dano ao jogador
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
