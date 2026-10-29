#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
using System;
using System.Linq;
using System.Threading.Tasks;
using RPGKarawara.SkillTree;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RPGKarawara.DevTools
{
    public sealed class InputRoutingSkillProbe : Skill
    {
        public int activations;
        protected override void Execute(GameObject user) { activations++; }
    }

    public static class InputRoutingFlowRegression
    {
        // Production NPC and HUD, generated actions and actual skill consumer. Only the
        // notification skill is a disposable probe; no content definition is modified.
        public static async Task Run(Keyboard keyboard)
        {
            var input = PlayerInputManager.instance;
            var player = input.player;
            var skills = player.GetComponent<PlayerSkillManager>();
            var npc = UnityEngine.Object.FindObjectsByType<NPCDialogue>(FindObjectsSortMode.None)
                .FirstOrDefault(n => n.missao1 != null && n.missao1.dialogue != null && n.missao1.dialogue.Length >= 2 && n.dialogueUI != null);
            Require(npc != null && input.CanRouteGameplay && skills != null, "Actual Tutorial preconditions: NPC=" + (npc != null) + "; input=" + input.CanRouteGameplay + "; skills=" + (skills != null));
            var originalSlots = skills.slot;
            var probe = ScriptableObject.CreateInstance<InputRoutingSkillProbe>();
            probe.isUnlocked = true; probe.cooldownDuration = 0; probe.ResetCooldown();
            skills.slot = new[] { new SkillSlot { skillSlot = probe, slotCooldownDuration = 0 }, new SkillSlot(), new SkillSlot() };
            try
            {
                npc.SendMessage("IniciarDialogo", SendMessageOptions.RequireReceiver);
                Require(input.Gate.IsBlocked && npc.dialogueUI.activeSelf, "Actual NPC did not acquire dialogue ownership.");
                // Freeze a production dialogue at its mission-choice screen, as if all lines completed.
                npc.SendMessage("FinalizarDialogo", SendMessageOptions.RequireReceiver);
                Require(npc.canvasEscolha.activeSelf && input.Gate.IsBlocked, "Final choice released dialogue prematurely.");
                var origin = player.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Digit1)); await Wait(.15f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait(.15f);
                skills.TryActivateSlot(0);
                Require(probe.activations == 0 && input.moveAmount == 0, "Movement/skill bypassed actual dialogue.");
                await Tap(keyboard, Key.Escape);
                Require(Time.timeScale == 0 && input.Gate.IsBlocked && input.playerControls.UI.enabled, "Escape did not open pause over dialogue.");
                await Tap(keyboard, Key.Escape);
                Require(Time.timeScale > 0 && input.Gate.IsBlocked && !input.playerControls.PlayerMovement.enabled, "Closing pause released dialogue.");
                Require(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Closing pause locked the cursor needed by the active mission choice.");
                var delta = player.transform.position - origin; delta.y = 0;
                Require(delta.magnitude < .1f, "Actual player moved while dialogue owned input.");
                player.gameObject.SetActive(false); player.gameObject.SetActive(true);
                Require(input.Gate.IsBlocked && !input.playerControls.PlayerActions.enabled, "Re-enable discarded actual NPC ownership.");
                npc.SelecionarMissao(1);
                Require(!input.Gate.IsBlocked && input.playerControls.PlayerActions.enabled, "Final mission choice did not release input.");
                await Tap(keyboard, Key.Digit1);
                Require(probe.activations == 1, "Released skill hotkey did not route exactly once after final unlock.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); await Wait(.15f);
                input.SendMessage("OnApplicationFocus", false);
                Require(input.moveAmount == 0 && !input.playerControls.asset.enabled, "Focus loss retained movement/actions.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait(.05f);
                input.SendMessage("OnApplicationFocus", true); await Wait(.1f);
                Require(input.moveAmount == 0 && input.playerControls.PlayerActions.enabled, "Focus return retained held movement or lost actions.");
                using (input.Pause.Acquire(Karawara.Core.Input.InputBlockReason.Cutscene))
                {
                    input.Pause.SetPaused(true); input.Pause.SetPaused(false);
                    Require(Time.timeScale == 0 && input.Gate.IsBlocked, "Panel close released another clock owner.");
                }
                Require(Time.timeScale > 0 && !input.Gate.IsBlocked, "Last clock lease did not restore gameplay.");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                skills.StopAllCoroutines(); skills.slot = originalSlots;
                npc.enabled = false; npc.enabled = true;
                UnityEngine.Object.Destroy(probe);
                input.SendMessage("OnApplicationFocus", true);
                input.Pause.SetPaused(false);
            }
        }
        private static async Task Tap(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); await Wait(.12f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Wait(.12f);
        }
        private static async Task Wait(float duration)
        {
            float end = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < end) await Task.Yield();
        }
        private static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
    }
}
#endif
