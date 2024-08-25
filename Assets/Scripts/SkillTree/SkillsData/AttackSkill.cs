using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Attack Skill", menuName = "Skills/AttackSkill")]
    public class AttackSkill : Skill {
        
        protected override void Execute(GameObject user) {
            if (prefab != null) {
                GameObject projectile = Instantiate(prefab, user.transform.position + user.transform.forward, user.transform.rotation);
                DurationSkill dSkill = projectile.GetComponent<DurationSkill>();
                if (dSkill != null) {
                    dSkill.Init(cooldownDuration);
                } else {
                    Debug.LogWarning("DurationSkill component not found on projectile.");
                }
            }
        }
    }
}