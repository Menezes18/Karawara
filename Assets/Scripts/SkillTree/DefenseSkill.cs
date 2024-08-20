using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Defense Skill", menuName = "Skills/DefenseSkill")]
    public class DefenseSkill : Skill {
        public int defenseValue;
        public GameObject barrierPrefab;

        protected override void Execute(GameObject user) {
            if (barrierPrefab != null) {
                Instantiate(barrierPrefab, user.transform.position, user.transform.rotation);
            }
        }
    }
}