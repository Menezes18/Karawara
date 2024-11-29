using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    [CreateAssetMenu(menuName = "Items/Spells/Fire Ball")]
    public class FireBallSpell : SpellItem
    {
        [Header("Projectile Velocity")]
        [SerializeField] float upwardVelocity = 3;
        [SerializeField] float forwardVelocity = 15;
        public override void AttemptToCastSpell(PlayerManager player)
        {
            base.AttemptToCastSpell(player);

            if (!CanICastThisSpell(player))
                return;

            if (player.playerNetworkManager.isUsingRightHand.Value)
            {
                player.playerAnimatorManager.PlayTargetActionAnimation(mainHandSpellAnimation, true);
            }
            else
            {
                player.playerAnimatorManager.PlayTargetActionAnimation(offHandSpellAnimation, true);
            }
        }

        public override void InstantiateWarmUpSpellFX(PlayerManager player)
        {
            base.InstantiateWarmUpSpellFX(player);

            // 1. DETERMINE WHICH HAND PLAYER IS USING
            SpellInstantiationLocation spellInstantiationLocation;
            GameObject instantiatedWarmUpSpellFX = Instantiate(spellCastWarmUpFX);

            if (player.playerNetworkManager.isUsingRightHand.Value)
            {
                // 2. INSTANTIATE WARM UP FX ON THE CORRECT PLACE. (INCANTATIONS JUST USE HAND, WHILST STAVES USE A POINT ON THE STAVE ITSELF)
                spellInstantiationLocation = player.playerEquipmentManager.rightWeaponManager.GetComponentInChildren<SpellInstantiationLocation>();
            }
            else
            {
                // 2. INSTANTIATE WARM UP FX ON THE CORRECT PLACE. (INCANTATIONS JUST USE HAND, WHILST STAVES USE A POINT ON THE STAVE ITSELF)
                spellInstantiationLocation = player.playerEquipmentManager.leftWeaponManager.GetComponentInChildren<SpellInstantiationLocation>();
            }

            instantiatedWarmUpSpellFX.transform.parent = spellInstantiationLocation.transform;
            instantiatedWarmUpSpellFX.transform.localPosition = Vector3.zero;
            instantiatedWarmUpSpellFX.transform.localRotation = Quaternion.identity;

            // 3. "SAVE" THE WARM UP FX AS A VARIABLE SO IT CAN BE DESTROYED IF THE PLAYER IS KNOCKED OUT OF THE ANIMATION
            player.playerEffectsManager.activeSpellWarmUpFX = instantiatedWarmUpSpellFX;
        }

        public override void SuccessfullyCastSpell(PlayerManager player)
        {
            base.SuccessfullyCastSpell(player);

            // 1. DESTROY ANY WARM UP FX REMAINING FROM THE SPELL
           // if (player.IsOwner)
                //player.playerCombatManager.DestroyAllCurrentActionFX();

            // 2. GET ANY COLLIDERS FROM THE CASTER
            Collider[] characterColliders = player.GetComponentsInChildren<Collider>();
            Collider characterCollisionCollider = player.GetComponent<Collider>();

            // 3. INSTANTIATE THE PROJECTILE
            SpellInstantiationLocation spellInstantiationLocation;
            GameObject instantiatedReleasedSpellFX = Instantiate(spellCastReleaseFX);

            if (player.playerNetworkManager.isUsingRightHand.Value)
            {
                // 2. INSTANTIATE WARM UP FX ON THE CORRECT PLACE. (INCANTATIONS JUST USE HAND, WHILST STAVES USE A POINT ON THE STAVE ITSELF)
                spellInstantiationLocation = player.playerEquipmentManager.rightWeaponManager.GetComponentInChildren<SpellInstantiationLocation>();
            }
            else
            {
                // 2. INSTANTIATE WARM UP FX ON THE CORRECT PLACE. (INCANTATIONS JUST USE HAND, WHILST STAVES USE A POINT ON THE STAVE ITSELF)
                spellInstantiationLocation = player.playerEquipmentManager.leftWeaponManager.GetComponentInChildren<SpellInstantiationLocation>();
            }

            //  WE DONT HAVE A DAMAGE COLLIDER YET, SO WE DONT NEED TO IGNORE COLLISIONS YET
            // 4. USE THE LIST OF COLLIDERS FROM THE CASTER AND NOW APPLY THE IGNORE PHYSICS WITH THE COLLIDERS FROM THE PROJECTILE
            instantiatedReleasedSpellFX.transform.parent = spellInstantiationLocation.transform;
            instantiatedReleasedSpellFX.transform.localPosition = Vector3.zero;
            instantiatedReleasedSpellFX.transform.localRotation = Quaternion.identity;
            instantiatedReleasedSpellFX.transform.parent = null;

            // 5. APPLY DAMAGE TO THE PROJECTILES DAMAGE COLLIDER

            // 6. SET THE PROJECTILES VELOCITY AND DIRECTION
            // TODO: MAKE PROJECTILES UP AND DOWN DIRECTION GET CHOOSEN BASED ON WHERE PLAYER IS LOOKING

            if (player.playerNetworkManager.isLockedOn.Value)
            {
                instantiatedReleasedSpellFX.transform.LookAt(player.playerCombatManager.currentTarget.transform.position);
            }
            else
            {
                Vector3 forwardDirection = player.transform.forward; // TODO: dps trocar isso para talvez a diferação da camera?
                instantiatedReleasedSpellFX.transform.forward = forwardDirection;
            }

            Rigidbody spellRigidbody = instantiatedReleasedSpellFX.GetComponent<Rigidbody>();
            Vector3 upwardVelocityVector = instantiatedReleasedSpellFX.transform.up * upwardVelocity;
            Vector3 forwardVelocityVector = instantiatedReleasedSpellFX.transform.forward * forwardVelocity;
            Vector3 totalVelocity = upwardVelocityVector + forwardVelocityVector;
            spellRigidbody.velocity = totalVelocity;
        }

        public override bool CanICastThisSpell(PlayerManager player)
        {
            if (player.isPerformingAction)
                return false;

            if (player.playerNetworkManager.isJumping.Value)
                return false;

            return true;
        }
    }
}
