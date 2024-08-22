using UnityEngine;

public enum SkillsAttack {
    none,
    Passaro,
    Lobo_Attack
}

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Attack Skill", menuName = "Skills/AttackSkill")]
    public class AttackSkill : Skill {
        
        public GameObject passaroPrefab;
        public GameObject loboPrefab;
        public SkillsAttack selectedSkill;

        protected override void Execute(GameObject user) {
            GameObject projectilePrefab = null;

            switch (selectedSkill) {
                case SkillsAttack.Passaro:
                    projectilePrefab = passaroPrefab;
                    break;
                case SkillsAttack.Lobo_Attack:
                    projectilePrefab = loboPrefab;
                    break;
                default:
                    Debug.LogWarning("Skill not defined.");
                    return;
            }

            if (projectilePrefab != null) {
                GameObject projectile = Instantiate(projectilePrefab, user.transform.position + user.transform.forward, user.transform.rotation);
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