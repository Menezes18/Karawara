using System;
using Karawara.Core.Persistence;
using UnityEngine;

namespace RPGKarawara
{
    /// <summary>Profile presentation and menu commands use explicitly supplied services.</summary>
    public sealed class SaveSlotsPresenter : IDisposable
    {
        private UI_Character_Save_Slot view;
        private WorldSaveGameManager saves;
        private TitleScreenManager menu;
        public SaveSlotsPresenter(UI_Character_Save_Slot view, WorldSaveGameManager saves, TitleScreenManager menu)
        { this.view = view; this.saves = saves; this.menu = menu; }
        public void Refresh()
        {
            if (saves == null || view == null) return;
            saves.RefreshCharacterProfiles(); var result = saves.GetCharacterProfile(view.characterSlot);
            if (result == null) return;
            if (view.characterName != null) view.characterName.text = result.Success ? result.Value.characterName : result.Error == SaveError.Missing ? "" : "Save indisponível";
            if (view.timedPlayed != null) view.timedPlayed.text = result.Success ? result.Message == "RecoveredFromBackup" ? "Recuperado do backup" : Mathf.FloorToInt(result.Value.secondsPlayed / 60) + " min" : result.Error == SaveError.Missing ? "" : result.Error.ToString();
            bool visible = result.Success || result.Error != SaveError.Missing;
            if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);
        }
        public void Select() { if (menu != null && view != null) menu.SelectCharacterSlot(view.characterSlot); }
        public void Load() { Select(); if (saves != null) saves.LoadGame(); }
        public void Dispose() { view = null; saves = null; menu = null; }
    }
}
