using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Karawara.Core.Characters;
using System;

namespace RPGKarawara
{
    public class CharacterManager : MonoBehaviour
    {
        public bool isBoss;
        public CharacterRuntimeState State { get; private set; } = new CharacterRuntimeState();
        public ICharacterRegistry Registry { get; private set; }
        public int LocalId { get; private set; }
        public bool IsInitialized { get; private set; }
        private static readonly Dictionary<CharacterRuntimeState, CharacterManager> localCharacters = new Dictionary<CharacterRuntimeState, CharacterManager>();
        private ICharacterRegistry resumeRegistry;
        private bool processingDeath;
        public ObservableValue<bool> isDead;
        public static CharacterManager Resolve(CharacterRuntimeState state)
        {
            if (state == null || !state.IsActive || state.IsDead) return null;
            return localCharacters.TryGetValue(state, out var value) && value != null && value.IsInitialized ? value : null;
        }

        [HideInInspector] public CharacterController characterController;

        public Animator animator;
        [HideInInspector] public CharacterNetworkManager characterNetworkManager;
        [HideInInspector] public CharacterEffectsManager characterEffectsManager;
        [HideInInspector] public CharacterAnimatorManager characterAnimatorManager;
        [HideInInspector] public CharacterStatsManager characterStatsManager;
        [HideInInspector] public CharacterCombatManeger characterCombatManager;
        [HideInInspector] public CharacterSoundFXManager characterSoundFXManager;
        [HideInInspector] public CharacterLocomotionManager characterLocomotionManager;
        [HideInInspector] public CharacterUIManager characterUIManager;

        [Header("Character Group")]
        public CharacterGroup characterGroup;

        public bool isPerformingAction { get => State.IsPerformingAction; set => State.SetPerformingAction(value); }
        public GameObject vfxDead;
        protected virtual void Awake()
        {
            isDead = new ObservableValue<bool>(() => State.IsDead, dead => { if (dead) State.SetHealth(0); else if (State.IsDead) State.SetHealth(State.MaxHealth); });
            // Scene ownership is retained until the session explicitly adopts the player.

            characterController = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            characterNetworkManager = GetComponent<CharacterNetworkManager>();
            characterEffectsManager = GetComponent<CharacterEffectsManager>();
            characterAnimatorManager = GetComponent<CharacterAnimatorManager>();
            characterStatsManager = GetComponent<CharacterStatsManager>();
            characterCombatManager = GetComponent<CharacterCombatManeger>();
            characterSoundFXManager = GetComponent<CharacterSoundFXManager>();
            characterLocomotionManager = GetComponent<CharacterLocomotionManager>();
            characterUIManager = GetComponent<CharacterUIManager>();
        }

        protected virtual void Start()
        {
            IgnoreMyOwnColliders();
        }

        protected virtual void Update()
        {
            if (!IsInitialized) return;
            if (animator != null && characterLocomotionManager != null)
                animator.SetBool("isGrounded", characterLocomotionManager.isGrounded);
        }

        protected virtual void FixedUpdate()
        {

        }

        protected virtual void LateUpdate()
        {

        }

        protected virtual void OnEnable()
        {
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.Enabled);
            if (resumeRegistry != null)
            {
                var registry = resumeRegistry;
                resumeRegistry = null;
                InitializeLocal(State, registry);
            }
        }

