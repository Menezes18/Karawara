using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Support Skill", menuName = "Skills/SupportSkill")]
    public class SupportSkill : Skill {
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
            else{
                Debug.Log("null o prefab");
            }
        }
    }
}