using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RPGKarawara {
    public class DamageCollider : MonoBehaviour
    {
        [Header("Collider")]
        [SerializeField] protected Collider damageCollider;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public Collider QaDamageCollider => damageCollider;
#endif

        [Header("Damage")]
        public float physicalDamage = 0;
        [Header("Para lugares que o escudo não ira proteger")] public float magicDamage = 0;
        public float fireDamage = 0;
        public float holyDamage = 0;

        [Header("Contact Point")]
        protected Vector3 contactPoint;

        [Header("Characters Damaged")]
        protected List<CharacterManager> charactersDamaged = new List<CharacterManager>();

        [Header("Pet Check")]
        public bool isPet; // Indica se é um pet

        protected virtual void Awake()
        {
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            CharacterManager damageTarget = other.GetComponentInParent<CharacterManager>();

            if (damageTarget != null){
                // Multiple colliders on one character still belong to the same attack target.
                if (charactersDamaged.Contains(damageTarget))
                    return;

                //contactPoint = other.gameObject.GetComponent<Collider>().ClosestPointOnBounds(transform.position);

                //  CHECK IF WE CAN DAMAGE THIS TARGET BASED ON FRIENDLY FIRE

                //  CHECK IF TARGET IS BLOCKING

                //damageTarget.IsOwner
                if (isPet && other.CompareTag("Player"))
                    return;

                CheckForBlock(damageTarget);
                DamageTarget(damageTarget);
            }
        }

        protected virtual void CheckForBlock(CharacterManager damageTarget){
            if (damageTarget.characterNetworkManager.isBlocking.Value){
                charactersDamaged.Add(damageTarget);
                TakeBlockedDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.TakeBlockedDamageEffect);
                damageEffect.physicalDamage = physicalDamage;
                damageEffect.magicDamage = magicDamage;
                damageEffect.fireDamage = fireDamage;
                damageEffect.holyDamage = holyDamage;

                damageTarget.characterEffectsManager.ProcessOwnedInstantEffect(damageEffect);
            }
        }

        public void DamagePublic(CharacterManager damageTarget){
            DamageTarget(damageTarget);
        }
        protected virtual void DamageTarget(CharacterManager damageTarget)
        {
            //  WE DON'T WANT TO DAMAGE THE SAME TARGET MORE THAN ONCE IN A SINGLE ATTACK
            //  SO WE ADD THEM TO A LIST THAT CHECKS BEFORE APPLYING DAMAGE

            charactersDamaged.Add(damageTarget);

            TakeDamageEffect damageEffect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
            damageEffect.physicalDamage = physicalDamage;
            damageEffect.magicDamage = magicDamage;
            damageEffect.fireDamage = fireDamage;
            damageEffect.holyDamage = holyDamage;
            damageEffect.contactPoint = contactPoint;

            damageTarget.characterEffectsManager.ProcessOwnedInstantEffect(damageEffect);
        }

        public virtual void EnableDamageCollider()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            bool wasEnabled = damageCollider.enabled;
#endif
            damageCollider.enabled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (!wasEnabled) { RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this); DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Enabled); }
#endif
        }

        public virtual void DisableDamageCollider()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            bool wasEnabled = damageCollider.enabled;
#endif
            damageCollider.enabled = false;
            charactersDamaged.Clear(); // Resetar alvos atingidos
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (wasEnabled) { RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this); DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Disabled); }
#endif
        }
    }
}
