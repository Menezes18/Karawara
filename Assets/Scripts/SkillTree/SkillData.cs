
using UnityEngine;

namespace RPGKarawara
{
    public abstract class SkillData : ScriptableObject
    {
        public string skillName;
        public int xp;
        public Sprite icon;
        
        public abstract void Apply();
        public abstract bool BuyXp();
    }
    
}
