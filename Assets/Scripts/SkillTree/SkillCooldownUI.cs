using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace RPGKarawara.SkillTree
{
    public class SkillCooldownUI : MonoBehaviour
    {
        public static SkillCooldownUI instance;

        [Header("Skill Attack")]
        public Image attackCooldownImage;
        public Image attackBackgroundImage;
        public Image skillCooldownImage;
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


            if (PlayerSkillManager.instance.IsCooldownActive("attack") &&
                !PlayerSkillManager.instance.attackSkill.IsOnCooldown){
                UpdateUI();
            }
        }

        public void UpdateUI(){
                GameObject imageGameObject = skillCooldownImage.gameObject;
                imageGameObject.SetActive(true);
                skillCooldownImage.fillAmount = GetCooldownProgress() / 5;
            
        }
        private void UpdateCooldownUI()
        {
               
            if (PlayerSkillManager.instance.attackSkill != null){
                // if (PlayerSkillManager.instance.IsCooldownActive("attack")){
                //     skillCooldownImage.fillAmount
                // }
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
        
        private float progress = 5;
        public float GetCooldownProgress()
        {
            if (progress > 0)
            {
                progress -= Time.deltaTime;
                progress = Mathf.Clamp(progress, 0, PlayerSkillManager.instance.skilldelay); 
                
            }

            return progress; 
        }
        
    }
}