using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using Karawara.Core.Persistence;


namespace RPGKarawara
{
    public class WorldSaveGameManager : MonoBehaviour
    {
        public static WorldSaveGameManager instance;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The dedicated playground uses in-memory character data, including boss rewards.
        public static bool DevPlaygroundIsolationActive { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDevelopmentIsolation()
        {
            DevPlaygroundIsolationActive = false;
        }
#endif

        #if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        public string SaveRootOverride { get; set; }
        // Actual creation provenance for explicit QA census; never used to drive gameplay.
        public GameObject QaLoadedWorldObject { get; private set; }
        public AssetData QaLoadedWorldAsset { get; private set; }
        public GameObject QaLoadedWorldPrefab { get; private set; }
        public Texture2D QaLoadedWorldTexture { get; private set; }
        public string QaLoadedWorldTexturePath { get; private set; }
        public Vector3 QaLoadedWorldPosition { get; private set; }
        public bool QaWorldTextureApplied { get; private set; }
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
        public bool CanCreateNewGame => !sceneLoading && player != null && player.playerNetworkManager != null && player.IsSpawned && player.IsOwner && loadscene != null;
        private long loadGeneration;
        private bool sceneLoading;
        private readonly Dictionary<PlayerManager, long> appliedPlayers = new Dictionary<PlayerManager, long>();

        [Header("SAVE/LOAD")]
        [SerializeField] bool saveGame;
        [SerializeField] bool loadGame;

        [Header("World Scene Index")]
        [SerializeField] int worldSceneIndex = 1;


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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return false;
#endif
            var scene = SceneManager.GetActiveScene();
            if (loadGeneration == 0 || sceneLoading || currentCharacterData == null || !scene.isLoaded ||
                scene.buildIndex != currentCharacterData.sceneIndex || player == null || !player.IsSpawned || !player.IsOwner)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
        private SaveResult<bool> NewGame(CharacterSlot slot)
        {
            if (!CanCreateNewGame)
            {
                return SaveResult<bool>.Fail(SaveError.Busy, "Waiting for the local player and loading screen.");
            }
            var root = ResolveSaveRoot();
            if (!root.Success) return SaveResult<bool>.Fail(root.Error, root.Message);
            if (!IsEnabledScene(worldSceneIndex)) return SaveResult<bool>.Fail(SaveError.InvalidData, "The new-game scene is not enabled in build settings.");
            // Build the initial snapshot without changing the live player or previous save context.
            CharacterSaveData candidate = new CharacterSaveData();
            var result = CaptureCharacterData(ref candidate);
            if (result.Success)
            {
                candidate.sceneIndex = worldSceneIndex;
                candidate.vitality = 15;
                candidate.endurance = 10;
                candidate.xPosition = -319.92f;
                candidate.yPosition = 175.81f;
                candidate.zPosition = 595.46f;
                if (player.playerStatsManager == null)
                    result = SaveResult<bool>.Fail(SaveError.InvalidData, "Player stats are not ready.");
                else
                {
                    candidate.currentHealth = player.playerStatsManager.CalculateHealthBasedOnVitalityLevel(candidate.vitality);
                    result = CreateWriter(slot).TrySave(candidate);
                }
            }
            if (!result.Success)
            {
                return result;
            }
            AcceptLoad(slot, candidate);
            RefreshCharacterProfiles();
            if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.RefreshSaveSlotPresentation();
            loadscene.LoadGame();
            RequestWorldSceneLoad();
            return SaveResult<bool>.Ok(true);
        }

        // Kept for the existing SceneMenu UnityEvent. Its selection comes from the menu.
        public void SlotTeste()
        {
            // Selection is owned by TitleScreenManager. LoadGame commits active context only after validation.
        }

        public SaveResult<CharacterSaveData> TryLoadSelectedSlot(CharacterSlot slot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return;
#endif
            if (sceneLoading) { RecordSaveFailure(SaveError.Busy, "A world scene is still loading."); return; }
            CharacterSlot selected = TitleScreenManager.Instance != null
                ? TitleScreenManager.Instance.currentSelectedSlot : currentCharacterSlotBeingUsed;
            var result = TryLoadSelectedSlot(selected);
            if (!result.Success)
            {
                RecordSaveFailure(result.Error, result.Message);
                return;
            }
            if (!IsEnabledScene(result.Value.sceneIndex))
            {
                RecordSaveFailure(SaveError.InvalidData, "The saved scene is not enabled in build settings.");
                return;
            }
            AcceptLoad(selected, result.Value);
            RequestWorldSceneLoad();
        }

        public void SaveGame()
        {
            var result = TrySaveGame();
            if (!result.Success) RecordSaveFailure(result.Error, result.Message);
        }

        public SaveResult<bool> TrySaveGame()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return SaveResult<bool>.Fail(SaveError.Busy, "Playground saves are isolated.");
#endif
            var root = ResolveSaveRoot();
            if (!root.Success) return SaveResult<bool>.Fail(root.Error, root.Message);
            if (sceneLoading || (loadGeneration > 0 && (player == null ||
                !appliedPlayers.TryGetValue(player, out long appliedGeneration) || appliedGeneration != loadGeneration)))
                return SaveResult<bool>.Fail(SaveError.Busy, "The accepted save is waiting for its scene and player restoration.");
            if (player == null || player.playerNetworkManager == null || !player.IsSpawned || currentCharacterData == null)
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) yield break;
#endif
            if (!IsEnabledScene(worldSceneIndex)) { RecordSaveFailure(SaveError.InvalidData, "The saved scene is not enabled in build settings."); yield break; }
            if (sceneLoading) yield break;
            sceneLoading = true;
            try
            {
            // Carregar a cena de forma assíncrona
            AsyncOperation sceneLoad = SceneManager.LoadSceneAsync(worldSceneIndex);
            sceneLoad.allowSceneActivation = false;

            float simulatedProgress = 0;

            while (!sceneLoad.isDone)
            {
                // Atualizar progresso simulado
                simulatedProgress = Mathf.Clamp(simulatedProgress + Time.deltaTime * 30, 0, 100);

                

                // Verificar se a cena pode ser ativada
                if (sceneLoad.progress >= 0.9f && simulatedProgress >= 100)
                {
                    Debug.Log("Cena carregada!");
                    sceneLoad.allowSceneActivation = true;
                }

                yield return null;
            }

            // Esperar o próximo quadro para garantir que a cena está ativa
            yield return new WaitForEndOfFrame();

            // Aplicar textura e instanciar o prefab de forma assíncrona
            if (assetData != null && assetData.texturePath != null)
            {
                // Carregar e aplicar a textura de forma assíncrona
                yield return StartCoroutine(InstantiatePrefabWithTextureAsync(assetData.prefab, assetData.texturePath));
            }
            }
            finally { sceneLoading = false; }
            ApplyPendingSaveWhenReady();
        }

