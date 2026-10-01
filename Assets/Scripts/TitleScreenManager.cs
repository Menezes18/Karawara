using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

namespace RPGKarawara
{
    public class TitleScreenManager : MonoBehaviour
    {
        public static TitleScreenManager Instance;

        [Header("Menus")]
        [SerializeField] GameObject titleScreenMainMenu;
        [SerializeField] GameObject titleScreenLoadMenu;

        [Header("Buttons")]
        [SerializeField] Button loadMenuReturnButton;
        [SerializeField] Button mainMenuLoadGameButton;
        [SerializeField] Button mainMenuNewGameButton;
        [SerializeField] Button deleteCharacterPopUpConfirmButton;

        [Header("Pop Ups")]
        [SerializeField] GameObject noCharacterSlotsPopUp;
        [SerializeField] Button noCharacterSlotsOkayButton;
        [SerializeField] GameObject deleteCharacterSlotPopUp;

        [Header("Character Slots")]
        public CharacterSlot currentSelectedSlot = CharacterSlot.NO_SLOT;
        public ushort ActiveHostPort { get; private set; }
        public string HostStatus { get; private set; }

        private void Awake()
        {
            Time.timeScale = 1;
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void StartNetworkAsHost()
        {
            TryStartNetworkAsHost();
        }

        public bool TryStartNetworkAsHost()
        {
            try
            {
                NetworkManager network = NetworkManager.Singleton;
                bool started = LocalHostStartup.TryStartHost(network, out ushort port);
                if (started && port == 0 && network.NetworkConfig.NetworkTransport is Unity.Netcode.Transports.UTP.UnityTransport transport)
                    port = transport.GetLocalEndpoint().Port;
                ActiveHostPort = port;
                HostStatus = started ? "Host ready on port " + port : "Host could not start. Try again.";
                if (!started) Debug.LogWarning(HostStatus);
                return started;
            }
            catch (System.Exception ex)
            {
                ActiveHostPort = 0;
                HostStatus = "Host could not start: " + ex.Message;
                Debug.LogWarning(HostStatus);
                return false;
            }
        }

        public void StartNewGame()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || !NetworkManager.Singleton.IsListening)
            {
                HostStatus = "Host could not start. Try again.";
                return;
            }
            if (WorldSaveGameManager.instance == null || !WorldSaveGameManager.instance.CanCreateNewGame)
            {
                HostStatus = "Waiting for the local player before starting a new game.";
                return;
            }
            WorldSaveGameManager.instance.AttemptToCreateNewGame();
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(HostStatus)) return;
            bool hostReady = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost && NetworkManager.Singleton.IsListening;
            string status = hostReady && ActiveHostPort > 0 ? "Host · porta " + ActiveHostPort : HostStatus;
            if (hostReady && (WorldSaveGameManager.instance == null || !WorldSaveGameManager.instance.CanCreateNewGame))
                status += " · preparando jogador";
            GUI.Label(new Rect(12, Screen.height - 28, Mathf.Min(Screen.width - 24, 550), 24), status);
        }

        public void OpenLoadGameMenu()
        {
            //  CLOSE MAIN MENU
            titleScreenMainMenu.SetActive(false);

            //  OPEN LOAD MENU
            titleScreenLoadMenu.SetActive(true);

            //  SELECT THE RETURN BUTTON FIRST
            loadMenuReturnButton.Select();
        }

        public void CloseLoadGameMenu()
        {
            //  CLOSE LOAD MENU
            titleScreenLoadMenu.SetActive(false);

            //  OPEN MAIN MENU
            titleScreenMainMenu.SetActive(true);

            //  SELECT THE LOAD BUTTON
            mainMenuLoadGameButton.Select();
        }

        public void DisplayNoFreeCharacterSlotsPopUp()
        {
            if (noCharacterSlotsPopUp != null) noCharacterSlotsPopUp.SetActive(true);
            if (noCharacterSlotsOkayButton != null) noCharacterSlotsOkayButton.Select();
        }

        public void CloseNoFreeCharacterSlotsPopUp()
        {
            noCharacterSlotsPopUp.SetActive(false);
            mainMenuNewGameButton.Select();
        }

        //  CHARACTER SLOTS

        public void SelectCharacterSlot(CharacterSlot characterSlot)
        {
            currentSelectedSlot = characterSlot;
        }

        public void SelectNoSlot()
        {
            currentSelectedSlot = CharacterSlot.NO_SLOT;
        }

        public void AttemptToDeleteCharacterSlot()
        {
            if (currentSelectedSlot != CharacterSlot.NO_SLOT)
            {
                deleteCharacterSlotPopUp.SetActive(true);
                deleteCharacterPopUpConfirmButton.Select();
            }
        }

        public void DeleteCharacterSlot()
        {
            deleteCharacterSlotPopUp.SetActive(false);
            WorldSaveGameManager.instance.DeleteGame(currentSelectedSlot);

            //  WE DISABLE AND THEN ENABLE THE LOAD MENU, TO REFRESH THE SLOTS (The deleted slots will now become inactive)
            titleScreenLoadMenu.SetActive(false);
            titleScreenLoadMenu.SetActive(true);

            loadMenuReturnButton.Select();
        }

        public void CloseDeleteCharacterPopUp()
        {
            deleteCharacterSlotPopUp.SetActive(false);
            loadMenuReturnButton.Select();
        }
        public void QuitGame()
            {
               
                Debug.Log("Saindo do jogo...");
        
               
                Application.Quit();
        
                
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }
    }
}
