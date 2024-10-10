using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class AISaciCombatManager : AICharacterCombatManager
    {
        AISaciCharacterManager saciManager;

        [Header("Tornado Attack Settings")]
        [SerializeField] SaciWhirlwindController whirlwindController; // Controlador dos tornados
        [SerializeField] SaciWhirlwindDamage kickDamageCollider;      // Collider para o ataque de chute
        public float whirlwindAttackRadius = 2.0f;

        [Header("Damage")]
        [SerializeField] int baseDamage = 20;
        [SerializeField] float kickDamageModifier = 1.5f;
        [SerializeField] float whirlwindDamageModifier = 1.2f;  // Pode ser usado para escalar o dano do tornado se necessário
        public float whirlwindDamage = 30;

        [Header("VFX")]
        public GameObject saciWhirlwindVFX; // Efeito visual do redemoinho
        
        protected override void Awake()
        {
            base.Awake();
            saciManager = GetComponent<AISaciCharacterManager>();
            if (saciManager == null){
                Debug.Log("faltando a saci character manager");
            }
        }

        public void ActivateMeleeAttack()
        {
            // Chama a animação de ataque corpo a corpo
            Debug.Log("Ataque corpo a corpo ativado!");
            // Implementar a lógica de ataque corpo a corpo aqui, se necessário
        }

        public void ActivateDurkStomp()
        {
            // Implementação do evento de stomp
            Debug.Log("Durk Stomp ativado!");
        }

        // Método para ativar o ataque de redemoinho, que agora instancia tornados
        public void ActivateSaciWhirlwind()
        {
            // Invoca o ataque de tornados em direção ao player
            whirlwindController.WhirlwindAttack(GetPlayer());

            // Instancia o efeito visual do redemoinho na posição do Saci
            //Instantiate(saciWhirlwindVFX, transform.position, Quaternion.identity); 
        }

        // Método para obter a posição do player
        public Vector3 GetPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player.transform.position;
        }
        // Método para ajustar a rotação do Saci, de acordo com o ângulo de visão em relação ao player
        public override void PivotTowardsTarget(AICharacterManager aiCharacter)
        {
            // Ajusta a rotação para focar no alvo, com animações de giros rápidos
            if (aiCharacter.isPerformingAction)
                return;

            if (viewableAngle >= 61 && viewableAngle <= 110)
            {
                aiCharacter.characterAnimatorManager.PlayTargetActionAnimation("Turn_Right_90", true);
            }
            else if (viewableAngle <= -61 && viewableAngle >= -110)
            {
                aiCharacter.characterAnimatorManager.PlayTargetActionAnimation("Turn_Left_90", true);
            }
            else if (viewableAngle >= 146 && viewableAngle <= 180)
            {
                aiCharacter.characterAnimatorManager.PlayTargetActionAnimation("Turn_Right_180", true);
            }
            else if (viewableAngle <= -146 && viewableAngle >= -180)
            {
                aiCharacter.characterAnimatorManager.PlayTargetActionAnimation("Turn_Left_180", true);
            }
        }
    }
}
