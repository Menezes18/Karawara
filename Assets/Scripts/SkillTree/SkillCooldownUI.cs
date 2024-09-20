using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace RPGKarawara.SkillTree
{
    public class SkillCooldownUI : MonoBehaviour
    {
        public static SkillCooldownUI instance;
        public Image[] skillCooldownImage;

        [Header("Skill Attack")]
        public Image attackCooldownImage;
        public Image attackBackgroundImage;
        [Header("Skill Defense")]
        public Image defenseCooldownImage;
        public Image defenseBackgroundImage;

        [Header("Skill Support")]
        public Image supportCooldownImage;
        public Image supportBackgroundImage;


        private void Awake()
        {
            instance = this;
        }

        private void Update(){
            if (PlayerSkillManager.instance == null)
                return;

            UpdateCooldownUI();

        }

        public void UpdateSkillCooldown(int i, float cooldownDuration, float remainingTime)
        {
            skillCooldownImage[i].enabled = true;
            skillCooldownImage[i].fillAmount = remainingTime / cooldownDuration;
        }
        private void UpdateCooldownUI()
        {
               
            if (PlayerSkillManager.instance.slot[0].skillSlot != null){
                attackCooldownImage.sprite = PlayerSkillManager.instance.slot[0].skillSlot.sprite;
                attackBackgroundImage.sprite = PlayerSkillManager.instance.slot[0].skillSlot.sprite;
                attackCooldownImage.fillAmount = PlayerSkillManager.instance.slot[0].skillSlot.GetCooldownProgress();
            }
            
        }
        
        
        
    }
}