        // Método para instanciar o prefab e aplicar a textura
        private IEnumerator InstantiatePrefabWithTextureAsync(GameObject prefab, string texturePath)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            QaWorldTextureApplied = false;
            QaLoadedWorldTexture = null;
            QaLoadedWorldAsset = assetData;
            QaLoadedWorldPrefab = prefab;
            QaLoadedWorldTexturePath = texturePath;
            QaLoadedWorldPosition = assetData == null ? Vector3.zero : assetData.position;
#endif
            if (prefab == null)
            {
                Debug.LogWarning("Prefab não encontrado.");
                yield break;
            }

            GameObject instantiatedObject = Instantiate(prefab, assetData.position, Quaternion.identity);
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            QaLoadedWorldObject = instantiatedObject;
            DevTools.Performance.PerformanceActorLifecycleScope.Notify(instantiatedObject.transform, DevTools.Performance.PerformanceActorLifecycle.Enabled);
#endif
            Debug.Log("Prefab instanciado: " + instantiatedObject.name);
            ResourceRequest resourceRequest = Resources.LoadAsync<Texture2D>(texturePath);
            yield return resourceRequest;
            if (resourceRequest.asset == null)
            {
                Debug.LogError($"Falha ao carregar o recurso: {texturePath}. Verifique se o caminho está correto e o arquivo está na pasta Resources.");
                yield break;
            }

            yield return StartCoroutine(ApplyTextureToObject(instantiatedObject, resourceRequest.asset as Texture2D));
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            if (QaLoadedWorldObject == instantiatedObject && QaLoadedWorldAsset == assetData && QaLoadedWorldPrefab == prefab && QaLoadedWorldTexturePath == texturePath)
            {
                QaLoadedWorldTexture = resourceRequest.asset as Texture2D;
                QaWorldTextureApplied = instantiatedObject != null && QaLoadedWorldTexture != null;
            }
#endif

        }

// Método para aplicar a textura ao objeto instanciado
        private IEnumerator ApplyTextureToObject(GameObject instantiatedObject, Texture2D texture)
        {
            if (instantiatedObject == null || texture == null)
            {
                Debug.Log("Objeto instanciado ou textura não encontrados.");
                yield break;
            }

            // Encontrar todos os Renderers nos filhos do objeto instanciado
            Renderer[] renderers = instantiatedObject.GetComponentsInChildren<Renderer>();
            SkinnedMeshRenderer[] skinnedMeshRenderers = instantiatedObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            // Aplicar textura aos Renderers comuns
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material material = renderer.sharedMaterial;

                    if (material != null)
                    {
                        material.mainTexture = texture;
                        Debug.Log($"Textura aplicada ao material do Renderer: {renderer.gameObject.name}");
                    }
                    else
                    {
                        Debug.Log($"Material não encontrado no Renderer: {renderer.gameObject.name}");
                    }
                }
                else
                {
                    Debug.Log("Renderer não encontrado em um dos filhos do prefab.");
                }
            }

            // Aplicar textura aos SkinnedMeshRenderers
            foreach (SkinnedMeshRenderer skinnedRenderer in skinnedMeshRenderers)
            {
                if (skinnedRenderer != null)
                {
                    Material material = skinnedRenderer.sharedMaterial;

                    if (material != null)
                    {
                        material.mainTexture = texture;
                        Debug.Log($"Textura aplicada ao material do SkinnedMeshRenderer: {skinnedRenderer.gameObject.name}");
                    }
                    else
                    {
                        Debug.Log($"Material não encontrado no SkinnedMeshRenderer: {skinnedRenderer.gameObject.name}");
                    }
                }
                else
                {
                    Debug.Log("SkinnedMeshRenderer não encontrado em um dos filhos do prefab.");
                }
            }
        }

        




        public int GetWorldSceneIndex()
        {
            return worldSceneIndex;
        }
    }
}
