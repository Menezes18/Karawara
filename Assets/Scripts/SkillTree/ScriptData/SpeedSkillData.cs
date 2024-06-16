using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(fileName = "New Speed Skill", menuName = "Skills/SpeedSkill")]
    public class SpeedSkillData : SkillData
    {
        public float speedIncrease;

        public override void Apply()
        {
            CharacterNetworkManager characterNetworkManager = FindObjectOfType<CharacterNetworkManager>();
            if (characterNetworkManager != null && characterNetworkManager.IsOwner){
                characterNetworkManager.IncreaseSprintingSpeedServerRpc(speedIncrease);
            }
        }

        public override bool BuyXp()
        {
            var systemXp = SystemXP.Instance;

            if (systemXp.currentLevel >= xp)
            {
                systemXp.currentLevel -= xp;
                Apply();
                return true;
            }

            return false;
        }
    }
}
