using System;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RPGKarawara
{
    public class SkillUISlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image skillIcon;   // Imagem do ícone da habilidade
        public Button skillButton; // Botão para ativar a habilidade
        public Image lockIcon;     // Imagem para mostrar se a habilidade está bloqueada
        public Skill skill;       // Referência para a habilidade associada ao slot
        public Image trianguloIcon;
        public TooltipTrigger tooltipTrigger;
        public int auxiliarSkill;

       [SerializeField] private Sprite spriteIconStart;
        private SkillTreeUiManager tree;
        public void Bind(SkillTreeUiManager owner) => tree = owner;
        private SkillTreeUiManager Tree => tree != null ? tree : GetComponentInParent<SkillTreeUiManager>();
        private PlayerSkillManager Owner => Tree != null ? Tree.Owner : null;
        private bool IsUnlocked => skill != null && Owner != null && Owner.IsSkillUnlocked(skill);
        
        public void Start(){
            
            spriteIconStart = skillIcon.sprite;
            if (skill == null){
                skillIcon.enabled = false;
                lockIcon.enabled = true;
            }
            tooltipTrigger = GetComponent<TooltipTrigger>();
            if (skill != null){
                tooltipTrigger.header = "Skill Bloqueada";
                if (IsUnlocked){
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
                if (skillIcon != null) { skillIcon.sprite = skill.sprite; skillIcon.enabled = IsUnlocked; }
                if (skillButton != null) skillButton.interactable = IsUnlocked;
                if (lockIcon != null) lockIcon.enabled = !IsUnlocked;
                SetTriangleColor(Owner != null && Owner.IsSkillEquipped(skill) ? new Color32(158, 255, 252, 255) : Color.white);
            }
            else
            {
                if (skillIcon != null) skillIcon.enabled = false;
                if (skillButton != null) skillButton.interactable = false;
                if (lockIcon != null) lockIcon.enabled = true;
                SetTriangleColor(Color.white);
            }
            
            
        }


        public void ClearSkill(){
            skillIcon.sprite = spriteIconStart;
        }
        public void SetTriangleColor(Color color)
        {
            if (trianguloIcon != null) trianguloIcon.color = color;
        }
        // Método chamado ao clicar no botão do slot
        public void OnSkillButtonClicked(int slotID)
        {
            if (IsUnlocked){
                Tree.Cabecalho(skill);
                tooltipTrigger.header =  skill.name + " ativada!"; 
                Tree.DisableSkillsExcept(skill.skillType, skill, this);
                TooltipSystem.current.Restart();
                Owner.EquipSkill(skill, slotID - 1);
                trianguloIcon.color = new Color32(158,255,252, 255);
                spriteIconStart = skillIcon.sprite;
            }
        }

        public void AtivarCabecalho(){
            Tree?.Cabecalho(skill);
        }
        public void OnUnlockButtonClicked(int skillIDSlot){
            
            if (skill != null){
                Tree?.ClearCabecalho();
               
                trianguloIcon.color = new Color32(255,255,252, 255);
                ClearSkill();
                Debug.Log("remove" + skillIDSlot + " slot");
                Owner?.EquipSkill(null, skillIDSlot - 1);
            }
        }

        public void OnPointerEnter(PointerEventData eventData){
            Tree?.Cabecalho(skill);
        }

        public void OnPointerExit(PointerEventData eventData){
            Tree?.ClearCabecalho();
        }
    }
}
