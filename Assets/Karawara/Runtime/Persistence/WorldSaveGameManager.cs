using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Karawara.Core.Persistence;


namespace RPGKarawara
{
    public class WorldSaveGameManager : MonoBehaviour
    {
        public static WorldSaveGameManager instance;
        [field: System.NonSerialized] internal string SessionSaveRootOverride { get; set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        // The dedicated playground uses in-memory character data, including boss rewards.
        public static bool DevPlaygroundIsolationActive { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDevelopmentIsolation()
        {
            DevPlaygroundIsolationActive = false;
        }
#endif

        #if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        [field: System.NonSerialized] public string SaveRootOverride { get; set; }
        // Actual creation provenance for explicit QA census; never used to drive gameplay.
        public GameObject QaLoadedWorldObject => worldInitializer != null ? worldInitializer.QaLoadedWorldObject : null;
        public AssetData QaLoadedWorldAsset => worldInitializer != null ? worldInitializer.QaLoadedWorldAsset : null;
        public GameObject QaLoadedWorldPrefab => worldInitializer != null ? worldInitializer.QaLoadedWorldPrefab : null;
        public Texture2D QaLoadedWorldTexture => worldInitializer != null ? worldInitializer.QaLoadedWorldTexture : null;
        public string QaLoadedWorldTexturePath => worldInitializer != null ? worldInitializer.QaLoadedWorldTexturePath : null;
        public Vector3 QaLoadedWorldPosition => worldInitializer != null ? worldInitializer.QaLoadedWorldPosition : Vector3.zero;
        public bool QaWorldTextureApplied => worldInitializer != null ? worldInitializer.QaWorldTextureApplied : false;
        public AssetData QaCurrentWorldAsset => assetData;
        private void OnEnable() => DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.Enabled);
        private void OnDisable() => DevTools.Performance.PerformanceActorLifecycleScope.Notify(this, DevTools.Performance.PerformanceActorLifecycle.Disabled);
#endif
        public string SaveRoot
        {
            get
            {
                var root = ResolveSaveRoot();
                if (!root.Success) SaveStatus = "Save " + root.Error + ": " + root.Message;
                return root.Success ? root.Value : null;
            }
        }

        private SaveResult<string> ResolveSaveRoot()
        {
            // Explicit QA sessions remain isolated even in an ordinary Release build.
            if (!string.IsNullOrEmpty(SessionSaveRootOverride))
                return SaveRootPolicy.Resolve(Application.persistentDataPath,
                    new[] { "-karawaraSaveRoot", SessionSaveRootOverride }, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            string[] args = System.Environment.GetCommandLineArgs();
            if (SaveRootOverride != null)
                args = new[] { "-karawaraSaveRoot", SaveRootOverride };
#if KARAWARA_QA
            if (System.Array.IndexOf(args, "-karawaraSaveRoot") < 0)
                return SaveResult<string>.Fail(SaveError.InvalidData, "Dedicated QA requires an explicit absolute -karawaraSaveRoot distinct from personal saves.");
#endif
            return SaveRootPolicy.Resolve(Application.persistentDataPath, args, true);
#else
            return SaveResult<string>.Ok(Application.persistentDataPath);
#endif
        }

        protected virtual void RequestWorldSceneLoad() => StartCoroutine(LoadWorldScene());

        public PlayerManager player;
        public bool CanCreateNewGame => !sceneLoading && (GameBootstrap.Instance == null || !GameBootstrap.Instance.Session.IsBusy);
        private long loadGeneration;
        private bool sceneLoading;
        private void OnDestroy() { if (instance == this) instance = null; }
        private readonly Dictionary<PlayerManager, long> appliedPlayers = new Dictionary<PlayerManager, long>();

        [Header("SAVE/LOAD")]
        [SerializeField] bool saveGame;
        [SerializeField] bool loadGame;

        [Header("World Scene Index")]
        [SerializeField] int worldSceneIndex = 1;
        [SerializeField] int newGameSceneIndex = 1;


        [Header("Current Character Data")]
        public CharacterSlot currentCharacterSlotBeingUsed;
        public CharacterSaveData currentCharacterData;
        public string SaveStatus { get; private set; }
        private SaveResult<CharacterSaveData> slot01Result;
        private SaveResult<CharacterSaveData> slot02Result;
        private SaveResult<CharacterSaveData> slot03Result;

        [Header("Character Slots")]
        public CharacterSaveData characterSlot01;
        public CharacterSaveData characterSlot02;
        public CharacterSaveData characterSlot03;
        
        public LoadScene loadscene;
        [Header("Texture Async")] 
        [SerializeField] private AssetData assetData;
        private void Awake()
        {
            //  THERE CAN ONLY BE ONE INSTANCE OF THIS SCRIPT AT ONE TIME, IF ANOTHER EXISTS, DESTROY IT
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            RefreshCharacterProfiles();
        }

        private void Update()
        {
            if (saveGame)
            {
                saveGame = false;
                SaveGame();
            }

            if (loadGame)
            {
                loadGame = false;
                LoadGame();
            }
            ApplyPendingSaveWhenReady();
        }

        private static bool IsEnabledScene(int index) => index >= 0 && index < SceneManager.sceneCountInBuildSettings &&
            !string.IsNullOrEmpty(SceneUtility.GetScenePathByBuildIndex(index));

        private void AcceptLoad(CharacterSlot slot, CharacterSaveData data)
        {
            currentCharacterSlotBeingUsed = slot;
            currentCharacterData = data;
            worldSceneIndex = data.sceneIndex;
            loadGeneration++;
            appliedPlayers.Clear();
            SaveStatus = "";
        }

        public bool ApplyPendingSaveWhenReady()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return false;
#endif
            var scene = SceneManager.GetActiveScene();
            if (loadGeneration == 0 || sceneLoading || currentCharacterData == null || !scene.isLoaded ||
                scene.buildIndex != currentCharacterData.sceneIndex || player == null || !player.IsInitialized)
                return false;
            if (appliedPlayers.TryGetValue(player, out long generation) && generation == loadGeneration) return false;
            if (!player.ApplyLegacySave(currentCharacterData)) return false;
            appliedPlayers[player] = loadGeneration;
            return true;
        }

        public string DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot characterSlot)
        {
            string fileName = "";

            switch (characterSlot)
            {
                case CharacterSlot.CharacterSlot_01:
                    fileName = "characterSlot_01";
                    break;
                case CharacterSlot.CharacterSlot_02:
                    fileName = "characterSlot_02";
                    break;
                case CharacterSlot.CharacterSlot_03:
                    fileName = "characterSlot_03";
                    break;
                default:
                    break;
            }

            return fileName;
        }
        public void AttemptToCreateNewGame()
        {
            var result = TryCreateNewGame();
            if (!result.Success) RecordSaveFailure(result.Error, result.Message);
        }

