using UnityEngine;
using TMPro;
using Karawara.Core.Persistence;

namespace RPGKarawara
{
    public class UI_Character_Save_Slot : MonoBehaviour
    {
        [Header("Game Slot")]
        public CharacterSlot characterSlot;

        [Header("Character Info")]
        public TextMeshProUGUI characterName;
        public TextMeshProUGUI timedPlayed;

        private void OnEnable() => RefreshPresentation();

        public void RefreshPresentation()
        {
            var manager = WorldSaveGameManager.instance;
            if (manager == null) return;
            manager.RefreshCharacterProfiles();
            var result = manager.GetCharacterProfile(characterSlot);
            if (result == null) return;
            if (characterName != null)
                characterName.text = result.Success ? result.Value.characterName :
                    (result.Error == SaveError.Missing ? "" : "Save indisponível");
            if (timedPlayed != null)
                timedPlayed.text = result.Success
                    ? (result.Message == "RecoveredFromBackup" ? "Recuperado do backup" : Mathf.FloorToInt(result.Value.secondsPlayed / 60) + " min")
                    : (result.Error == SaveError.Missing ? "" : result.Error.ToString());
            // Preserve the existing empty-slot layout; invalid slots stay visible with status.
            bool visible = result.Success || result.Error != SaveError.Missing;
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        public void LoadGameFromCharacterSlot()
        {
            SelectCurrentSlot();
            if (WorldSaveGameManager.instance == null) return;
            // The prefab command and SceneMenu command use the same selected-slot validation.
            WorldSaveGameManager.instance.LoadGame();
        }

        public void SelectCurrentSlot()
        {
            if (TitleScreenManager.Instance != null)
                TitleScreenManager.Instance.SelectCharacterSlot(characterSlot);
        }
    }
}
