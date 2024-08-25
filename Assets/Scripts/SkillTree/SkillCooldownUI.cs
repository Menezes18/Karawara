using UnityEngine;
using UnityEngine.UI;

namespace RPGKarawara.SkillTree
{
    public class SkillCooldownUI : MonoBehaviour
    {
        public static SkillCooldownUI instance;

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

        private void Update()
        {
            if(PlayerSkillManager.instance == null)
                return;
            
            UpdateCooldownUI();
            
           
        }

        private void UpdateCooldownUI()
        {
            if (PlayerSkillManager.instance.attackSkill != null){
                
                attackCooldownImage.sprite = PlayerSkillManager.instance.attackSkill.sprite;
                attackBackgroundImage.sprite = PlayerSkillManager.instance.attackSkill.sprite;
                attackCooldownImage.fillAmount = PlayerSkillManager.instance.attackSkill.GetCooldownProgress();
            }

            if (PlayerSkillManager.instance.defenseSkill != null)
            {
                defenseBackgroundImage.sprite = PlayerSkillManager.instance.defenseSkill.sprite;
                defenseCooldownImage.sprite = PlayerSkillManager.instance.defenseSkill.sprite;
                defenseCooldownImage.fillAmount = PlayerSkillManager.instance.defenseSkill.GetCooldownProgress();
            }

            if (PlayerSkillManager.instance.supportSkill != null)
            {
                supportBackgroundImage.sprite = PlayerSkillManager.instance.supportSkill.sprite;
                supportCooldownImage.sprite = PlayerSkillManager.instance.supportSkill.sprite;
                supportCooldownImage.fillAmount = PlayerSkillManager.instance.supportSkill.GetCooldownProgress();
            }
        }
        
    }
}