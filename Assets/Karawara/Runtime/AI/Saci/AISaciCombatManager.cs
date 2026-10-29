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
        [SerializeField] float whirlwindDamageModifier = 1.2f;  // Pode ser usado para escalar o dano do tornado se necessÃ¡rio
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
            // Chama a animaÃ§Ã£o de ataque corpo a corpo
            Debug.Log("Ataque corpo a corpo ativado!");
            // Implementar a lÃ³gica de ataque corpo a corpo aqui, se necessÃ¡rio
        }

        public void ActivateDurkStomp()
        {
            // ImplementaÃ§Ã£o do evento de stomp
            Debug.Log("Durk Stomp ativado!");
        }

        // MÃ©todo para ativar o ataque de redemoinho, que agora instancia tornados
        public void ActivateSaciWhirlwind()
        {
            if (aiCharacter == null || !aiCharacter.IsInitialized || aiCharacter.State.IsDead || !aiCharacter.isPerformingAction) return;
            // Invoca o ataque de tornados em direÃ§Ã£o ao player
            whirlwindController.WhirlwindAttack(GetPlayer());

            // Instancia o efeito visual do redemoinho na posiÃ§Ã£o do Saci
            //Instantiate(saciWhirlwindVFX, transform.position, Quaternion.identity); 
        }

        // MÃ©todo para obter a posiÃ§Ã£o do player
        public Vector3 GetPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player.transform.position;
        }
        // MÃ©todo para ajustar a rotaÃ§Ã£o do Saci, de acordo com o Ã¢ngulo de visÃ£o em relaÃ§Ã£o ao player
        public override void PivotTowardsTarget(AICharacterManager aiCharacter)
        {
            // Ajusta a rotaÃ§Ã£o para focar no alvo, com animaÃ§Ãµes de giros rÃ¡pidos
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
