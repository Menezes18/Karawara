using System;
using Karawara.Core.Input;
using UnityEngine;

namespace RPGKarawara
{
    /// <summary>The panel, independent from transient player enable, owns its pause lease.</summary>
    public sealed class PauseMenuPresenter : IDisposable
    {
        private readonly GameObject pause, skills;
        private PauseService service;
        private Action restoreCursor;
        private IDisposable lease;
        private bool isMenu;
        public PauseMenuPresenter(GameObject pause, GameObject skills) { this.pause = pause; this.skills = skills; }
        public void Bind(PauseService current, Action cursorPolicy, bool menu)
        {
            if (service != current) { Close(); service = current; }
            restoreCursor = cursorPolicy; isMenu = menu;
            if (menu) Close();
        }
        public void Toggle(int index)
        {
            if (isMenu || service == null) return;
            var panel = index == 1 ? pause : index == 2 ? skills : null;
            var other = index == 1 ? skills : pause;
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf); if (other != null) other.SetActive(false);
            bool visible = (pause != null && pause.activeSelf) || (skills != null && skills.activeSelf);
            if (visible)
            {
                if (lease == null) lease = service.Acquire(InputBlockReason.Pause);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else Release();
        }
        private void Release() { lease?.Dispose(); lease = null; restoreCursor?.Invoke(); }
        public void Close() { if (pause != null) pause.SetActive(false); if (skills != null) skills.SetActive(false); Release(); }
        public void Dispose() { Close(); service = null; restoreCursor = null; }
    }
}