        protected virtual void OnDisable()
        {
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.Disabled);
            if (IsInitialized)
            {
                resumeRegistry = Registry;
                ShutdownLocal();
                State.SetActive(false);
            }
        }

        public virtual void InitializeLocal(CharacterRuntimeState state, ICharacterRegistry registry)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (IsInitialized && ReferenceEquals(State, state) && ReferenceEquals(Registry, registry)) return;
            ShutdownLocal();
            State = state;
            Registry = registry;
            State.SetActive(gameObject.activeInHierarchy);
            if (!State.IsActive) throw new InvalidOperationException("Initialize an active character.");
            if (!State.IsDead) LocalId = registry.Register(State);
            localCharacters[State] = this;
            IsInitialized = true;
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.LocalInitialized);
            processingDeath = false;
            if (characterNetworkManager != null) characterNetworkManager.BindState(State);
            State.HealthChanged += OnLocalHealthChanged;
            State.MovingChanged += OnLocalMovingChanged;
            State.ActiveChanged += OnLocalActiveChanged;
            State.ChargingAttackChanged += OnLocalChargingChanged;
            OnLocalMovingChanged(false, State.IsMoving);
            if (State.IsDead) OnLocalHealthChanged(State.Health, State.Health);
        }

        private void OnLocalHealthChanged(int previous, int next)
        {
            if (!(this is PlayerManager) && characterUIManager != null && characterUIManager.characterHPBar != null)
                characterUIManager.OnHPChanged(previous, next);
            if (characterNetworkManager != null) characterNetworkManager.CheckHP(previous, next);
        }
        private void OnLocalMovingChanged(bool previous, bool next) { if (animator != null) animator.SetBool("isMoving", next); }
        private void OnLocalChargingChanged(bool previous, bool next)
        {
            if (animator == null) return;
            animator.SetBool("isChargingAttack", next);
            if (!next) animator.SetBool("Released", true);
        }
        private void OnLocalActiveChanged(bool previous, bool next) { if (characterNetworkManager != null) characterNetworkManager.OnIsActiveChanged(previous, next); else if (gameObject.activeSelf != next) gameObject.SetActive(next); }
        public void BeginDeath()
        {
            if (processingDeath) return;
            processingDeath = true;
            isPerformingAction = false;
            State.SetAttacking(false);
            State.SetChargingAttack(false);
            State.SetIsChargingRightSpell(false);
            State.SetIsChargingLeftSpell(false);
            foreach (var hitbox in GetComponentsInChildren<DamageCollider>(true)) hitbox.DisableDamageCollider();
            if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(ProcessDeathEvent());
        }

        public virtual void ShutdownLocal()
        {
            if (!IsInitialized) return;
            IsInitialized = false;
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.LocalShutdown);
            StopAllCoroutines();
            State.HealthChanged -= OnLocalHealthChanged;
            State.MovingChanged -= OnLocalMovingChanged;
            State.ActiveChanged -= OnLocalActiveChanged;
            State.ChargingAttackChanged -= OnLocalChargingChanged;
            if (characterNetworkManager != null) characterNetworkManager.UnbindState(State);
            Registry?.Unregister(LocalId);
            localCharacters.Remove(State);
            State.SetTarget(null);
            State.SetChargingAttack(false);
            State.SetAttacking(false);
            State.SetJumping(false);
            State.SetInvulnerable(false);
            State.SetSprinting(false);
            State.SetMoving(false);
            if (characterEffectsManager != null && characterEffectsManager.activeSpellWarmUpFX != null)
                Destroy(characterEffectsManager.activeSpellWarmUpFX);
            foreach (var collider in GetComponentsInChildren<MeleeWeaponDamageCollider>(true)) collider.DisableDamageCollider();
            LocalId = 0;
            Registry = null;
            isPerformingAction = false;
            characterLocomotionManager?.ResetLocalMotion();
            if (characterAnimatorManager != null) characterAnimatorManager.applyRootMotion = false;
        }

        public bool player = false;
        public virtual IEnumerator ProcessDeathEvent(bool manuallySelectDeathAnimation = false)
        {
            {
                State.SetHealth(0);
                isDead.Value = true;

                //  RESET ANY FLAGS HERE THAT NEED TO BE RESET
                //  NOTHING YET

                //  IF WE ARE NOT GROUNDED, PLAY AN AERIAL DEATH ANIMATION

                if (!manuallySelectDeathAnimation)
                {
                    if (characterAnimatorManager != null && animator != null) characterAnimatorManager.PlayTargetActionAnimation("Dead_01", true);
                    
                }
            }

            //  PLAY SOME DEATH SFX

            yield return new WaitForSeconds(2);

            //  AWARD PLAYERS WITH RUNES

            //  DISABLE CHARACTER
            if (player || this is PlayerManager) yield break;
            if (vfxDead != null)
            {
                GameObject vfx = Instantiate(vfxDead, transform.position, Quaternion.identity);
                Destroy(vfx, 3f);
            }
            yield return new WaitForSeconds(0.2f);
            this.gameObject.SetActive(false);
        }

        public virtual void ReviveCharacter()
        {
            StopAllCoroutines();
            processingDeath = false;
            State.SetHealth(State.MaxHealth);
            if (IsInitialized && Registry != null) LocalId = Registry.Register(State);
        }
        protected virtual void OnDestroy()
        {
            resumeRegistry = null;
            ShutdownLocal();
        }

        
        protected virtual void IgnoreMyOwnColliders()
        {
            Collider characterControllerCollider = GetComponent<Collider>();
            Collider[] damageableCharacterColliders = GetComponentsInChildren<Collider>();
            List<Collider> ignoreColliders = new List<Collider>();

            //  ADDS ALL OF OUR DAMAGEABLE CHARACTER COLLIDERS, TO THE LIST THAT WILL BE USED TO IGNORE COLLISIONS
            foreach (var collider in damageableCharacterColliders)
            {
                ignoreColliders.Add(collider);
            }

            //  ADDS OUR CHARACTER CONTROLLER COLLIDER TO THE LIST THAT WILL BE USED TO IGNORE COLLISIONS
            ignoreColliders.Add(characterControllerCollider);

            //  GOES THROUGH EVERY COLLIDER ON THE LIST, AND IGNORES COLLISION WITH EACH OTHER
            foreach (var collider in ignoreColliders)
            {
                foreach (var otherCollider in ignoreColliders)
                {
                    if (collider != null && otherCollider != null) Physics.IgnoreCollision(collider, otherCollider, true);
                }
            }
        }
    }
}
