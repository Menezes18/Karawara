using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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
        public Skill novaSkillAdd;
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
            if(skill == null) return;
            textCabecalho.text = skill.name;
            descriptionCabecalho.text = skill.description;
        }

        private void Update(){
            if (Keyboard.current.lKey.wasReleasedThisFrame)
            {
                LoadSkillsFromData(WorldSaveGameManager.instance.characterSlot01);
            }
        }

        public void ClearCabecalho(){
            textCabecalho.text = "";
            descriptionCabecalho.text = "";
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
                    slot.SetTriangleColor(Color.white);
                }
            }
        }

        public CharacterSaveData SaveSkillsToData(CharacterSaveData saveData)
        {
            

            // Copia as habilidades para o saveData
            saveData.SkillAttack = attackSkills != null ? (Skill[])attackSkills.Clone() : new Skill[0];
            saveData.SkillDefense = defenseSkills != null ? (Skill[])defenseSkills.Clone() : new Skill[0];
            saveData.SkillSuport = supportSkills != null ? (Skill[])supportSkills.Clone() : new Skill[0];

            Debug.Log("Habilidades salvas no CharacterSaveData.");
            return saveData;
        }

        // Método para carregar habilidades do CharacterSaveData
        public void LoadSkillsFromData(CharacterSaveData saveData)
        {
            if (saveData == null) return;

            // Copia as habilidades de volta para os arrays originais
            attackSkills = saveData.SkillAttack != null ? (Skill[])saveData.SkillAttack.Clone() : new Skill[0];
            defenseSkills = saveData.SkillDefense != null ? (Skill[])saveData.SkillDefense.Clone() : new Skill[0];
            supportSkills = saveData.SkillSuport != null ? (Skill[])saveData.SkillSuport.Clone() : new Skill[0];

            // Atualiza os slots da UI
            SetupSkillSlots(attackSkills, attackSkillSlots);
            SetupSkillSlots(defenseSkills, defenseSkillSlots);
            SetupSkillSlots(supportSkills, supportSkillSlots);

            Debug.Log("Habilidades carregadas do CharacterSaveData.");
        }
        public void AddSkill(Skill newSkill, SkillType skillType){
            newSkill.isUnlocked = true;
           // ((IList)allSkills).Add(newSkill);

            switch(skillType){
                case SkillType.Attack:
                    Array.Resize(ref attackSkills, attackSkills.Length + 1);
                    attackSkills[attackSkills.Length - 1] = newSkill;
                    SetupSkillSlots(attackSkills, attackSkillSlots);
                    break;

                case SkillType.Defense:
                    Array.Resize(ref defenseSkills, defenseSkills.Length + 1);
                    defenseSkills[defenseSkills.Length - 1] = newSkill;
                    SetupSkillSlots(defenseSkills, defenseSkillSlots);
                    break;

                case SkillType.Support:
                    Array.Resize(ref supportSkills, supportSkills.Length + 1);
                    supportSkills[supportSkills.Length - 1] = newSkill;
                    SetupSkillSlots(supportSkills, supportSkillSlots);
                    break;
            }
        }


        public void AdicionarSkill(Skill  novaSkill){ 
            AddSkill(novaSkill, SkillType.Attack);
            UnlockSkill(novaSkill); 
        }
    }
}