        public SaveResult<bool> TryCreateNewGame()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return SaveResult<bool>.Fail(SaveError.Busy, "Playground saves are isolated.");
#endif
            if (!CanCreateNewGame)
                return SaveResult<bool>.Fail(SaveError.Busy, "Wait for the local player and loading screen before creating a game.");
            var root = ResolveSaveRoot();
            if (!root.Success) return SaveResult<bool>.Fail(root.Error, root.Message);
            foreach (CharacterSlot slot in new[] { CharacterSlot.CharacterSlot_01, CharacterSlot.CharacterSlot_02, CharacterSlot.CharacterSlot_03 })
            {
                var profile = TryLoadSelectedSlot(slot);
                if (profile.Success || profile.Error != SaveError.Missing) continue;
                return NewGame(slot);
            }
            if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.DisplayNoFreeCharacterSlotsPopUp();
            return SaveResult<bool>.Fail(SaveError.Missing, "No empty character slot is available.");
        }
        public void OnSlotOverwriteSelected(string selectedSlot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return;
#endif

            CharacterSlot slot;
            switch (selectedSlot)
            {
                case "Save-01":
                    slot = CharacterSlot.CharacterSlot_01;
                    break;
                case "Save-02":
                    slot = CharacterSlot.CharacterSlot_02;
                    break;
                case "Save-03":
                    slot = CharacterSlot.CharacterSlot_03;
                    break;
                default:
                    Debug.LogError("Slot selecionado desconhecido.");
                    return;
            }
            
            var result = NewGame(slot);
            if (!result.Success) RecordSaveFailure(result.Error, result.Message);
        }
        public SaveResult<CharacterSaveData> PrepareNewGame(CharacterSlot slot)
        {
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsQaSession)
                return SaveResult<CharacterSaveData>.Fail(SaveError.Busy, "QA sessions cannot create personal saves.");
            var root = ResolveSaveRoot();
            if (!root.Success) return SaveResult<CharacterSaveData>.Fail(root.Error, root.Message);
            if (string.IsNullOrEmpty(DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot)) || !IsEnabledScene(newGameSceneIndex))
                return SaveResult<CharacterSaveData>.Fail(SaveError.InvalidData, "Invalid new-game slot or scene.");
            var candidate = new CharacterSaveData
            {
                sceneIndex = newGameSceneIndex, vitality = 15, endurance = 10, currentHealth = 225,
                xPosition = -319.92f, yPosition = 175.81f, zPosition = 595.46f
            };
            var write = CreateWriter(slot).TrySave(candidate);
            return write.Success ? SaveResult<CharacterSaveData>.Ok(candidate) : SaveResult<CharacterSaveData>.Fail(write.Error, write.Message);
        }

        private SaveResult<bool> NewGame(CharacterSlot slot)
        {
            if (!CanCreateNewGame) return SaveResult<bool>.Fail(SaveError.Busy, "A session command is already running.");
            var prepared = PrepareNewGame(slot);
            if (!prepared.Success) return SaveResult<bool>.Fail(prepared.Error, prepared.Message);
            RefreshCharacterProfiles();
            _ = GameBootstrap.EnsureInstance().Session.LoadGameAsync((int)slot + 1);
            return SaveResult<bool>.Ok(true);
        }

        public void AcceptSessionLoad(CharacterSlot slot, CharacterSaveData data, PlayerManager sessionPlayer)
        {
            player = sessionPlayer;
            AcceptLoad(slot, data);
        }

        public void ClearSession()
        {
            worldInitializer = null;
            player = null; currentCharacterData = null; loadGeneration = 0; appliedPlayers.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            DevPlaygroundIsolationActive = false;
#endif
        }

        // Kept for the existing SceneMenu UnityEvent. Its selection comes from the menu.
        public void SlotTeste()
        {
            // Selection is owned by TitleScreenManager. LoadGame commits active context only after validation.
        }

        public SaveResult<CharacterSaveData> TryLoadSelectedSlot(CharacterSlot slot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return SaveResult<CharacterSaveData>.Fail(SaveError.Busy, "Playground saves are isolated.");
#endif
            string name = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot);
            if (string.IsNullOrEmpty(name)) return SaveResult<CharacterSaveData>.Fail(SaveError.InvalidData, "Select a character slot first.");
            var root = ResolveSaveRoot();
            if (!root.Success) return SaveResult<CharacterSaveData>.Fail(root.Error, root.Message);
            return CreateWriter(slot).TryLoad();
        }

        public void LoadGame()
        {
            CharacterSlot selected = TitleScreenManager.Instance != null
                ? TitleScreenManager.Instance.currentSelectedSlot : currentCharacterSlotBeingUsed;
            _ = GameBootstrap.EnsureInstance().Session.LoadGameAsync((int)selected + 1);
        }

        public void SaveGame()
        {
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsQaSession) return;
            var result = TrySaveGame();
            if (!result.Success) RecordSaveFailure(result.Error, result.Message);
        }

        public SaveResult<bool> TrySaveGame()
        {
            if (GameBootstrap.Instance != null && GameBootstrap.Instance.Session.IsQaSession)
                return SaveResult<bool>.Fail(SaveError.Busy, "QA sessions cannot write personal saves.");
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return SaveResult<bool>.Fail(SaveError.Busy, "Playground saves are isolated.");
#endif
            var root = ResolveSaveRoot();
            if (!root.Success) return SaveResult<bool>.Fail(root.Error, root.Message);
            if (sceneLoading || (loadGeneration > 0 && (player == null ||
                !appliedPlayers.TryGetValue(player, out long appliedGeneration) || appliedGeneration != loadGeneration)))
                return SaveResult<bool>.Fail(SaveError.Busy, "The accepted save is waiting for its scene and player restoration.");
            if (player == null || !player.IsInitialized || currentCharacterData == null)
                return SaveResult<bool>.Fail(SaveError.Busy, "No spawned player/save context is ready.");
            var result = CaptureAndSave(currentCharacterSlotBeingUsed, ref currentCharacterData);
            if (result.Success)
            {
                SaveStatus = "";
                RefreshCharacterProfiles();
                if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.RefreshSaveSlotPresentation();
            }
            return result;
        }

        private SaveResult<bool> CaptureAndSave(CharacterSlot slot, ref CharacterSaveData data)
        {
            if (string.IsNullOrEmpty(DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot)))
                return SaveResult<bool>.Fail(SaveError.InvalidData, "Select a character slot first.");
            var capture = CaptureCharacterData(ref data);
            return capture.Success ? CreateWriter(slot).TrySave(data) : capture;
        }

        private SaveResult<bool> CaptureCharacterData(ref CharacterSaveData data)
        {
            try { player.SaveGameDataToCurrentCharacterData(ref data); }
            catch (System.Exception ex) { return SaveResult<bool>.Fail(SaveError.InvalidData, "Character capture failed: " + ex.Message); }
            return SaveResult<bool>.Ok(true);
        }

        private SaveFileDataWriter CreateWriter(CharacterSlot slot) => new SaveFileDataWriter
        {
            saveDataDirectoryPath = SaveRoot,
            saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot)
        };

        private void RecordSaveFailure(SaveError error, string message)
        {
            SaveStatus = "Save " + error + ": " + message;
            Debug.LogWarning(SaveStatus, this);
        }

        public void DeleteGame(CharacterSlot characterSlot)
        {
            TryDeleteGame(characterSlot);
        }

        public SaveResult<bool> TryDeleteGame(CharacterSlot characterSlot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return SaveResult<bool>.Fail(SaveError.Busy, "Playground saves are isolated.");
#endif
            var root = ResolveSaveRoot();
            if (!root.Success)
            {
                RecordSaveFailure(root.Error, root.Message);
                return SaveResult<bool>.Fail(root.Error, root.Message);
            }
            if (string.IsNullOrEmpty(DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(characterSlot)))
                return SaveResult<bool>.Fail(SaveError.InvalidData, "Select a character slot first.");
            var result = CreateWriter(characterSlot).TryDelete();
            if (result.Success)
            {
                SaveStatus = "";
                RefreshCharacterProfiles();
                if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.RefreshSaveSlotPresentation();
            }
            else RecordSaveFailure(result.Error, result.Message);
            return result;
        }

        public void RefreshCharacterProfiles()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (DevPlaygroundIsolationActive) return;
