using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AISaciCharacterManager : AIBossCharacterManager
    {
        [HideInInspector] public AISaciCombatManager saciCombatManager;
        
        [Header("Range Settings")]
        public float meleeRange = 2.0f; // Distância do ataque corpo a corpo
        public float rangeAttackDistance = 8.0f; // Distância do ataque de longo alcance
        public float minRangeAttackDistance = 5.0f; // Distância mínima para o ataque de longo alcance
        public float longRangeStopDistance = 6.0f; // Distância de parada para o ataque de longo alcance
        public float meleeStopDistance = 1.0f; // Distância de parada para o ataque corpo a corpo
        protected override void Awake()
        {
            base.Awake();
            saciCombatManager = GetComponent<AISaciCombatManager>();
        }

        protected override void Update()
        {
            base.Update();

            // Verifica a distância entre o Saci e o player
            float distanceToPlayer = Vector3.Distance(transform.position, GetPlayerPosition());

            // Se o player estiver muito distante, move em direção ao player antes de atacar
            if (distanceToPlayer > rangeAttackDistance)
            {
                // Ajusta a stopDistance para o ataque de longo alcance, mas primeiro se aproxima
                navMeshAgent.stoppingDistance = 0f; // Define a distância de parada para 0 enquanto se aproxima
                //navMeshAgent.SetDestination(GetPlayerPosition()); // Move em direção ao player
                Debug.Log("Player está longe. Movendo-se em direção ao player.");
            }
            // Se o player estiver dentro do alcance melee, ataca com ataque corpo a corpo
            else if (distanceToPlayer <= meleeRange)
            {
                // Ajusta a stopDistance para o ataque corpo a corpo
                navMeshAgent.stoppingDistance = meleeStopDistance;
                Debug.Log("Player está perto. Atacando corpo a corpo.");
                //saciCombatManager.ActivateMeleeAttack(); // Chama a animação de ataque corpo a corpo
            }
            // Se o player estiver em uma distância intermediária e maior que a distância mínima de ataque de longo alcance
            else if (distanceToPlayer > minRangeAttackDistance && distanceToPlayer <= rangeAttackDistance)
            {
                // Ajusta a stopDistance para o ataque de longo alcance
                navMeshAgent.stoppingDistance = longRangeStopDistance;
                Debug.Log("Player em distância intermediária. Atacando com o ataque de longo alcance.");
                //saciCombatManager.ActivateSaciWhirlwind(); // Ataque de longo alcance
            }
            else
            {
                // Ajusta a stopDistance para uma distância intermediária (se necessário)
                navMeshAgent.stoppingDistance = meleeRange;
            }

            // Verifica se o Saci está perto o suficiente para atacar (não está se movendo mais)
            if (distanceToPlayer <= rangeAttackDistance && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                // Agora que o Saci chegou perto o suficiente, ele pode atacar
                if (distanceToPlayer <= meleeRange){
                    Debug.Log("Atacar corpo a corpo");
                    //saciCombatManager.ActivateMeleeAttack();
                }
                else
                {
                    Debug.Log("Atacar de longo alcance");
                    //saciCombatManager.ActivateSaciWhirlwind();
                }
            }
        }

        // Método para obter a posição do player
        private Vector3 GetPlayerPosition()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player.transform.position;
        }
    }
}