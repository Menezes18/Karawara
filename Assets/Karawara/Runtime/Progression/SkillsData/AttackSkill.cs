using UnityEngine;

public enum AttackSkillType{
    None,
    PetPantera,
    PetSpirit
}
namespace RPGKarawara.SkillTree {
    [CreateAssetMenu(fileName = "New Attack Skill", menuName = "Skills/AttackSkill")]
    public class AttackSkill : Skill {
        public AttackSkillType SkillType;
        protected override void Execute(GameObject user) {
            if (prefab != null) {
                if (SkillType == AttackSkillType.PetSpirit){
                    Debug.Log("A");
                }

                GameObject projectile = Instantiate(prefab, user.transform.position + user.transform.forward, user.transform.rotation);
                var owner = user.GetComponent<PlayerManager>();
                foreach (var wolf in projectile.GetComponentsInChildren<WolfAI>(true)) wolf.BindOwner(owner);
                foreach (var bird in projectile.GetComponentsInChildren<Orbita>(true)) bird.BindOwner(owner);
                PetAttackController petController = projectile.GetComponent<PetAttackController>();
                if (petController != null) { petController.BindOwner(user.GetComponent<PlayerManager>()); petController.BindSkillDuration(cooldownDuration); }
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