#endif
            slot01Result = TryLoadSelectedSlot(CharacterSlot.CharacterSlot_01);
            slot02Result = TryLoadSelectedSlot(CharacterSlot.CharacterSlot_02);
            slot03Result = TryLoadSelectedSlot(CharacterSlot.CharacterSlot_03);
            characterSlot01 = slot01Result.Success ? slot01Result.Value : null;
            characterSlot02 = slot02Result.Success ? slot02Result.Value : null;
            characterSlot03 = slot03Result.Success ? slot03Result.Value : null;
        }

        public SaveResult<CharacterSaveData> GetCharacterProfile(CharacterSlot slot)
        {
            switch (slot)
            {
                case CharacterSlot.CharacterSlot_01: return slot01Result;
                case CharacterSlot.CharacterSlot_02: return slot02Result;
                case CharacterSlot.CharacterSlot_03: return slot03Result;
                default: return SaveResult<CharacterSaveData>.Fail(SaveError.InvalidData, "Unknown slot.");
            }
        }

        public IEnumerator LoadWorldScene()
        {
            if (currentCharacterData == null) yield break;
            var command = GameBootstrap.EnsureInstance().Session.LoadGameAsync((int)currentCharacterSlotBeingUsed + 1);
            while (!command.IsCompleted) yield return null;
        }

        public AssetData WorldAssetDefinition => assetData;
        private WorldSceneInitializer worldInitializer;
        public void BindWorldInitializer(WorldSceneInitializer initializer) => worldInitializer = initializer;
        private WorldSceneInitializer ResolveWorldInitializer()
        {
            if (worldInitializer == null) worldInitializer = SceneContext.GetOrCreate(SceneManager.GetActiveScene()).WorldAssets;
            worldInitializer.Configure(assetData); return worldInitializer;
        }
        public Task PrepareWorldAssetsAsync(CancellationToken cancellationToken) => ResolveWorldInitializer().PrepareAsync(assetData, cancellationToken);
        public IEnumerator PrepareWorldAssets() => ResolveWorldInitializer().Prepare();
        // Compatibility for the retained real-resource provenance fixture.
        private IEnumerator InstantiatePrefabWithTextureAsync(GameObject prefab, string texturePath) => ResolveWorldInitializer().InstantiatePrefabWithTextureAsync(prefab, texturePath);

        public int GetWorldSceneIndex()
        {
            return worldSceneIndex;
        }
    }
}
