using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(fileName = "New Defense Skill", menuName = "Skills/DefenseSkill")]
    public class DefenseSkillData : SkillData
    {
        public float defenseIncrease;

        public override void Apply()
        {
           
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
