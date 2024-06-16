using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(fileName = "New Health Skill", menuName = "Skills/HealthSkill")]
    public class HealthSkillData : SkillData
    {
        public int healthIncrease;

        public override void Apply()
        {
            CharacterNetworkManager characterNetworkManager = FindObjectOfType<CharacterNetworkManager>();
            if (characterNetworkManager != null && characterNetworkManager.IsOwner){
                characterNetworkManager.AddMaxHealthServerRpc(healthIncrease);
            }
        }

        public override bool BuyXp()
        {
            
            var systemXp = SystemXP.Instance;

            if (systemXp.currentLevel >= xp)
            {
                Debug.Log("comprei");
                systemXp.currentLevel -= xp;
                Apply();
                return true;
            }

            return false;
        }
    }
}
