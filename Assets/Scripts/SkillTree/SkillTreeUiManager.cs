using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RPGKarawara{
    public class SkillTreeUiManager : MonoBehaviour{
        public static SkillTreeUiManager instance;

        // Slots de habilidades na UI (referências para SkillUISlot)
        public SkillUISlot[] attackSkillSlots; // Slots de habilidades de ataque
        public SkillUISlot[] defenseSkillSlots; // Slots de habilidades de defesa
        public SkillUISlot[] supportSkillSlots; // Slots de habilidades de suporte

        // Referências às habilidades
        public Skill[] attackSkills; // Habilidades de ataque
        public Skill[] defenseSkills; // Habilidades de defesa
        public Skill[] supportSkills; // Habilidades de suporte
        public Skill[] allSkills;
        [Header("Dados Skill Direita")]
        public TextMeshProUGUI textCabecalho;
        public Sprite spriteCabecalho;
        public TextMeshProUGUI descriptionCabecalho;
        private void Awake(){
            instance = this;
            DeactivateAllSkillsAndResetCooldown();
        }

        private void Start(){
            // Configura os slots de habilidades na UI com base no estado de desbloqueio
            SetupSkillSlots(attackSkills, attackSkillSlots);
            SetupSkillSlots(defenseSkills, defenseSkillSlots);
            SetupSkillSlots(supportSkills, supportSkillSlots);
        }
        public void DeactivateAllSkillsAndResetCooldown()
        {
            // Encontra todas as instâncias de Skill no jogo
            allSkills = Resources.FindObjectsOfTypeAll<Skill>();

            // Desativa e reseta o cooldown de todas as habilidades
            foreach (Skill skill in allSkills)
            {
                if (skill != null)
                {
                    skill.active = false; // Desativa a habilidade
                    skill.ResetCooldown(); // Reseta o cooldown
                }
            }
        }

        // Método para configurar os slots de habilidade
        private void SetupSkillSlots(Skill[] skills, SkillUISlot[] skillSlots){
            for (int i = 0; i < skills.Length; i++){
                if (i < skillSlots.Length && skills[i] != null){
                    skillSlots[i].SetSkill(skills[i]); // Configura o slot com a habilidade
                }
            }
        }

        // Método para desbloquear uma habilidade específica
        public void UnlockSkill(Skill skill){
            skill.isUnlocked = true;
            Start(); // Reatualiza a UI
        }

        public void Cabecalho(Skill skill){
            textCabecalho.text = skill.name;
            descriptionCabecalho.text = skill.description;
        }
        public void DisableSkillsExcept(SkillType skillType, Skill skillToIgnore, SkillUISlot skillUISlot){
            SkillUISlot[] skillSlots = null;

            // Determine a lista de slots a ser usada com base no tipo de habilidade
            switch (skillType){
                case SkillType.Attack:
                    skillSlots = attackSkillSlots;
                    foreach (Skill skill in attackSkills){
                        if (skill != skillToIgnore){
                            skill.active = false;
                            UpdateSkillUISlot(skill, skillSlots);
                        }
                    }

                    break;

                case SkillType.Defense:
                    skillSlots = defenseSkillSlots;
                    foreach (Skill skill in defenseSkills){
                        if (skill != skillToIgnore){
                            skill.active = false;
                            UpdateSkillUISlot(skill, skillSlots);
                        }
                    }

                    break;

                case SkillType.Support:
                    skillSlots = supportSkillSlots;
                    foreach (Skill skill in supportSkills){
                        if (skill != skillToIgnore){
                            skill.active = false;
                            UpdateSkillUISlot(skill, skillSlots);
                        }
                    }

                    break;

                default:
                    Debug.LogWarning("Tipo de habilidade não reconhecido: " + skillType);
                    break;
            }

            // Atualizar o slot de habilidade específico
            if (skillUISlot != null){
                skillUISlot.SetTriangleColor(Color.white);
            }
        }

        private void UpdateSkillUISlot(Skill skill, SkillUISlot[] skillSlots){
            foreach (SkillUISlot slot in skillSlots){
                if (slot.skill == skill){
                    // Atualize a cor do triângulo com base no estado da habilidade
                    slot.SetTriangleColor(Color.white);
                }
            }
        }
    }
}