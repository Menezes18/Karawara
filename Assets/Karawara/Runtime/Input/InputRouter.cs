using Karawara.Core.Input;
using UnityEngine.InputSystem;
namespace RPGKarawara
{
    /// <summary>Session-bound keyboard commands outside the generated action maps.</summary>
    public sealed class InputRouter
    {
        private PlayerManager player;
        private InputGate gate;
        private WorldSaveGameManager saves;
        private bool enabled;
        public void SetEnabled(bool value) { enabled = value; }
        public bool WasPressed(Key key) => CanRouteGameplay && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        public bool WasReleased(Key key) => CanRouteGameplay && Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame;
        public bool DialogueAdvancePressed => enabled && player != null && player.IsInitialized && !player.State.IsDead &&
            UnityEngine.Time.timeScale > 0 && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        public bool RevivePressed => enabled && player != null && player.IsInitialized && gate != null && !gate.IsBlockedExcept(InputBlockReason.Death) &&
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        public bool CanRouteGameplay => enabled && player != null && player.IsInitialized && player.isActiveAndEnabled &&
            !player.State.IsDead && gate != null && !gate.IsBlocked;
        public void Bind(PlayerManager player, InputGate gate, WorldSaveGameManager saves = null) { Unbind(); this.player = player; this.gate = gate; this.saves = saves; }
        public void Unbind() { player = null; gate = null; saves = null; enabled = false; }
        public void RouteSkills(Keyboard keyboard)
        {
            if (!CanRouteGameplay || keyboard == null) return;
            var skills = player.Context?.Skills;
            if (skills != null)
            {
                if (keyboard.digit1Key.wasReleasedThisFrame) skills.TryActivateSlot(0);
                if (keyboard.digit2Key.wasReleasedThisFrame) skills.TryActivateSlot(1);
                if (keyboard.digit3Key.wasReleasedThisFrame) skills.TryActivateSlot(2);
            }
            if (keyboard.lKey.wasReleasedThisFrame && saves != null)
                saves.ApplyPendingSaveWhenReady();
        }
    }
}
