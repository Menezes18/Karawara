using UnityEngine;
using Karawara.Core.Characters;
namespace RPGKarawara
{
    // Historical component name retained to preserve authored GUID references.
    // All values are local; gameplay values read and write the Core state directly.
    public class CharacterNetworkManager : MonoBehaviour
    {
        protected CharacterManager character;
        public ObservableValue<bool> isActive;
        public ObservableValue<bool> isMoving;
        public ObservableValue<bool> isBlocking;
        public ObservableValue<bool> isPowerPet;
        public ObservableValue<bool> isInvulnerable;
        public ObservableValue<bool> isLockedOn;
        public ObservableValue<bool> isSprinting;
        public ObservableValue<bool> isJumping;
        public ObservableValue<bool> isChargingAttack;
        public ObservableValue<bool> isAttacking;
        public ObservableValue<int> currentHealth;
        public ObservableValue<int> maxHealth;
        public ObservableValue<int> vitality;
        public ObservableValue<int> endurance;
        protected virtual void Awake() { character = GetComponent<CharacterManager>(); EnsureValues(); }
        public void EnsureValues()
        {
            if (character == null) character = GetComponent<CharacterManager>();
            if (currentHealth != null) return;
            isActive = new ObservableValue<bool>(() => character.State.IsActive, value => character.State.SetActive(value), true);
            isMoving = new ObservableValue<bool>(() => character.State.IsMoving, value => character.State.SetMoving(value), true);
            isBlocking = new ObservableValue<bool>(() => character.State.IsBlocking, value => character.State.SetBlocking(value), false);
            isPowerPet = new ObservableValue<bool>(() => character.State.IsPowerPet, value => character.State.SetPowerPet(value), false);
            isInvulnerable = new ObservableValue<bool>(() => character.State.IsInvulnerable, value => character.State.SetInvulnerable(value), false);
            isLockedOn = new ObservableValue<bool>(() => character.State.IsLockedOn, value => character.State.SetLockedOn(value), true);
            isSprinting = new ObservableValue<bool>(() => character.State.IsSprinting, value => character.State.SetSprinting(value), false);
            isJumping = new ObservableValue<bool>(() => character.State.IsJumping, value => character.State.SetJumping(value), false);
            isChargingAttack = new ObservableValue<bool>(() => character.State.IsChargingAttack, value => character.State.SetChargingAttack(value), true);
            isAttacking = new ObservableValue<bool>(() => character.State.IsAttacking, value => character.State.SetAttacking(value), false);
            currentHealth = new ObservableValue<int>(() => character.State.Health, value => character.State.SetHealth(value), true);
            maxHealth = new ObservableValue<int>(() => character.State.MaxHealth, value => character.State.SetMaxHealth(value), true);
            vitality = new ObservableValue<int>(() => character.State.Vitality, value => character.State.SetVitality(value), true);
            endurance = new ObservableValue<int>(() => character.State.Endurance, value => character.State.SetEndurance(value), false);
        }
        public virtual void BindState(CharacterRuntimeState state)
        {
            EnsureValues();
            state.ActiveChanged += isActive.Publish;
            state.MovingChanged += isMoving.Publish;
            state.LockedOnChanged += isLockedOn.Publish;
            state.ChargingAttackChanged += isChargingAttack.Publish;
            state.HealthChanged += currentHealth.Publish;
            state.MaxHealthChanged += maxHealth.Publish;
            state.VitalityChanged += vitality.Publish;
        }
        public virtual void UnbindState(CharacterRuntimeState state)
        {
            state.ActiveChanged -= isActive.Publish;
            state.MovingChanged -= isMoving.Publish;
            state.LockedOnChanged -= isLockedOn.Publish;
            state.ChargingAttackChanged -= isChargingAttack.Publish;
            state.HealthChanged -= currentHealth.Publish;
            state.MaxHealthChanged -= maxHealth.Publish;
            state.VitalityChanged -= vitality.Publish;
        }
        public virtual void CheckHP(int oldValue, int newValue)
        {
            if (newValue > 0) return;
            character.BeginDeath();
            if (!Application.isPlaying) return;
            var corruption = GameObject.FindWithTag("Corruption");
            if (corruption != null) corruption.SetActive(false);
        }
        public void AddHealth(int amount) => character.State.SetHealth(character.State.Health + amount);
        public void AddMaxHealth(int amount) => character.State.SetMaxHealth(Mathf.Max(0, character.State.MaxHealth + amount));
        public void IncreaseSprintingSpeed(float amount)
        { GetComponent<PlayerLocomotionManager>()?.IncreaseSprintingSpeed(amount); }
        public void OnIsLockedOnChanged(bool previous, bool next) { if (!next) character.characterCombatManager?.SetTarget(null); }
        public void OnIsChargingAttackChanged(bool previous, bool next)
        {
            if (character.animator == null) return;
            character.animator.SetBool("isChargingAttack", next);
            if (!next) character.animator.SetBool("Released", true);
        }
        public void OnIsMovingChanged(bool previous, bool next) { if (character.animator != null) character.animator.SetBool("isMoving", next); }
        public virtual void OnIsActiveChanged(bool previous, bool next) { if (gameObject.activeSelf != next) gameObject.SetActive(next); }
        public void ApplyDamage(CharacterManager victim, CharacterManager attacker,
            float physicalDamage, float magicDamage, float fireDamage, float holyDamage,
            float poiseDamage, float angleHitFrom, float contactPointX, float contactPointY, float contactPointZ)
        {
            if (victim == null || !victim.IsInitialized || victim.State.IsDead || !victim.State.IsActive || victim.State.IsInvulnerable || victim == attacker) return;
            if (attacker != null && WorldUtilityManager.Instance != null &&
                !WorldUtilityManager.Instance.CanIDamageThisTarget(attacker.characterGroup, victim.characterGroup)) return;
            if (victim.State.IsBlocking)
            {
                var blocked = Instantiate(WorldCharacterEffectsManager.instance.TakeBlockedDamageEffect);
                blocked.characterCausingDamage = attacker;
                blocked.physicalDamage = physicalDamage; blocked.magicDamage = magicDamage;
                blocked.fireDamage = fireDamage; blocked.holyDamage = holyDamage; blocked.poiseDamage = poiseDamage;
                victim.characterEffectsManager.ProcessOwnedInstantEffect(blocked);
                return;
            }
            var effect = Instantiate(WorldCharacterEffectsManager.instance.takeDamageEffect);
            effect.characterCausingDamage = attacker;
            effect.physicalDamage = physicalDamage; effect.magicDamage = magicDamage;
            effect.fireDamage = fireDamage; effect.holyDamage = holyDamage; effect.poiseDamage = poiseDamage;
            effect.angleHitFrom = angleHitFrom;
            effect.contactPoint = new Vector3(contactPointX, contactPointY, contactPointZ);
            victim.characterEffectsManager.ProcessOwnedInstantEffect(effect);
        }
        public void DestroyAllCurrentActionFX()
        {
            if (character.characterEffectsManager != null && character.characterEffectsManager.activeSpellWarmUpFX != null)
                Destroy(character.characterEffectsManager.activeSpellWarmUpFX);
        }
    }
}
