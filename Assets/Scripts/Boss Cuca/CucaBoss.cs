using System.Collections;
using UnityEngine;

namespace RPGKarawara
{
    public class CucaBoss : MonoBehaviour
    {
        public Transform player;
        public GameObject nightmarePrefab;
        public GameObject fireballPrefab;
        public Transform fireballSpawnPoint;
        public float meleeRange = 5f;
        public float fireballCooldown = 5f;
        public float summonCooldown = 10f;
        public float fleeCooldown = 8f; // Tempo entre fugas
        public float health = 100f;

        private UnityEngine.AI.NavMeshAgent agent;
        private Animator animator;
        private float fireballTimer = 0f;
        private float summonTimer = 0f;
        private float fleeTimer = 0f;
        private int phase = 1;
        private bool isFleeing = false;

        void Start()
        {
            agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (health <= 50 && phase == 1)
            {
                EnterPhase2();
            }

            if (isFleeing) return; // Durante a fuga, não realiza outros comportamentos

            fleeTimer += Time.deltaTime;
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer <= meleeRange)
            {
                MeleeAttack();
            }
            else
            {
                fireballTimer += Time.deltaTime;
                summonTimer += Time.deltaTime;

                if (summonTimer >= summonCooldown)
                {
                    SummonNightmares();
                    summonTimer = 0f;
                }
                else if (fireballTimer >= fireballCooldown)
                {
                    FireballAttack();
                    fireballTimer = 0f;
                }
                else if (fleeTimer >= fleeCooldown)
                {
                    StartCoroutine(FleeFromPlayer());
                }
                else
                {
                    ChasePlayer();
                }
            }
        }

        public void TakeDamage(float damage)
        {
            health -= damage;

            if (!isFleeing && health > 0) // Foge apenas se estiver viva e não fugindo
            {
                StartCoroutine(FleeFromPlayer());
            }
        }

        void MeleeAttack()
        {
            agent.isStopped = true;
            // animator.SetTrigger("MeleeAttack");
        }

        void FireballAttack()
        {
            agent.isStopped = true;
            // animator.SetTrigger("CastFireball");

            GameObject fireball = Instantiate(fireballPrefab, fireballSpawnPoint.position, Quaternion.identity);
            fireball.GetComponent<Rigidbody>().velocity = (player.position - fireballSpawnPoint.position).normalized * 10f;
        }

        void SummonNightmares()
        {
            agent.isStopped = true;
            // animator.SetTrigger("Summon");

            for (int i = 0; i < 3; i++) // Invoca 3 pesadelos em volta do jogador
            {
                Vector3 spawnPosition = player.position + new Vector3(Random.Range(-4, 4), 0, Random.Range(-4, 4));
                Instantiate(nightmarePrefab, spawnPosition, Quaternion.identity);
            }
        }

        void ChasePlayer()
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            // animator.SetBool("IsMoving", true);
        }

        void EnterPhase2()
        {
            phase = 2;
            // animator.SetTrigger("Transform");
            StartCoroutine(Phase2AttackPattern());
        }

        IEnumerator Phase2AttackPattern()
        {
            while (health > 0 && phase == 2)
            {
                // Ataque em padrão circular
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * (360f / 5);
                    Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                    Vector3 spawnPosition = fireballSpawnPoint.position + direction * 2f;

                    GameObject fireball = Instantiate(fireballPrefab, spawnPosition, Quaternion.identity);
                    fireball.GetComponent<Rigidbody>().velocity = direction * 10f;
                }

                yield return new WaitForSeconds(5f); // Pausa antes do próximo ataque
            }
        }

        IEnumerator FleeFromPlayer()
        {
            isFleeing = true;
            fleeTimer = 0f; // Reseta o cooldown de fuga
            // animator.SetBool("IsMoving", true);

            Vector3 fleeDirection = (transform.position - player.position).normalized * 10f;
            Vector3 fleePosition = transform.position + fleeDirection;

            agent.SetDestination(fleePosition);
            yield return new WaitForSeconds(3f); // Tempo da fuga

            isFleeing = false;
        }
    }
}
