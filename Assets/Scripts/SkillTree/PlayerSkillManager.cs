using System.Collections;
using Unity.VisualScripting;
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
            var keyboard = Keyboard.current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            keyboard = RPGKarawara.DevTools.Performance.PerformanceSkillRetentionSources.SkillKeyboard;
#endif
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasReleasedThisFrame && slot[0].canUse)
            {
                ActivateSkill(0); 
            }
            if (keyboard.digit2Key.wasReleasedThisFrame && slot[1].canUse)
            {
                ActivateSkill(1); 
            }
            if (keyboard.digit3Key.wasReleasedThisFrame && slot[2].canUse)
            {
                ActivateSkill(2); 
            }
            if (keyboard.lKey.wasReleasedThisFrame)
            {
                if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.ApplyPendingSaveWhenReady();
            }
        }

        // Método para salvar o estado dos slots de habilidades no CharacterSaveData
        public void SaveSkillSlot(CharacterSaveData saveData)
        {
            if (saveData == null) return;

            // Cria o array com o mesmo tamanho que o array de slots de habilidades
            saveData.skillsSlot = new Skill[slot.Length];

            // Copia as habilidades do slot para saveData.skillsSlot
            for (int i = 0; i < slot.Length; i++)
            {
                if (slot[i] != null)
                {
                    saveData.skillsSlot[i] = slot[i].skillSlot;
                }
            }

            Debug.Log("Slots de habilidades salvos no CharacterSaveData.");
        }

        // Método para carregar o estado dos slots de habilidades do CharacterSaveData
        public void LoadSkillSlot(CharacterSaveData saveData)
        {
            if (saveData == null || slot == null) return;

            // A previously equipped skill or cooldown cannot leak from another character slot.
            StopAllCoroutines();

            for (int i = 0; i < slot.Length; i++)
            {
                if (slot[i] == null) slot[i] = new SkillSlot();
                slot[i].skillSlot = saveData.skillsSlot != null && i < saveData.skillsSlot.Length ? saveData.skillsSlot[i] : null;
                slot[i].canUse = true;
                if (SkillCooldownUI.instance != null && SkillCooldownUI.instance.skillCooldownImage != null &&
                    i < SkillCooldownUI.instance.skillCooldownImage.Length && SkillCooldownUI.instance.skillCooldownImage[i] != null)
                    SkillCooldownUI.instance.skillCooldownImage[i].enabled = false;
            }

            Debug.Log("Slots de habilidades carregados do CharacterSaveData.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public void QaActivateSkill(int slotIndex) => ActivateSkill(slotIndex);
#endif
        private void ActivateSkill(int slotIndex)
        {
            // Verifica se a habilidade está desbloqueada e se não está em cooldown interno
            if(slot[slotIndex] == null) return; 
             if (slot[slotIndex] == null || slot[slotIndex].skillSlot == null) return;
            if (slot[slotIndex].skillSlot.isUnlocked && !slot[slotIndex].skillSlot.IsOnCooldown && slot[slotIndex].skillSlot != null)
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
            var image = GetCooldownImage(slotIndex);
            if (image != null) image.enabled = true;
            float cooldownTime = slot[slotIndex].slotCooldownDuration;
            float elapsedTime = 0f;

            while (elapsedTime < cooldownTime)
            {
                elapsedTime += Time.deltaTime; // Acumula o tempo decorrido
                float remainingTime = cooldownTime - elapsedTime;
        
                // Atualiza o fillAmount baseado no tempo restante
                image = GetCooldownImage(slotIndex);
                if (image != null) image.fillAmount = remainingTime / cooldownTime;

                
                yield return null; // Espera até o próximo frame
            }

            // Após o cooldown do slot, a habilidade pode ser usada novamente
            slot[slotIndex].canUse = true;
            
            image = GetCooldownImage(slotIndex);
            if (image != null) image.enabled = false; // Desativa a imagem do cooldown ao final
        }

        private static Image GetCooldownImage(int slotIndex)
        {
            var ui = SkillCooldownUI.instance;
            if (ui == null || ui.skillCooldownImage == null || slotIndex < 0 || slotIndex >= ui.skillCooldownImage.Length) return null;
            return ui.skillCooldownImage[slotIndex];
        }

    }
}
