using UnityEngine;

namespace RPGKarawara.SkillTree{
    [CreateAssetMenu(fileName = "New Skill", menuName = "Skill", order = 0)]
    public class SkillScriptable : ScriptableObject{
        
    }


    public class UpgraadeData{
        public StateTypes stateType;
        public int skillIncreaaseAmount;
        public bool isPercentage;
    }


    public enum StateTypes{
        Strength,
        Speed, 
        Life
    }
}