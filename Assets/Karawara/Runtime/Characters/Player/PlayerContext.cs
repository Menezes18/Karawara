using System;
using Karawara.Core.Characters;
using Karawara.Core.Input;
using RPGKarawara.SkillTree;
using UnityEngine;

namespace RPGKarawara
{
    /// <summary>One player's cached components and explicit bindings, with symmetric teardown.</summary>
    public sealed class PlayerContext : IDisposable
    {
        private readonly PlayerManager player;
        private readonly PlayerNetworkManager values;
        private readonly AudioListener listener;
        private SkillCooldownUI cooldownUI;
        public CharacterRuntimeState State { get; private set; }
        public ICharacterRegistry Registry { get; private set; }
        public InputGate Gate { get; private set; }
        public PlayerInputManager Input { get; private set; }
        public PlayerCamera Camera { get; private set; }
        public PlayerUIManager UI { get; private set; }
        public WorldItemDatabase Catalog { get; private set; }
        public SkillTreeUiManager SkillUI { get; private set; }
        public PlayerSkillManager Skills { get; }
        public SavePositionOnTrigger Respawn { get; private set; }
        public bool IsDisposed { get; private set; } = true;
        public PlayerContext(PlayerManager owner)
        {
            player = owner ?? throw new ArgumentNullException(nameof(owner));
            values = owner.playerNetworkManager; listener = owner.GetComponent<AudioListener>();
            Skills = owner.GetComponent<PlayerSkillManager>();
        }
        public void Initialize(CharacterRuntimeState state, ICharacterRegistry registry, InputGate inputGate)
        {
            Dispose();
            State = state ?? throw new ArgumentNullException(nameof(state)); Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Gate = inputGate ?? throw new ArgumentNullException(nameof(inputGate)); IsDisposed = false;
            if (values == null) return;
            values.vitality.OnValueChanged += values.SetNewMaxHealthValue;
            values.isChargingLeftSpell.OnValueChanged += values.OnIsChargingLeftSpellChanged;
            values.isChargingRightSpell.OnValueChanged += values.OnIsChargingRightSpellChanged;
            values.currentRightHandWeaponID.OnValueChanged += values.OnCurrentRightHandWeaponIDChange;
            values.currentLeftHandWeaponID.OnValueChanged += values.OnCurrentLeftHandWeaponIDChange;
            values.currentWeaponBeingUsed.OnValueChanged += values.OnCurrentWeaponBeingUsedIDChange;
            values.currentSpellID.OnValueChanged += values.OnCurrentSpellIDChange;
            values.OnIsChargingLeftSpellChanged(false, State.IsChargingLeftSpell); values.OnIsChargingRightSpellChanged(false, State.IsChargingRightSpell);
        }
        public void BindPresentation(PlayerUIManager ui, PlayerInputManager input, PlayerCamera camera, SavePositionOnTrigger respawn, WorldItemDatabase catalog = null, bool sceneProvidesAudio = false)
        {
            UnbindPresentation();
            if (IsDisposed) return;
            UI = ui; Input = input; Camera = camera; Respawn = respawn; Catalog = catalog; Gate = input != null ? input.Gate : Gate;
            if (Camera != null) Camera.player = player;
            if (UI != null)
            {
                UI.BindPlayer(State, input, catalog);
                if (values != null && catalog != null)
                {
                    UI.playerUIHudManager?.SetRightWeaponQuickSlotIcon(values.currentRightHandWeaponID.Value);
                    UI.playerUIHudManager?.SetLeftWeaponQuickSlotIcon(values.currentLeftHandWeaponID.Value);
                }
                SkillUI = UI.GetComponentInChildren<SkillTreeUiManager>(true); SkillUI?.Bind(Skills);
                cooldownUI = UI.GetComponentInChildren<SkillCooldownUI>(true); cooldownUI?.Bind(Skills);
            }
            if (values != null && Catalog != null && player.playerInventoryManager != null && player.playerInventoryManager.currentSpell != null)
            {
                var spell = Catalog.GetSpellByID(player.playerInventoryManager.currentSpell.itemID);
                if (spell != null && spell.GetType() == player.playerInventoryManager.currentSpell.GetType()) values.currentSpellID.Value = spell.itemID;
            }
            if (listener != null) listener.enabled = player.IsInitialized && !sceneProvidesAudio;
        }
        public void UnbindPresentation()
        {
            UI?.UnbindPlayer(State); SkillUI?.Bind(null); cooldownUI?.Bind(null);
            if (Camera != null && Camera.player == player) Camera.player = null;
            UI = null; Input = null; Camera = null; Respawn = null; Catalog = null; SkillUI = null; cooldownUI = null;
        }
        public void RefreshHUD() { if (UI != null) UI.playerUIHudManager?.RefreshState(); }
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (values != null)
            {
                values.isChargingLeftSpell.Value = false; values.isChargingRightSpell.Value = false;
                values.vitality.OnValueChanged -= values.SetNewMaxHealthValue;
                values.isChargingLeftSpell.OnValueChanged -= values.OnIsChargingLeftSpellChanged;
                values.isChargingRightSpell.OnValueChanged -= values.OnIsChargingRightSpellChanged;
                values.currentRightHandWeaponID.OnValueChanged -= values.OnCurrentRightHandWeaponIDChange;
                values.currentLeftHandWeaponID.OnValueChanged -= values.OnCurrentLeftHandWeaponIDChange;
                values.currentWeaponBeingUsed.OnValueChanged -= values.OnCurrentWeaponBeingUsedIDChange;
                values.currentSpellID.OnValueChanged -= values.OnCurrentSpellIDChange;
            }
            UnbindPresentation(); if (listener != null) listener.enabled = false;
            State = null; Registry = null; Gate = null;
        }
    }
}
