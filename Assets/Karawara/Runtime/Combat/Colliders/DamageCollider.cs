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
        [Header("Para lugares que o escudo nÃ£o ira proteger")] public float magicDamage = 0;
        public float fireDamage = 0;
        public float holyDamage = 0;

        [Header("Contact Point")]
        protected Vector3 contactPoint;

        [Header("Characters Damaged")]
        protected List<CharacterManager> charactersDamaged = new List<CharacterManager>();

        [Header("Pet Check")]
        public bool isPet; // Indica se Ã© um pet

        public CharacterManager sourceCharacter;
        protected virtual CharacterManager SourceCharacter => sourceCharacter != null ? sourceCharacter : GetComponentInParent<CharacterManager>();

        protected virtual void Awake()
        {
            if (damageCollider == null) damageCollider = GetComponent<Collider>();
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


                if (isPet && other.CompareTag("Player"))
                    return;

                if (CanDamage(damageTarget)) DamageTarget(damageTarget);
            }
        }

        protected bool CanDamage(CharacterManager target)
        {
            if (target == null || charactersDamaged.Contains(target) || target.State.IsDead || target.State.IsInvulnerable) return false;
            var source = SourceCharacter;
            if (source != null && (!source.IsInitialized || !source.State.IsActive || source.State.IsDead)) return false;
            if (source == target) return false;
            if (isPet && target is PlayerManager) return false;
            if (source != null && WorldUtilityManager.Instance != null && !WorldUtilityManager.Instance.CanIDamageThisTarget(source.characterGroup, target.characterGroup)) return false;
            return true;
        }

        protected virtual void CheckForBlock(CharacterManager target) { }
        public void DamagePublic(CharacterManager target) { if (CanDamage(target)) DamageTarget(target); }
        protected virtual void DamageTarget(CharacterManager target)
        {
            if (!CanDamage(target)) return;
            charactersDamaged.Add(target);
            if (target.State.IsBlocking)
            {
                var blocked = Instantiate(WorldCharacterEffectsManager.instance.TakeBlockedDamageEffect);
                blocked.characterCausingDamage = SourceCharacter;
                blocked.physicalDamage = physicalDamage; blocked.magicDamage = magicDamage;
                blocked.fireDamage = fireDamage; blocked.holyDamage = holyDamage;
                target.characterEffectsManager.ProcessOwnedInstantEffect(blocked);
                return;
            }
            var damage = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
            damage.characterCausingDamage = SourceCharacter;
            damage.physicalDamage = physicalDamage; damage.magicDamage = magicDamage;
            damage.fireDamage = fireDamage; damage.holyDamage = holyDamage;
            damage.contactPoint = contactPoint;
            target.characterEffectsManager.ProcessOwnedInstantEffect(damage);
        }

        protected virtual void OnDisable()
        {
            if (damageCollider != null) damageCollider.enabled = false;
            charactersDamaged.Clear();
        }

        public virtual void EnableDamageCollider()
        {
            if (damageCollider == null) return;
            var source = SourceCharacter;
            if (source != null && (!source.isActiveAndEnabled || source.State.IsDead)) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            bool wasEnabled = damageCollider.enabled;
#endif
            if (!damageCollider.enabled) charactersDamaged.Clear();
            damageCollider.enabled = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (!wasEnabled) { RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource.NotifyStateChanged(this); DevTools.Performance.PerformanceActorLifecycleScope.NotifyVisualChild(this, DevTools.Performance.PerformanceActorLifecycle.Enabled); }
#endif
        }

        public virtual void DisableDamageCollider()
        {
            if (damageCollider == null) { charactersDamaged.Clear(); return; }
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
