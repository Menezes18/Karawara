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

        public Sprite saveSpriteBackground;

        public Image slotSpellOrLanca;
        private PlayerSkillManager owner;
        public void Bind(PlayerSkillManager player)
        {
            if (owner != null) owner.BindCooldownUI(null);
            owner = player;
            if (owner != null) owner.BindCooldownUI(this);
        }
        private void OnDestroy() { Bind(null); if (instance == this) instance = null; }
        private void Awake()
        {
            instance = this;
            if (attackBackgroundImage != null) saveSpriteBackground = attackBackgroundImage.sprite;
        }

        private void Update(){
            if (owner == null || owner.slot == null || owner.slot.Length < 3)
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

            if (owner.slot[0].skillSlot != null){
                attackCooldownImage.sprite = owner.slot[0].skillSlot.sprite;
                attackBackgroundImage.sprite = owner.slot[0].skillSlot.sprite;
                attackCooldownImage.fillAmount = owner.GetCooldownProgress(owner.slot[0].skillSlot);
            }
            else{
                attackBackgroundImage.sprite = saveSpriteBackground;
            }
            if (owner.slot[1].skillSlot != null){
                defenseCooldownImage.sprite = owner.slot[1].skillSlot.sprite;
                defenseBackgroundImage.sprite = owner.slot[1].skillSlot.sprite;
                defenseCooldownImage.fillAmount = owner.GetCooldownProgress(owner.slot[1].skillSlot);
            }
            else{
                defenseBackgroundImage.sprite = saveSpriteBackground;
            }
            if (owner.slot[2].skillSlot != null){
                supportCooldownImage.sprite = owner.slot[2].skillSlot.sprite;
                supportBackgroundImage.sprite = owner.slot[2].skillSlot.sprite;
                supportCooldownImage.fillAmount = owner.GetCooldownProgress(owner.slot[2].skillSlot);
            }else{
                supportBackgroundImage.sprite = saveSpriteBackground;
            }

        }




    }
}
