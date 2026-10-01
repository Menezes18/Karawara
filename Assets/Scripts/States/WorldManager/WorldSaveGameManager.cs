using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;


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

        public PlayerManager player;
        public bool CanCreateNewGame => player != null && player.playerNetworkManager != null && player.IsSpawned && player.IsOwner && loadscene != null;

        [Header("SAVE/LOAD")]
        [SerializeField] bool saveGame;
        [SerializeField] bool loadGame;

        [Header("World Scene Index")]
        [SerializeField] int worldSceneIndex = 1;

        [Header("Save Data Writer")]
        private SaveFileDataWriter saveFileDataWriter;

        [Header("Current Character Data")]
        public CharacterSlot currentCharacterSlotBeingUsed;
        public CharacterSaveData currentCharacterData;
        private string saveFileName;

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
            LoadAllCharacterProfiles();
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return;
#endif
            if (!CanCreateNewGame)
            {
                Debug.LogWarning("Wait for the local player and loading screen before creating a game.", this);
                return;
            }
            var writer = new SaveFileDataWriter { saveDataDirectoryPath = Application.persistentDataPath };
            foreach (CharacterSlot slot in new[] { CharacterSlot.CharacterSlot_01, CharacterSlot.CharacterSlot_02, CharacterSlot.CharacterSlot_03 })
            {
                writer.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot);
                if (writer.CheckToSeeIfFileExists()) continue;
                NewGame(slot);
                return;
            }
            if (TitleScreenManager.Instance != null) TitleScreenManager.Instance.DisplayNoFreeCharacterSlotsPopUp();
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
            
            NewGame(slot);
        }
        private void NewGame(CharacterSlot slot)
        {
            if (player == null || player.playerNetworkManager == null || !player.IsSpawned || !player.IsOwner)
            {
                Debug.LogWarning("Wait for the local host and player to spawn before creating a game.");
                return;
            }
            if (loadscene == null)
            {
                Debug.LogError("WorldSaveGameManager has no loading screen; new game was not saved or started.", this);
                return;
            }
            // Only replace the in-memory context once the owned player is ready.
            currentCharacterSlotBeingUsed = slot;
            currentCharacterData = new CharacterSaveData();
            // Salva os dados do novo personagem criado, incluindo estatísticas e itens (quando a tela de criação for adicionada)
            player.playerNetworkManager.vitality.Value = 15;
            player.playerNetworkManager.endurance.Value = 10;
            player.playerNetworkManager.gameObject.transform.position = new Vector3(-319.92f, 175.81f, 595.46f);

            SaveGame();
            loadscene.LoadGame();
            StartCoroutine(LoadWorldScene());
        }



        public void SlotTeste()
        {
            currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_01;
        }
        public void LoadGame()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return;
#endif
            //  LOAD A PREVIOUS FILE, WITH A FILE NAME DEPENDING ON WHICH SLOT WE ARE USING
            saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(currentCharacterSlotBeingUsed);

            saveFileDataWriter = new SaveFileDataWriter();
            //  GENERALLY WORKS ON MULTIPLE MACHINE TYPES (Application.persistentDataPath)
            saveFileDataWriter.saveDataDirectoryPath = Application.persistentDataPath;
            saveFileDataWriter.saveFileName = saveFileName;
            currentCharacterData = saveFileDataWriter.LoadSaveFile();

            StartCoroutine(LoadWorldScene());
        }

        public void SaveGame()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return;
#endif
            if (player == null || player.playerNetworkManager == null || !player.IsSpawned || currentCharacterData == null)
            {
                Debug.LogWarning("No spawned player/save context is ready; save was not written.", this);
                return;
            }
            //  SAVE THE CURRENT FILE UNDER A FILE NAME DEPENDING ON WHICH SLOT WE ARE USING
            saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(currentCharacterSlotBeingUsed);
            if (string.IsNullOrEmpty(saveFileName)) return;

            saveFileDataWriter = new SaveFileDataWriter();
            //  GENERALLY WORKS ON MULTIPLE MACHINE TYPES (Application.persistentDataPath)
            saveFileDataWriter.saveDataDirectoryPath = Application.persistentDataPath;
            saveFileDataWriter.saveFileName = saveFileName;

            //  PASS THE PLAYERS INFO, FROM GAME, TO THEIR SAVE FILE
            player.SaveGameDataToCurrentCharacterData(ref currentCharacterData);

            //  WRITE THAT INFO ONTO A JSON FILE, SAVED TO THIS MACHINE
            saveFileDataWriter.CreateNewCharacterSaveFile(currentCharacterData);
        }

        public void DeleteGame(CharacterSlot characterSlot)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return;
#endif
            //  CHOOSE FILE BASED ON NAME
            saveFileDataWriter = new SaveFileDataWriter();
            saveFileDataWriter.saveDataDirectoryPath = Application.persistentDataPath;
            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(characterSlot);
            saveFileDataWriter.DeleteSaveFile();
        }

        //  LOAD ALL CHARACTER PROFILES ON DEVICE WHEN STARTING GAME
        private void LoadAllCharacterProfiles()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) return;
#endif
            saveFileDataWriter = new SaveFileDataWriter();
            saveFileDataWriter.saveDataDirectoryPath = Application.persistentDataPath;

            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_01);
            characterSlot01 = saveFileDataWriter.LoadSaveFile();

            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_02);
            characterSlot02 = saveFileDataWriter.LoadSaveFile();

            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03);
            characterSlot03 = saveFileDataWriter.LoadSaveFile();

            
        }

        public IEnumerator LoadWorldScene()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevPlaygroundIsolationActive) yield break;
#endif
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

        // Método para instanciar o prefab e aplicar a textura
        private IEnumerator InstantiatePrefabWithTextureAsync(GameObject prefab, string texturePath)
        {
            if (prefab == null)
            {
                Debug.LogWarning("Prefab não encontrado.");
                yield break;
            }

            GameObject instantiatedObject = Instantiate(prefab, assetData.position, Quaternion.identity);
            Debug.Log("Prefab instanciado: " + instantiatedObject.name);
            ResourceRequest resourceRequest = Resources.LoadAsync<Texture2D>(texturePath);
            yield return resourceRequest;
            if (resourceRequest.asset == null)
            {
                Debug.LogError($"Falha ao carregar o recurso: {texturePath}. Verifique se o caminho está correto e o arquivo está na pasta Resources.");
                yield break;
            }

            yield return StartCoroutine(ApplyTextureToObject(instantiatedObject, resourceRequest.asset as Texture2D));

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
