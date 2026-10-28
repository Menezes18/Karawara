using System.Collections;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
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

            //  IF WE DO NOT OWN THIS GAMEOBJECT, WE DO NOT CONTROL OR EDIT IT
            if (!IsOwner)
                return;

            //  HANDLE MOVEMENT
            playerLocomotionManager.HandleAllMovement();
            if (Keyboard.current.rKey.wasPressedThisFrame){
                var save = FindObjectOfType<SavePositionOnTrigger>();
                save.Reviver();
            }
            //  REGEN STAMINA
            //playerStatsManager.RegenerateStamina();
        }

        protected override void LateUpdate()
        {
            if (!IsOwner)
                return;

            base.LateUpdate();

            PlayerCamera.instance.HandleAllCameraActions();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            SceneManager.sceneLoaded += OnAudioSceneLoaded;
            RefreshAudioListener();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;

            //  IF THIS IS THE PLAYER OBJECT OWNED BY THIS CLIENT
            if (IsOwner)
            {
                PlayerCamera.instance.player = this;
                PlayerInputManager.instance.player = this;
                WorldSaveGameManager.instance.player = this;

                //  UPDATE THE TOTAL AMOUNT OF HEALTH OR STAMINA WHEN THE STAT LINKED TO EITHER CHANGES
                playerNetworkManager.vitality.OnValueChanged += playerNetworkManager.SetNewMaxHealthValue;
               // playerNetworkManager.endurance.OnValueChanged += playerNetworkManager.SetNewMaxStaminaValue;

                //  UPDATES UI STAT BARS WHEN A STAT CHANGES (HEALTH OR STAMINA)
                playerNetworkManager.currentHealth.OnValueChanged += PlayerUIManager.instance.playerUIHudManager.SetNewHealthValue;
               // playerNetworkManager.currentStamina.OnValueChanged += PlayerUIManager.instance.playerUIHudManager.SetNewStaminaValue;
               // playerNetworkManager.currentStamina.OnValueChanged += playerStatsManager.ResetStaminaRegenTimer;
            }

            //  ONLY UPDATE FLOATING HP BAR IF THIS CHARACTER IS NOT THE LOCAL PLAYERS CHARACTER (YOU DONT WANNA SEE A HP BAR FLOATING ABOVE YOUR OWN HEAD)
            if (!IsOwner)
                characterNetworkManager.currentHealth.OnValueChanged += characterUIManager.OnHPChanged;

            //  STATS
            playerNetworkManager.currentHealth.OnValueChanged += playerNetworkManager.CheckHP;

            //  LOCK ON
            playerNetworkManager.isLockedOn.OnValueChanged += playerNetworkManager.OnIsLockedOnChanged;
            playerNetworkManager.currentTargetNetworkObjectID.OnValueChanged += playerNetworkManager.OnLockOnTargetIDChange;
            //  SPELLS
            playerNetworkManager.isChargingLeftSpell.OnValueChanged += playerNetworkManager.OnIsChargingLeftSpellChanged;
            playerNetworkManager.isChargingRightSpell.OnValueChanged += playerNetworkManager.OnIsChargingRightSpellChanged;
            //  EQUIPMENT
            playerNetworkManager.currentRightHandWeaponID.OnValueChanged += playerNetworkManager.OnCurrentRightHandWeaponIDChange;
            playerNetworkManager.currentLeftHandWeaponID.OnValueChanged += playerNetworkManager.OnCurrentLeftHandWeaponIDChange;
            playerNetworkManager.currentWeaponBeingUsed.OnValueChanged += playerNetworkManager.OnCurrentWeaponBeingUsedIDChange;
            playerNetworkManager.currentSpellID.OnValueChanged += playerNetworkManager.OnCurrentSpellIDChange;
            // Publish the spell already equipped on the player prefab to observers.
            if (IsOwner && playerInventoryManager.currentSpell != null && WorldItemDatabase.Instance != null)
            {
                var equippedSpell = WorldItemDatabase.Instance.GetSpellByID(playerInventoryManager.currentSpell.itemID);
                if (equippedSpell != null && equippedSpell.GetType() == playerInventoryManager.currentSpell.GetType())
                    playerNetworkManager.currentSpellID.Value = equippedSpell.itemID;
            }
            //  FLAGS
            playerNetworkManager.isChargingAttack.OnValueChanged += playerNetworkManager.OnIsChargingAttackChanged;

            // Both persistent and recreated local owners use the pending generation.
            if (IsOwner && WorldSaveGameManager.instance != null)
            {
                WorldSaveGameManager.instance.ApplyPendingSaveWhenReady();
            }
        }

        public override void OnNetworkDespawn()
        {
            SceneManager.sceneLoaded -= OnAudioSceneLoaded;
            if (playerAudioListener != null) playerAudioListener.enabled = false;
            base.OnNetworkDespawn();

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;

            //  IF THIS IS THE PLAYER OBJECT OWNED BY THIS CLIENT
            if (IsOwner)
            {
                //  UPDATE THE TOTAL AMOUNT OF HEALTH OR STAMINA WHEN THE STAT LINKED TO EITHER CHANGES
                playerNetworkManager.vitality.OnValueChanged -= playerNetworkManager.SetNewMaxHealthValue;
               // playerNetworkManager.endurance.OnValueChanged -= playerNetworkManager.SetNewMaxStaminaValue;

                //  UPDATES UI STAT BARS WHEN A STAT CHANGES (HEALTH OR STAMINA)
                playerNetworkManager.currentHealth.OnValueChanged -= PlayerUIManager.instance.playerUIHudManager.SetNewHealthValue;
                //playerNetworkManager.currentStamina.OnValueChanged -= PlayerUIManager.instance.playerUIHudManager.SetNewStaminaValue;
               // playerNetworkManager.currentStamina.OnValueChanged -= playerStatsManager.ResetStaminaRegenTimer;
            }

            if (!IsOwner)
                characterNetworkManager.currentHealth.OnValueChanged -= characterUIManager.OnHPChanged;

            //  STATS
            playerNetworkManager.currentHealth.OnValueChanged -= playerNetworkManager.CheckHP;
            //  SPELLS
            playerNetworkManager.isChargingLeftSpell.OnValueChanged -= playerNetworkManager.OnIsChargingLeftSpellChanged;
            playerNetworkManager.isChargingRightSpell.OnValueChanged -= playerNetworkManager.OnIsChargingRightSpellChanged;
            //  LOCK ON
            playerNetworkManager.isLockedOn.OnValueChanged -= playerNetworkManager.OnIsLockedOnChanged;
            playerNetworkManager.currentTargetNetworkObjectID.OnValueChanged -= playerNetworkManager.OnLockOnTargetIDChange;

            //  EQUIPMENT
            playerNetworkManager.currentRightHandWeaponID.OnValueChanged -= playerNetworkManager.OnCurrentRightHandWeaponIDChange;
            playerNetworkManager.currentLeftHandWeaponID.OnValueChanged -= playerNetworkManager.OnCurrentLeftHandWeaponIDChange;
            playerNetworkManager.currentWeaponBeingUsed.OnValueChanged -= playerNetworkManager.OnCurrentWeaponBeingUsedIDChange;
            playerNetworkManager.currentSpellID.OnValueChanged -= playerNetworkManager.OnCurrentSpellIDChange;
            //  FLAGS
            playerNetworkManager.isChargingAttack.OnValueChanged -= playerNetworkManager.OnIsChargingAttackChanged;
        }

        private void OnAudioSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshAudioListener();
        }

        private void RefreshAudioListener()
        {
            if (playerAudioListener == null) return;
            bool usePlayerListener = IsSpawned && IsOwner;
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

        private void OnClientConnectedCallback(ulong clientID)
        {
            WorldGameSessionManager.instance.AddPlayerToActivePlayersList(this);

            //  IF WE ARE THE SERVER, WE ARE THE HOST, SO WE DONT NEED TO LOAD PLAYERS TO SYNC THEM
            //  YOU ONLY NEED TO LOAD OTHER PLAYERS GEAR TO SYNC IT IF YOU JOIN A GAME THATS ALREADY BEEN ACTIVE WITHOUT YOU BEING PRESENT
            if (!IsServer && IsOwner)
            {
                foreach (var player in WorldGameSessionManager.instance.players)
                {
                    if (player != this)
                    {
                        player.LoadOtherPlayerCharacterWhenJoiningServer();
                    }
                }
            }
        }

        public override IEnumerator ProcessDeathEvent(bool manuallySelectDeathAnimation = false)
        {
            if (IsOwner)
            {
                PlayerUIManager.instance.playerUIPopUpManager.SendYouDiedPopUp();
            }

            return base.ProcessDeathEvent(manuallySelectDeathAnimation);

            //  CHECK FOR PLAYERS THAT ARE ALIVE, IF 0 RESPAWN CHARACTERS
        }

        public override void ReviveCharacter()
        {
            base.ReviveCharacter();

            if (IsOwner)
            {
                isDead.Value = false;
                playerNetworkManager.currentHealth.Value = playerNetworkManager.maxHealth.Value;
                //playerNetworkManager.currentStamina.Value = playerNetworkManager.maxStamina.Value;
                //  RESTORE FOCUS POINTS

                //  PLAY REBIRTH EFFECTS
                playerAnimatorManager.PlayTargetActionAnimation("Empty", false);
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
            if (!IsSpawned || !IsOwner || data == null || playerNetworkManager == null || playerStatsManager == null ||
                hud == null || !hud.IsHealthBarReady || skillUi == null || skills == null || skills.slot == null || characterController == null)
                return false;
            var position = new Vector3(data.xPosition, data.yPosition, data.zPosition);
            if (!IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z)) return false;
            Unity.Collections.FixedString64Bytes name;
            try { name = new Unity.Collections.FixedString64Bytes(data.characterName); }
            catch (System.ArgumentException) { return false; }
            playerNetworkManager.characterName.Value = name;
            playerNetworkManager.vitality.Value = data.vitality;
            playerNetworkManager.endurance.Value = data.endurance;
            playerNetworkManager.maxHealth.Value = playerStatsManager.CalculateHealthBasedOnVitalityLevel(data.vitality);
            playerNetworkManager.currentHealth.Value = data.currentHealth;
            bool controllerEnabled = characterController.enabled;
            try
            {
                characterController.enabled = false;
                transform.position = position;
                playerNetworkManager.networkPosition.Value = position;
            }
            finally { characterController.enabled = controllerEnabled; }
            skillUi.LoadSkillsFromData(data);
            skills.LoadSkillSlot(data);
            // Rebind the current scene HUD even when unchanged NetworkVariables emit no callback.
            hud.SetMaxHealthValue(playerNetworkManager.maxHealth.Value);
            hud.SetNewHealthValue(0, playerNetworkManager.currentHealth.Value);
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public void LoadOtherPlayerCharacterWhenJoiningServer()
        {
            //  SYNC WEAPONS
            playerNetworkManager.OnCurrentRightHandWeaponIDChange(0, playerNetworkManager.currentRightHandWeaponID.Value);
            playerNetworkManager.OnCurrentLeftHandWeaponIDChange(0, playerNetworkManager.currentLeftHandWeaponID.Value);

            //  ARMOR
            playerNetworkManager.OnCurrentSpellIDChange(0,playerNetworkManager.currentSpellID.Value);
            //  LOCK ON
            if (playerNetworkManager.isLockedOn.Value)
            {
                playerNetworkManager.OnLockOnTargetIDChange(0, playerNetworkManager.currentTargetNetworkObjectID.Value);
            }
        }

    }
}
