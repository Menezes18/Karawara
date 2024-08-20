using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Support Skill", menuName = "Skills/SupportSkill")]
    public class SupportSkill : Skill {
        public int healingAmount;
        public GameObject healCirclePrefab;

        protected override void Execute(GameObject user) {
            
            if (healCirclePrefab != null) {
                GameObject healCircle = Instantiate(healCirclePrefab, user.transform.position, user.transform.rotation);
                HealCircle healCircleScript = healCircle.GetComponent<HealCircle>();
               
            }
            else{
                Debug.Log("null o prefab");
            }
        }
    }
}