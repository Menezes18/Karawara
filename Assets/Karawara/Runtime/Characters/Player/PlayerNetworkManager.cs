using UnityEngine;
using Karawara.Core.Characters;



namespace RPGKarawara
{
    public class PlayerNetworkManager : CharacterNetworkManager
    {
        PlayerManager player;

        public ObservableValue<string> characterName = new ObservableValue<string>("Character");

        [Header("Equipment")]
        public ObservableValue<int> currentSpellID = new ObservableValue<int>(0);
        public ObservableValue<int> currentWeaponBeingUsed = new ObservableValue<int>(0);
        public ObservableValue<int> currentRightHandWeaponID = new ObservableValue<int>(0);
        public ObservableValue<int> currentLeftHandWeaponID = new ObservableValue<int>(0);
        public ObservableValue<bool> isUsingRightHand;
        public ObservableValue<bool> isUsingLeftHand;
        [Header("Spells")]
        public ObservableValue<bool> isChargingRightSpell;
        public ObservableValue<bool> isChargingLeftSpell;
        protected override void Awake()
        {
            base.Awake();

            isUsingRightHand = new ObservableValue<bool>(() => character.State.IsUsingRightHand, value => character.State.SetIsUsingRightHand(value), true);
            isUsingLeftHand = new ObservableValue<bool>(() => character.State.IsUsingLeftHand, value => character.State.SetIsUsingLeftHand(value), true);
            isChargingRightSpell = new ObservableValue<bool>(() => character.State.IsChargingRightSpell, value => character.State.SetIsChargingRightSpell(value), true);
            isChargingLeftSpell = new ObservableValue<bool>(() => character.State.IsChargingLeftSpell, value => character.State.SetIsChargingLeftSpell(value), true);
            player = GetComponent<PlayerManager>();
        }

        public override void BindState(CharacterRuntimeState state)
        {
            base.BindState(state);
            state.IsUsingRightHandChanged += isUsingRightHand.Publish;
            state.IsUsingLeftHandChanged += isUsingLeftHand.Publish;
            state.IsChargingRightSpellChanged += isChargingRightSpell.Publish;
            state.IsChargingLeftSpellChanged += isChargingLeftSpell.Publish;
        }
        public override void UnbindState(CharacterRuntimeState state)
        {
            base.UnbindState(state);
            state.IsUsingRightHandChanged -= isUsingRightHand.Publish;
            state.IsUsingLeftHandChanged -= isUsingLeftHand.Publish;
            state.IsChargingRightSpellChanged -= isChargingRightSpell.Publish;
            state.IsChargingLeftSpellChanged -= isChargingLeftSpell.Publish;
        }
        public void SetCharacterActionHand(bool rightHandedAction)
        {
            if (rightHandedAction)
            {
                isUsingLeftHand.Value = false;
                isUsingRightHand.Value = true;//
            }
            else
            {
                isUsingRightHand.Value = false;
                isUsingLeftHand.Value = true;
            }
        }

        public void SetNewMaxHealthValue(int oldVitality, int newVitality)
        {
            maxHealth.Value = player.playerStatsManager.CalculateHealthBasedOnVitalityLevel(newVitality);
            currentHealth.Value = maxHealth.Value;
        }

        // public void SetNewMaxStaminaValue(int oldEndurance, int newEndurance)
        // {
        //     maxStamina.Value = player.playerStatsManager.CalculateStaminaBasedOnEnduranceLevel(newEndurance);
        //     PlayerUIManager.instance.playerUIHudManager.SetMaxStaminaValue(maxStamina.Value);
        //     currentStamina.Value = maxStamina.Value;
        // }

        public void OnCurrentRightHandWeaponIDChange(int oldID, int newID)
        {
            var template = player.Context?.Catalog != null ? player.Context.Catalog.GetWeaponByID(newID) : null;
            if (template == null) return;
            // Item definitions are immutable; runtime models and action state belong to the player.
            WeaponItem newWeapon = template;
            player.playerInventoryManager.currentRightHandWeapon = newWeapon;
            player.playerEquipmentManager.LoadRightWeapon();
            {
                player.Context?.UI?.playerUIHudManager?.SetRightWeaponQuickSlotIcon(newID);
            }
        }
        public void OnCurrentSpellIDChange(int oldID, int newID)
        {
            var template = player.Context?.Catalog != null ? player.Context.Catalog.GetSpellByID(newID) : null;
            player.playerInventoryManager.currentSpell = template;
        }
        public void OnCurrentLeftHandWeaponIDChange(int oldID, int newID)
        {
            var template = player.Context?.Catalog != null ? player.Context.Catalog.GetWeaponByID(newID) : null;
            if (template == null) return;
            // Item definitions are immutable; runtime models and action state belong to the player.
            WeaponItem newWeapon = template;
            player.playerInventoryManager.currentLeftHandWeapon = newWeapon;
            player.playerEquipmentManager.LoadLeftWeapon();
            {
                player.Context?.UI?.playerUIHudManager?.SetLeftWeaponQuickSlotIcon(newID);
            }
        }

        public void OnCurrentWeaponBeingUsedIDChange(int oldID, int newID)
        {
            var template = player.Context?.Catalog != null ? player.Context.Catalog.GetWeaponByID(newID) : null;
            if (template == null) return;
            // Item definitions are immutable; runtime models and action state belong to the player.
            WeaponItem newWeapon = template;
            player.playerCombatManager.currentWeaponBeingUsed = newWeapon;
        }

        public void OnIsChargingRightSpellChanged(bool oldStatus, bool newStatus)
        {
            if (player.animator != null) player.animator.SetBool("isChargingRightSpell", newStatus);
        }
        public void OnIsChargingLeftSpellChanged(bool oldStatus, bool newStatus)
        {
            if (player.animator != null) player.animator.SetBool("isChargingLeftSpell", isChargingLeftSpell.Value);
        }
    }
}
