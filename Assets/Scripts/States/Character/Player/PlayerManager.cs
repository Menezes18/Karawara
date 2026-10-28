using System.Collections;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.SceneManagement;
using Karawara.Core.Characters;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    public class PlayerManager : CharacterManager{

        [HideInInspector] public PlayerAnimatorManager playerAnimatorManager;
        [HideInInspector] public PlayerLocomotionManager playerLocomotionManager;
        [HideInInspector] public PlayerNetworkManager playerNetworkManager;
        [HideInInspector] public PlayerStatsManager playerStatsManager;
        [HideInInspector] public PlayerInventoryManager playerInventoryManager;
        [HideInInspector] public PlayerEquipmentManager playerEquipmentManager;
        [HideInInspector] public PlayerCombatManager playerCombatManager;
        [HideInInspector] public PlayerInteractionManager playerInteractionManager;
        [HideInInspector] public PlayerEffectsManager playerEffectsManager;
        private AudioListener playerAudioListener;

        protected override void Awake()
        {
            base.Awake();
            playerAudioListener = GetComponent<AudioListener>();
            if (playerAudioListener != null) playerAudioListener.enabled = false;

            //  DO MORE STUFF, ONLY FOR THE PLAYER

            playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            playerAnimatorManager = GetComponent<PlayerAnimatorManager>();
            playerNetworkManager = GetComponent<PlayerNetworkManager>();
            playerStatsManager = GetComponent<PlayerStatsManager>();
            playerInventoryManager = GetComponent<PlayerInventoryManager>();
            playerEquipmentManager = GetComponent<PlayerEquipmentManager>();
            playerCombatManager = GetComponent<PlayerCombatManager>();
            playerInteractionManager = GetComponent<PlayerInteractionManager>();
            playerEffectsManager = GetComponent<PlayerEffectsManager>();
        }

        protected override void Update()
        {
            base.Update();

            // Gameplay starts after the session binds this local character.
            if (!IsInitialized)
                return;

            //  HANDLE MOVEMENT
            playerLocomotionManager.HandleAllMovement();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame){
                var save = FindObjectOfType<SavePositionOnTrigger>();
                if (save != null) save.Reviver();
            }
            //  REGEN STAMINA
            //playerStatsManager.RegenerateStamina();
        }

        protected override void LateUpdate()
        {
            if (!IsInitialized)
                return;

            base.LateUpdate();

            if (PlayerCamera.instance != null) PlayerCamera.instance.HandleAllCameraActions();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        public override void InitializeLocal(CharacterRuntimeState state, ICharacterRegistry registry)
        {
            if (IsInitialized && ReferenceEquals(State, state) && ReferenceEquals(Registry, registry)) return;
            base.InitializeLocal(state, registry);
            SceneManager.sceneLoaded += OnAudioSceneLoaded;
            RefreshAudioListener();
            if (PlayerCamera.instance != null) PlayerCamera.instance.player = this;
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.player = this;
            playerNetworkManager.vitality.OnValueChanged += playerNetworkManager.SetNewMaxHealthValue;
            State.HealthChanged += OnPlayerHealthChanged;
            State.MaxHealthChanged += OnPlayerMaxHealthChanged;
            playerNetworkManager.isChargingLeftSpell.OnValueChanged += playerNetworkManager.OnIsChargingLeftSpellChanged;
            playerNetworkManager.isChargingRightSpell.OnValueChanged += playerNetworkManager.OnIsChargingRightSpellChanged;
            playerNetworkManager.currentRightHandWeaponID.OnValueChanged += playerNetworkManager.OnCurrentRightHandWeaponIDChange;
            playerNetworkManager.currentLeftHandWeaponID.OnValueChanged += playerNetworkManager.OnCurrentLeftHandWeaponIDChange;
            playerNetworkManager.currentWeaponBeingUsed.OnValueChanged += playerNetworkManager.OnCurrentWeaponBeingUsedIDChange;
            playerNetworkManager.currentSpellID.OnValueChanged += playerNetworkManager.OnCurrentSpellIDChange;
            if (playerInventoryManager != null && playerInventoryManager.currentSpell != null && WorldItemDatabase.Instance != null)
            {
                var equippedSpell = WorldItemDatabase.Instance.GetSpellByID(playerInventoryManager.currentSpell.itemID);
                if (equippedSpell != null && equippedSpell.GetType() == playerInventoryManager.currentSpell.GetType())
                    playerNetworkManager.currentSpellID.Value = equippedSpell.itemID;
            }
            playerNetworkManager.OnIsChargingLeftSpellChanged(false, State.IsChargingLeftSpell);
            playerNetworkManager.OnIsChargingRightSpellChanged(false, State.IsChargingRightSpell);
            RefreshLocalHUD();
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Session.ResumePlayerBinding(this);
        }
        private void OnPlayerHealthChanged(int oldValue, int newValue)
        {
            if (PlayerUIManager.instance != null) PlayerUIManager.instance.playerUIHudManager?.SetNewHealthValue(oldValue, newValue);
        }
        private void OnPlayerMaxHealthChanged(int oldValue, int newValue)
        {
            if (PlayerUIManager.instance == null) return;
            var hud = PlayerUIManager.instance.playerUIHudManager;
            if (hud == null) return;
            hud.SetMaxHealthValue(newValue);
            hud.SetNewHealthValue(State.Health, State.Health);
        }
        public void RefreshLocalHUD()
        {
            OnPlayerMaxHealthChanged(State.MaxHealth, State.MaxHealth);
            OnPlayerHealthChanged(State.Health, State.Health);
        }

        public override void ShutdownLocal()
        {
            if (!IsInitialized) return;
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Session.SuspendPlayerBinding(this);
            SceneManager.sceneLoaded -= OnAudioSceneLoaded;
            playerCombatManager?.ShutdownActions();
            if (playerAudioListener != null) playerAudioListener.enabled = false;
            playerNetworkManager.vitality.OnValueChanged -= playerNetworkManager.SetNewMaxHealthValue;
            State.HealthChanged -= OnPlayerHealthChanged;
            State.MaxHealthChanged -= OnPlayerMaxHealthChanged;
            playerNetworkManager.isChargingLeftSpell.Value = false;
            playerNetworkManager.isChargingRightSpell.Value = false;
            playerNetworkManager.isChargingLeftSpell.OnValueChanged -= playerNetworkManager.OnIsChargingLeftSpellChanged;
            playerNetworkManager.isChargingRightSpell.OnValueChanged -= playerNetworkManager.OnIsChargingRightSpellChanged;
            playerNetworkManager.currentRightHandWeaponID.OnValueChanged -= playerNetworkManager.OnCurrentRightHandWeaponIDChange;
            playerNetworkManager.currentLeftHandWeaponID.OnValueChanged -= playerNetworkManager.OnCurrentLeftHandWeaponIDChange;
            playerNetworkManager.currentWeaponBeingUsed.OnValueChanged -= playerNetworkManager.OnCurrentWeaponBeingUsedIDChange;
            playerNetworkManager.currentSpellID.OnValueChanged -= playerNetworkManager.OnCurrentSpellIDChange;
            playerNetworkManager.isChargingLeftSpell.Value = false;
            playerNetworkManager.isChargingRightSpell.Value = false;
            if (PlayerCamera.instance != null && PlayerCamera.instance.player == this) PlayerCamera.instance.player = null;
            if (PlayerInputManager.instance != null && PlayerInputManager.instance.player == this) PlayerInputManager.instance.BindSessionPlayer(null);
            if (WorldSaveGameManager.instance != null && WorldSaveGameManager.instance.player == this) WorldSaveGameManager.instance.player = null;
            base.ShutdownLocal();
        }

        private void OnAudioSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshAudioListener();
            RefreshLocalHUD();
        }

        private void RefreshAudioListener()
        {
            if (playerAudioListener == null) return;
            bool usePlayerListener = IsInitialized;
            if (usePlayerListener)
            {
                var activeScene = SceneManager.GetActiveScene();
                foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    // A scene camera supplies audio during its authored presentation.
                    if (listener != playerAudioListener && listener.isActiveAndEnabled &&
                        listener.gameObject.scene == activeScene && listener.GetComponentInParent<PlayerManager>() == null)
                    {
                        usePlayerListener = false;
                        break;
                    }
                }
            }
            playerAudioListener.enabled = usePlayerListener;
        }



        public override IEnumerator ProcessDeathEvent(bool manuallySelectDeathAnimation = false)
        {

            {
                if (PlayerUIManager.instance != null) PlayerUIManager.instance.playerUIPopUpManager?.SendYouDiedPopUp();
            }

            return base.ProcessDeathEvent(manuallySelectDeathAnimation);

            //  CHECK FOR PLAYERS THAT ARE ALIVE, IF 0 RESPAWN CHARACTERS
        }

        public override void ReviveCharacter()
        {
            base.ReviveCharacter();


            {
                isDead.Value = false;
                playerNetworkManager.currentHealth.Value = playerNetworkManager.maxHealth.Value;
                //playerNetworkManager.currentStamina.Value = playerNetworkManager.maxStamina.Value;
                //  RESTORE FOCUS POINTS

                //  PLAY REBIRTH EFFECTS
                if (playerAnimatorManager != null && animator != null) playerAnimatorManager.PlayTargetActionAnimation("Empty", false);
            }
        }

        public void SaveGameDataToCurrentCharacterData(ref CharacterSaveData currentCharacterData)
        {
            currentCharacterData.sceneIndex = SceneManager.GetActiveScene().buildIndex;

            currentCharacterData.characterName = playerNetworkManager.characterName.Value.ToString();
            currentCharacterData.xPosition = transform.position.x;
            currentCharacterData.yPosition = transform.position.y;
            currentCharacterData.zPosition = transform.position.z;

            currentCharacterData.currentHealth = playerNetworkManager.currentHealth.Value;
            //currentCharacterData.currentStamina = playerNetworkManager.currentStamina.Value;

            currentCharacterData.vitality = playerNetworkManager.vitality.Value;
            currentCharacterData.endurance = playerNetworkManager.endurance.Value;
            var skillUi = FindOwnedSkillUi();
            var skills = GetComponent<PlayerSkillManager>();
            if (skillUi == null || skills == null) throw new System.InvalidOperationException("Player skills are not ready for capture.");
            skillUi.SaveSkillsToData(currentCharacterData);
            skills.SaveSkillSlot(currentCharacterData);

        }

        public void LoadGameDataFromCurrentCharacterData(ref CharacterSaveData currentCharacterData)
        {
            // Preserve the old binding while routing it through generation/readiness checks.
            if (WorldSaveGameManager.instance != null)
                WorldSaveGameManager.instance.ApplyPendingSaveWhenReady();
        }

        private SkillTreeUiManager FindOwnedSkillUi()
        {
            // The authored skill panel can be inactive and therefore not have run Awake yet.
            var ui = PlayerUIManager.instance != null
                ? PlayerUIManager.instance.GetComponentInChildren<SkillTreeUiManager>(true) : null;
            if (ui != null) return ui;
            return SkillTreeUiManager.instance != null ? SkillTreeUiManager.instance : FindAnyObjectByType<SkillTreeUiManager>(FindObjectsInactive.Include);
        }

        public bool ApplyLegacySave(CharacterSaveData data)
        {
            var hud = PlayerUIManager.instance != null ? PlayerUIManager.instance.playerUIHudManager : null;
            var skillUi = FindOwnedSkillUi();
            var skills = GetComponent<PlayerSkillManager>();
            if (!IsInitialized || data == null || playerNetworkManager == null || playerStatsManager == null ||
                hud == null || !hud.IsHealthBarReady || skillUi == null || skills == null || skills.slot == null || characterController == null)
                return false;
            var position = new Vector3(data.xPosition, data.yPosition, data.zPosition);
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)) return false;
            playerNetworkManager.characterName.Value = data.characterName ?? "Character";
            playerNetworkManager.vitality.Value = data.vitality;
            playerNetworkManager.endurance.Value = data.endurance;
            playerNetworkManager.maxHealth.Value = playerStatsManager.CalculateHealthBasedOnVitalityLevel(data.vitality);
            playerNetworkManager.currentHealth.Value = data.currentHealth;
            bool controllerEnabled = characterController.enabled;
            try
            {
                characterController.enabled = false;
                transform.position = position;
            }
            finally { characterController.enabled = controllerEnabled; }
            skillUi.LoadSkillsFromData(data);
            skills.LoadSkillSlot(data);
            // Refresh the HUD even when applying an unchanged value.
            hud.SetMaxHealthValue(playerNetworkManager.maxHealth.Value);
            hud.SetNewHealthValue(0, playerNetworkManager.currentHealth.Value);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);



    }
}
