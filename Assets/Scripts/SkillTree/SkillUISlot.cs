using System;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class SkillUISlot : MonoBehaviour
    {
        public Image skillIcon;   // Imagem do ícone da habilidade
        public Button skillButton; // Botão para ativar a habilidade
        public Image lockIcon;     // Imagem para mostrar se a habilidade está bloqueada
        public Skill skill;       // Referência para a habilidade associada ao slot
        public Image trianguloIcon;
        public TooltipTrigger tooltipTrigger;
        public void Start(){
            if (skill == null){
                skillIcon.enabled = false;
                lockIcon.enabled = true;
            }
            tooltipTrigger = GetComponent<TooltipTrigger>();
            if (skill != null){
                tooltipTrigger.header = "Skill Bloqueada";
                if (skill.isUnlocked){
                    tooltipTrigger.header = skill.name + " habilitado!";
                }
            }
            else if (skill == null){
                tooltipTrigger.header = "Skill Bloqueada";
            }
        }

        // Configura o slot com a habilidade
        public void SetSkill(Skill newSkill)
        {
               
            skill = newSkill;

            if (skill != null)
            {
                skillIcon.sprite = skill.sprite; // Define o ícone da habilidade
                skillButton.interactable = skill.isUnlocked; // Habilita/desabilita o botão
                lockIcon.enabled = !skill.isUnlocked; // Mostra/esconde o ícone de cadeado

                // Se a habilidade estiver bloqueada, esconde o ícone
                skillIcon.enabled = skill.isUnlocked;
            }
            
            
        }
        public void SetTriangleColor(Color color)
        {
            trianguloIcon.color = color;
        }
        // Método chamado ao clicar no botão do slot
        public void OnSkillButtonClicked()
        {
            if (skill != null && skill.isUnlocked){
                SkillTreeUiManager.instance.Cabecalho(skill);
                tooltipTrigger.header =  skill.name + " ativada!"; 
                SkillTreeUiManager.instance.DisableSkillsExcept(skill.skillType, skill, this);
                TooltipSystem.current.Restart();
                skill.active = true;    
                trianguloIcon.color = new Color32(158,255,252, 255);
                skill.AddSkill();
            }
        }
    }
}