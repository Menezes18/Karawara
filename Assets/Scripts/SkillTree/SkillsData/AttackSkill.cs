using UnityEngine;
public enum SkillsAttack{
    
}
namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Attack Skill", menuName = "Skills/AttackSkill")]
    public class AttackSkill : Skill {
        
        public GameObject projectilePrefab;

        protected override void Execute(GameObject user) {
            if (projectilePrefab != null) {
                GameObject projectile = Instantiate(projectilePrefab, user.transform.position + user.transform.forward, user.transform.rotation);
                DurationSkill dSkill = projectile.GetComponent<DurationSkill>();
                dSkill.Init(cooldownDuration);
            }
        }
    }
}