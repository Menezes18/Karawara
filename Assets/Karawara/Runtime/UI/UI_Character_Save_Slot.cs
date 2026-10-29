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

        private SaveSlotsPresenter presenter;
        public void BindServices(WorldSaveGameManager saves, TitleScreenManager menu)
        {
            presenter?.Dispose(); presenter = new SaveSlotsPresenter(this, saves, menu);
        }
        private void OnEnable() => RefreshPresentation();
        private void OnDestroy() { presenter?.Dispose(); presenter = null; }
        public void RefreshPresentation() => presenter?.Refresh();
        public void LoadGameFromCharacterSlot() => presenter?.Load();
        public void SelectCurrentSlot() => presenter?.Select();
    }
}
