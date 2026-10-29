using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class DurkClubDamageCollider : DamageCollider
    {
        [SerializeField] AIBossCharacterManager bossCharacter;

        protected override CharacterManager SourceCharacter => bossCharacter;

        protected override void Awake()
        {
            base.Awake();

            damageCollider = GetComponent<Collider>();
            bossCharacter = GetComponentInParent<AIBossCharacterManager>();
            DisableDamageCollider();
        }

        private void LateUpdate()
        {
            if (damageCollider.enabled && (bossCharacter == null || !bossCharacter.isPerformingAction || bossCharacter.isDead.Value))
                DisableDamageCollider();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void DamageTarget(CharacterManager damageTarget)
        {
            //  WE DON'T WANT TO DAMAGE THE SAME TARGET MORE THAN ONCE IN A SINGLE ATTACK
            //  SO WE ADD THEM TO A LIST THAT CHECKS BEFORE APPLYING DAMAGE
            if (!CanDamage(damageTarget))
                return;

            charactersDamaged.Add(damageTarget);

            TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
            damageEffect.physicalDamage = physicalDamage;
            damageEffect.magicDamage = magicDamage;
            damageEffect.fireDamage = fireDamage;
            damageEffect.holyDamage = holyDamage;
            damageEffect.contactPoint = contactPoint;
            damageEffect.angleHitFrom = Vector3.SignedAngle(bossCharacter.transform.forward, damageTarget.transform.forward, Vector3.up);

            //  OPTION 01:
            //  THIS WILL APPLY DAMAGE IF THE A.I HITS ITS TARGET ON THE HOSTS SIDE REGARDLESS OF HOW IT LOOKS ON ANY OTHER CLIENTS SIDE


            //  OPTION 02:
            //  THIS WILL APPLY DAMAGE IF THE A.I HITS ITS TARGET ON THE CONNECTED CHARACTERS SIDE REGARDLESS OF HOW IT LOOKS ON ANY OTHER CLIENTS SIDE

            {
                damageTarget.characterNetworkManager.ApplyDamage(
                    damageTarget,
                    bossCharacter,
                    damageEffect.physicalDamage,
                    damageEffect.magicDamage,
                    damageEffect.fireDamage,
                    damageEffect.holyDamage,
                    damageEffect.poiseDamage,
                    damageEffect.angleHitFrom,
                    damageEffect.contactPoint.x,
                    damageEffect.contactPoint.y,
                    damageEffect.contactPoint.z);
                Destroy(damageEffect);
            }
        }
    }
}
