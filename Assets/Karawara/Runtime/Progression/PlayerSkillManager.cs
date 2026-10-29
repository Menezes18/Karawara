using System.Collections;
using System.Collections.Generic;
using Karawara.Core.Progression;
using UnityEngine;
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
        private readonly Dictionary<Skill, SkillRuntimeState> runtime = new Dictionary<Skill, SkillRuntimeState>();
        private SkillCooldownUI cooldownUi;
        public void BindCooldownUI(SkillCooldownUI ui) => cooldownUi = ui;
        public SkillRuntimeState GetRuntimeState(Skill definition)
        {
            if (definition == null) return null;
            if (!runtime.TryGetValue(definition, out var state))
            {
                // Unity references remain the legacy key; no persistent ID is fabricated in 3A.
                state = new SkillRuntimeState(null, definition.cooldownDuration);
                if (definition.isUnlocked) state.Progression.Unlock();
                runtime.Add(definition, state);
            }
            return state;
        }
        public bool IsSkillUnlocked(Skill definition) => GetRuntimeState(definition)?.Progression.IsUnlocked ?? false;
        public bool IsSkillEquipped(Skill definition) => GetRuntimeState(definition)?.Progression.IsEquipped ?? false;
        public void UnlockSkill(Skill definition) => GetRuntimeState(definition)?.Progression.Unlock();
        public float GetCooldownProgress(Skill definition) => GetRuntimeState(definition)?.Progress(Time.time) ?? 0;
        public void ResetSkillCooldown(Skill definition) => GetRuntimeState(definition)?.ResetCooldown();
        public void ResetRuntimeState() { StopAllCoroutines(); runtime.Clear(); }
        public void ResetAllCooldowns()
        {
            StopAllCoroutines();
            foreach (var state in runtime.Values) state.ResetCooldown();
            if (slot != null) foreach (var entry in slot) if (entry != null) entry.canUse = true;
        }
        public void EquipSkill(Skill definition, int index)
        {
            if (slot == null || index < 0 || index >= slot.Length || (definition != null && !IsSkillUnlocked(definition))) return;
            if (slot[index] == null) slot[index] = new SkillSlot();
            var previous = slot[index].skillSlot;
            slot[index].skillSlot = definition;
            if (previous != null && !System.Array.Exists(slot, s => s != null && s.skillSlot == previous)) GetRuntimeState(previous).Progression.SetEquipped(false);
            if (definition != null) GetRuntimeState(definition).Progression.SetEquipped(true);
        }
        public bool TryActivateDefinition(Skill definition)
        {
            var state = GetRuntimeState(definition);
            if (state == null || !state.TryActivate(Time.time)) return false;
            definition.ExecuteForUser(gameObject);
            return true;
        }
        private void OnDestroy()
        {
            runtime.Clear(); cooldownUi = null;
            if (instance == this) instance = null;
        }
        private void Awake()
        {
            instance = this;
        }

        public void TryActivateSlot(int index)
        {
            var input = PlayerInputManager.instance;
            if (input == null || !input.CanRouteGameplay || input.player == null || input.player.gameObject != gameObject) return;
            if (slot == null || index < 0 || index >= slot.Length || slot[index] == null || !slot[index].canUse) return;
            ActivateSkill(index);
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
            foreach (var state in runtime.Values) { state.ResetCooldown(); state.Progression.SetEquipped(false); }

            for (int i = 0; i < slot.Length; i++)
            {
                if (slot[i] == null) slot[i] = new SkillSlot();
                slot[i].skillSlot = saveData.skillsSlot != null && i < saveData.skillsSlot.Length ? saveData.skillsSlot[i] : null;
                slot[i].canUse = true;
                if (slot[i].skillSlot != null) GetRuntimeState(slot[i].skillSlot).Progression.SetEquipped(true);
                var image = GetCooldownImage(i);
                if (image != null) image.enabled = false;
            }

            Debug.Log("Slots de habilidades carregados do CharacterSaveData.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public void QaActivateSkill(int slotIndex) => ActivateSkill(slotIndex);
#endif
        private void ActivateSkill(int slotIndex)
        {
            // Verifica se a habilidade está desbloqueada e se não está em cooldown interno
            if (slot == null || slotIndex < 0 || slotIndex >= slot.Length || slot[slotIndex] == null || !slot[slotIndex].canUse || slot[slotIndex].skillSlot == null) return;
            if (TryActivateDefinition(slot[slotIndex].skillSlot))
            {
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

        private Image GetCooldownImage(int slotIndex)
        {
            var ui = cooldownUi;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            // Older isolated QA coroutine fixtures do not have a composed player HUD.
            if (ui == null && !Application.isPlaying) ui = SkillCooldownUI.instance;
#endif
            if (ui == null || ui.skillCooldownImage == null || slotIndex < 0 || slotIndex >= ui.skillCooldownImage.Length) return null;
            return ui.skillCooldownImage[slotIndex];
        }

    }
}
