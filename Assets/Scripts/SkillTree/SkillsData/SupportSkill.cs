using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Support Skill", menuName = "Skills/SupportSkill")]
    public class SupportSkill : Skill {
        
        public bool showSkillJacare = false;
        [HideInInspector] public GameObject skillPrefabJacare;
        
        
        [HideInInspector]public float _healingRate;       // Taxa de cura por segundo
        [HideInInspector]public int _healingAmount;  
        [HideInInspector]public GameObject healCirclePrefab;
        public bool showHealVariables = true;
        protected override void Execute(GameObject user) {
            
            if (healCirclePrefab != null) {
                GameObject healCircle = Instantiate(healCirclePrefab, user.transform.position, user.transform.rotation);
                HealCircle healCircleScript = healCircle.GetComponent<HealCircle>();
                DurationSkill durationSkillScript = healCircle.GetComponent<DurationSkill>();
                durationSkillScript.Init(cooldownDuration);
                healCircleScript.Initialize(_healingRate, _healingAmount);

            }

            if (skillPrefabJacare != null){
                GameObject jacare  = Instantiate(skillPrefabJacare, user.transform.position, user.transform.rotation);
                SkillJacare jacareScript = jacare.GetComponent<SkillJacare>();
                jacareScript.skillDuration = cooldownDuration;
                DurationSkill durationSkillScript = jacare.GetComponent<DurationSkill>();
                durationSkillScript.Init(cooldownDuration);
                
            }
            
        }
    }
}