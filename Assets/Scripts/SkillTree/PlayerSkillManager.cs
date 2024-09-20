using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace RPGKarawara.SkillTree
{
    [System.Serializable]
    public class SkillSlot
    {
        public Skill skillSlot;
        public float slotCooldownDuration = 5f; // Cooldown específico do slot
        public bool canUse = true; // Indica se o slot pode ser usado (não está em cooldown)
    }

    public class PlayerSkillManager : MonoBehaviour
    {
        public static PlayerSkillManager instance;
        public SkillSlot[] slot;
        [Header("DAMAGE SPIRIT EXTRA")]
        public float damageSpirit;
        private void Awake()
        {
            instance = this;
        }

        private void Update()
        {
          
            if (Keyboard.current.digit1Key.wasReleasedThisFrame && slot[0].canUse)
            {
                ActivateSkill(0); // Ativa a habilidade no slot 0
            }
        }

        private void ActivateSkill(int slotIndex)
        {
            // Verifica se a habilidade está desbloqueada e se não está em cooldown interno
            if (slot[slotIndex].skillSlot.isUnlocked && !slot[slotIndex].skillSlot.IsOnCooldown)
            {
                slot[slotIndex].skillSlot.Activate(gameObject); // Ativa a habilidade
                slot[slotIndex].canUse = false; // Inicia o cooldown do slot

                // Inicia o cooldown do slot APÓS a duração da habilidade
                StartCoroutine(SkillDurationTimer(slotIndex));
            }
        }

        // Coroutine que gerencia o tempo de duração da habilidade
        private IEnumerator SkillDurationTimer(int slotIndex)
        {
            // Espera pela duração da habilidade
            Debug.Log("Habilidade ativada. Aguardando duração da habilidade...");
            yield return new WaitForSeconds(slot[slotIndex].skillSlot.cooldownDuration);

            // Quando a duração da habilidade terminar, inicia o cooldown do slot
            Debug.Log("Duração da habilidade terminou. Iniciando cooldown do slot.");
            StartCoroutine(SlotCooldownTimer(slotIndex)); // Inicia o cooldown do slot
        }

        // Coroutine que gerencia o cooldown do slot de habilidade
        private IEnumerator SlotCooldownTimer(int slotIndex)
        {
            SkillCooldownUI.instance.skillCooldownImage[slotIndex].enabled = true;
            float cooldownTime = slot[slotIndex].slotCooldownDuration;
            float elapsedTime = 0f;

            while (elapsedTime < cooldownTime)
            {
                elapsedTime += Time.deltaTime; // Acumula o tempo decorrido
                float remainingTime = cooldownTime - elapsedTime;
        
                // Atualiza o fillAmount baseado no tempo restante
                SkillCooldownUI.instance.skillCooldownImage[slotIndex].fillAmount = remainingTime / cooldownTime;

                Debug.Log($"Cooldown do slot: {remainingTime} segundos restantes.");
                yield return null; // Espera até o próximo frame
            }

            // Após o cooldown do slot, a habilidade pode ser usada novamente
            slot[slotIndex].canUse = true;
            Debug.Log("Cooldown do slot terminou. Habilidade pronta para uso novamente.");
            SkillCooldownUI.instance.skillCooldownImage[slotIndex].enabled = false; // Desativa a imagem do cooldown ao final
        }

    }
}
