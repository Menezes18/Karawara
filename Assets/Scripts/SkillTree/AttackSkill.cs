using UnityEngine;

namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Attack Skill", menuName = "Skills/AttackSkill")]
    public class AttackSkill : Skill {
        public int damage;
        public float speed;
        public GameObject projectilePrefab;

        protected override void Execute(GameObject user) {
            if (projectilePrefab != null) {
                GameObject projectile = Instantiate(projectilePrefab, user.transform.position + user.transform.forward, user.transform.rotation);
                Projectile projectileScript = projectile.GetComponent<Projectile>();
                if (projectileScript != null) {
                    projectileScript.Initialize(damage, speed);
                }
            }
        }
    }
}