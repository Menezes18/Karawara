using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] Button newGameStartButton;
        [SerializeField] Button deleteCharacterPopUpConfirmButton;

        [Header("Pop Ups")]
        [SerializeField] GameObject noCharacterSlotsPopUp;
        [SerializeField] Button noCharacterSlotsOkayButton;
        [SerializeField] GameObject deleteCharacterSlotPopUp;

        [Header("Character Slots")]
        public CharacterSlot currentSelectedSlot = CharacterSlot.NO_SLOT;
        public string SessionStatus { get; private set; }

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

        // Preserve serialized menu UnityEvents while starting only application services.
        public void StartLocalRuntime() { TryStartLocalRuntime(); }
        public bool TryStartLocalRuntime()
        {
            GameBootstrap.EnsureInstance(); SessionStatus = null; return true;
        }
        public void StartNewGame()
        {
            if (WorldSaveGameManager.instance == null) { SessionStatus = "Save service is unavailable. Try again."; return; }
            var result = WorldSaveGameManager.instance.TryCreateNewGame();
            if (!result.Success) { SessionStatus = "Save " + result.Error + ": " + result.Message; return; }
            SessionStatus = null;
            if (newGameStartButton != null) newGameStartButton.gameObject.SetActive(false);
        }
        private void OnGUI()
        {
            string status = WorldSaveGameManager.instance != null ? WorldSaveGameManager.instance.SaveStatus : SessionStatus;
            if (string.IsNullOrEmpty(status)) status = SessionStatus;
            if (!string.IsNullOrEmpty(status)) GUI.Label(new Rect(12, Screen.height - 28, Mathf.Min(Screen.width - 24, 650), 24), status);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void RefreshSaveSlotPresentation()
        {
            if (titleScreenLoadMenu == null) return;
            foreach (var slot in titleScreenLoadMenu.GetComponentsInChildren<UI_Character_Save_Slot>(true))
                slot.RefreshPresentation();
        }

        public void OpenLoadGameMenu()
        {
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.RefreshCharacterProfiles();
            RefreshSaveSlotPresentation();
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
            if (WorldSaveGameManager.instance == null) return;
            var result = WorldSaveGameManager.instance.TryDeleteGame(currentSelectedSlot);
            if (!result.Success)
            {
                // Keep the confirmation visible; display the explicit failure through the existing status path.
                SessionStatus = "Save " + result.Error + ": " + result.Message;
                return;
            }
            if (deleteCharacterSlotPopUp != null) deleteCharacterSlotPopUp.SetActive(false);
            SelectNoSlot();
            RefreshSaveSlotPresentation();
            if (loadMenuReturnButton != null) loadMenuReturnButton.Select();
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
