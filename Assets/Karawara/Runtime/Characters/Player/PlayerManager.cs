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
        public PlayerContext Context { get; private set; }
        private Karawara.Core.Input.InputGate contextGate;
        public void InitializeLocal(CharacterRuntimeState state, ICharacterRegistry registry, Karawara.Core.Input.InputGate gate)
        {
            contextGate = gate; InitializeLocal(state, registry);
        }

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
            if (Context?.Input != null && Context.Input.Router.RevivePressed) Context.Respawn?.Reviver();
            //  REGEN STAMINA
            //playerStatsManager.RegenerateStamina();
        }

        protected override void LateUpdate()
        {
            if (!IsInitialized)
                return;

            base.LateUpdate();

            Context?.Camera?.HandleAllCameraActions();
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
            if (Context == null) Context = new PlayerContext(this);
            Context.Initialize(State, registry, contextGate ?? (contextGate = new Karawara.Core.Input.InputGate()));
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Session.ResumePlayerBinding(this);
        }
        public void RefreshLocalHUD() => Context?.RefreshHUD();

        public override void ShutdownLocal()
        {
            if (!IsInitialized) return;
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Session.SuspendPlayerBinding(this);
            playerCombatManager?.ShutdownActions();
            var input = Context?.Input;
            Context?.Dispose();
            if (input != null && input.player == this) input.BindSessionPlayer(null);
            base.ShutdownLocal();
        }

        public override IEnumerator ProcessDeathEvent(bool manuallySelectDeathAnimation = false)
        {

            {
                Context?.UI?.playerUIPopUpManager?.SendYouDiedPopUp();
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
            var skills = Context?.Skills;
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

        private SkillTreeUiManager FindOwnedSkillUi() => Context?.SkillUI;

        public bool ApplyLegacySave(CharacterSaveData data)
        {
            var hud = Context?.UI?.playerUIHudManager;
            var skillUi = FindOwnedSkillUi();
            var skills = Context?.Skills;
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
