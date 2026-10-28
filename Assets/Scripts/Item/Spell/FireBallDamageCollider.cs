using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class FireBallDamageCollider : SpellProjectileDamageCollider
    {
        private FireBallManager fireBallManager;

        protected override void Awake()
        {
            base.Awake();

            fireBallManager = GetComponentInParent<FireBallManager>();
        }

        protected override void OnTriggerEnter(Collider other)
        {
            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null && spellCaster != null)
            {
                contactPoint = other.gameObject.GetComponent<Collider>().ClosestPointOnBounds(transform.position);

                //  WE DO NOT WANT TO DAMAGE OURSELVES
                if (damageTarget == spellCaster)
                    return;

                //  CHECK IF WE CAN DAMAGE THIS TARGET BASED ON FRIENDLY FIRE
                if (!WorldUtilityManager.Instance.CanIDamageThisTarget(spellCaster.characterGroup, damageTarget.characterGroup))
                    return;
                

                //  CHECK IF TARGET IS BLOCKING
                CheckForBlock(damageTarget);

                if (!damageTarget.characterNetworkManager.isInvulnerable.Value)
                    DamageTarget(damageTarget);

                fireBallManager.InstantiateSpellDestructionFX();
            }
        }
        
        /*
        protected override void GetBlockingDotValues(CharacterManager damageTarget)
        {
            directionFromAttackToDamageTarget = spellCaster.transform.position - damageTarget.transform.position;
            dotValueFromAttackToDamageTarget = Vector3.Dot(directionFromAttackToDamageTarget, damageTarget.transform.forward);
        }*/

        protected override void DamageTarget(CharacterManager damageTarget)
        {
            if (!CanDamage(damageTarget) || spellCaster == null) return;
            charactersDamaged.Add(damageTarget);
            float angle = Vector3.SignedAngle(spellCaster.transform.forward, damageTarget.transform.forward, Vector3.up);
            damageTarget.characterNetworkManager.ApplyDamage(damageTarget, spellCaster,
                physicalDamage, magicDamage, fireDamage, holyDamage, 0, angle,
                contactPoint.x, contactPoint.y, contactPoint.z);
        }
    }
}
