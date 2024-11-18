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

        public PlayerManager player;

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
            saveFileDataWriter = new SaveFileDataWriter();
            saveFileDataWriter.saveDataDirectoryPath = Application.persistentDataPath;

            // Verifica se podemos criar um novo arquivo de salvamento (checando a existência de outros arquivos primeiro)
            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_01);
            currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_01;
            currentCharacterData = new CharacterSaveData();
            NewGame();
            return;
            
            /*
            if (!saveFileDataWriter.CheckToSeeIfFileExists())
            {
                // Se este slot de perfil não estiver ocupado, cria um novo usando este slot
                currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_01;
                currentCharacterData = new CharacterSaveData();
                NewGame();
                return;
            }

            // Repete o processo para o segundo slot
            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_02);

            if (!saveFileDataWriter.CheckToSeeIfFileExists())
            {
                currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_02;
                currentCharacterData = new CharacterSaveData();
                NewGame();
                return;
            }

            // Continua repetindo o processo para os próximos slots até o décimo slot
            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03);

            if (!saveFileDataWriter.CheckToSeeIfFileExists())
            {
                currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_03;
                currentCharacterData = new CharacterSaveData();
                NewGame();
                return;
            }

            
            // Se não houver slots livres, notifica o jogador
            TitleScreenManager.Instance.DisplayNoFreeCharacterSlotsPopUp();

            //OnSlotOverwriteSelected(CharacterSlot.CharacterSlot_01);
            */
        }
        public void OnSlotOverwriteSelected(string selectedSlot)
        {

            switch (selectedSlot)
            {
                case "Save-01":
                    currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_01;
                    break;
                case "Save-02":
                    currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_02;
                    break;
                case "Save-03":
                    currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_03;
                    break;
                default:
                    Debug.LogError("Slot selecionado desconhecido.");
                    break;
            }
            
            //CharacterSlot selectedSlot
            // Aqui você deve implementar a lógica para sobrescrever o slot selecionado
            //currentCharacterSlotBeingUsed = selectedSlot;
            //currentCharacterData = new CharacterSaveData();
           // DeleteGame(currentCharacterSlotBeingUsed);
            NewGame();
        }
        private void NewGame()
        {
            // Salva os dados do novo personagem criado, incluindo estatísticas e itens (quando a tela de criação for adicionada)
            player.playerNetworkManager.vitality.Value = 15;
            player.playerNetworkManager.endurance.Value = 10;
            player.playerNetworkManager.gameObject.transform.position = new Vector3(-319.92f, 100.13f, 595.46f);

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
            //  SAVE THE CURRENT FILE UNDER A FILE NAME DEPENDING ON WHICH SLOT WE ARE USING
            saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(currentCharacterSlotBeingUsed);

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
            //  CHOOSE FILE BASED ON NAME
            saveFileDataWriter = new SaveFileDataWriter();
            saveFileDataWriter.saveDataDirectoryPath = Application.persistentDataPath;
            saveFileDataWriter.saveFileName = DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(characterSlot);
            saveFileDataWriter.DeleteSaveFile();
        }

        //  LOAD ALL CHARACTER PROFILES ON DEVICE WHEN STARTING GAME
        private void LoadAllCharacterProfiles()
        {
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
            //  IF YOU JUST WANT 1 WORLD SCENE USE THIS
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(worldSceneIndex);

            //  IF YOU WANT TO USE DIFFERENT SCENES FOR LEVELS IN YOUR PROJECT USE THIS
            //AsyncOperation loadOperation = SceneManager.LoadSceneAsync(currentCharacterData.sceneIndex);

            player.LoadGameDataFromCurrentCharacterData(ref currentCharacterData);

            while (!loadOperation.isDone)
            {
                yield return null;
            }
        }

        public int GetWorldSceneIndex()
        {
            return worldSceneIndex;
        }
    }
}
