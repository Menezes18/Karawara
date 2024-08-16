using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace RPGKarawara {
    public class PeriodicDamage : MonoBehaviour
    {
 [Header("Collider")]
        [SerializeField] protected Collider damageCollider;

        [Header("Damage Over Time Settings")]
        public float physicalDamagePerTick = 10f;
        public float magicDamagePerTick = 0f;
        public float fireDamagePerTick = 0f;
        public float lightningDamagePerTick = 0f;
        public float holyDamagePerTick = 0f;
        public float tickInterval = 1f;  // Interval in seconds

        [Header("Contact Point")]
        protected Vector3 contactPoint;

        [Header("Characters Damaged")]
        protected List<CharacterManager> charactersDamaged = new List<CharacterManager>();

        private Dictionary<CharacterManager, Coroutine> damageCoroutines = new Dictionary<CharacterManager, Coroutine>();

        private void Awake()
        {
            // Optional initialization logic
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null)
            {
                contactPoint = other.gameObject.GetComponent<Collider>().ClosestPointOnBounds(transform.position);

                if (!damageCoroutines.ContainsKey(damageTarget))
                {
                    Coroutine damageCoroutine = StartCoroutine(ApplyDamageOverTime(damageTarget));
                    damageCoroutines.Add(damageTarget, damageCoroutine);
                }
            }
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null && damageCoroutines.ContainsKey(damageTarget))
            {
                StopCoroutine(damageCoroutines[damageTarget]);
                damageCoroutines.Remove(damageTarget);
            }
        }

        private IEnumerator ApplyDamageOverTime(CharacterManager damageTarget)
        {
            while (true)
            {
                // Apply damage to the character
                TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
                damageEffect.physicalDamage = physicalDamagePerTick;
                damageEffect.magicDamage = magicDamagePerTick;
                damageEffect.fireDamage = fireDamagePerTick;
                damageEffect.lightningDamage = lightningDamagePerTick;
                damageEffect.holyDamage = holyDamagePerTick;
                damageEffect.contactPoint = contactPoint;

                damageTarget.characterEffectsManager.ProcessInstantEffect(damageEffect);

                yield return new WaitForSeconds(tickInterval);
            }
        }

        public virtual void EnableDamageCollider()
        {
            damageCollider.enabled = true;
        }

        public virtual void DisableDamageCollider()
        {
            damageCollider.enabled = false;
            charactersDamaged.Clear(); // Reset the characters that have been hit when we reset the collider, so they may be hit again

            // Stop all coroutines
            foreach (var coroutine in damageCoroutines.Values)
            {
                StopCoroutine(coroutine);
            }
            damageCoroutines.Clear();
        }
    }
}