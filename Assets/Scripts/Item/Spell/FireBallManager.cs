using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPGKarawara
{
    public class FireBallManager : SpellManager
    {
        // - What does this script do? - This script serves as a central hub to manipulate and adjust the fireball spell once its active, doing things such as ...
        // 1. Making the spell slightly "curve" or "follow" its lock targets as they are moving
        // 2. Assigning damage neatly with a function from this script
        // 3. Enabling/Disabling VFX and SFX, such as "contact" explosions, trails, ect

        // OPTIONAL THINGS TO DO: IF YOU HAVE MANY SPELLS THAT SHARE THIS LOGIC (Spell Target, Collider, Impact Particles ect) MAKE A BASE CLASS

        [Header("Colliders")]
        public FireBallDamageCollider damageCollider;

        [Header("Instantiated FX")]
        private GameObject instantiatedDestructionFX;

        private bool hasCollided = false;
        public bool isFullyCharged = false;
        private Rigidbody fireBallRigidbody;
        private Coroutine destructionFXCoroutine;

        protected override void Awake()
        {
            base.Awake();

            fireBallRigidbody = GetComponent<Rigidbody>();
        }
        

        protected override void Update()
        {
            base.Update();

            if (spellTarget != null)
                transform.LookAt(spellTarget.transform);

            if (fireBallRigidbody != null)
            {
                Vector3 currentVelocity = fireBallRigidbody.velocity;
                fireBallRigidbody.velocity = transform.forward + currentVelocity;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            //  IF WE COLLIDE WITH A CHARACTER, IGNORE THIS WE WILL LET THE DAMAGE COLLIDER HANDLE CHARACTER COLLISIONS, THIS IS JUST FOR IMPACT VFX
            if (collision.gameObject.layer == 6)
                return;

            if (!hasCollided)
            {
                hasCollided = true;
                InstantiateSpellDestructionFX();
            }
        }

        public void InitializeFireBall(CharacterManager spellCaster)
        {
            damageCollider.spellCaster = spellCaster;

            //  TO DO SET UP DAMAGE FORMULA TO CALCULATE DAMAGE BASED ON CHARACTERS STATS, SPELL POWER AND SPELL CASTING WEAPON'S SPELL BUFF
            damageCollider.fireDamage = 25;

            if (isFullyCharged)
                damageCollider.fireDamage *= 1.4f;
        }

        public void InstantiateSpellDestructionFX()
        {
            if (isFullyCharged)
            {
                instantiatedDestructionFX = Instantiate(impactParticleFullCharge, transform.position, Quaternion.identity);
            }
            else
            {
                instantiatedDestructionFX = Instantiate(impactParticle, transform.position, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        public void WaitThenInstantiateSpellDestructionFX(float timeToWait)
        {
            if (destructionFXCoroutine != null)
                StopCoroutine(destructionFXCoroutine);

            destructionFXCoroutine = StartCoroutine(WaitThenInstantiateFX(timeToWait));
            StartCoroutine(WaitThenInstantiateFX(timeToWait));
        }

        private IEnumerator WaitThenInstantiateFX(float timeToWait)
        {
            yield return new WaitForSeconds(timeToWait);

            InstantiateSpellDestructionFX();
        }
    }
}
