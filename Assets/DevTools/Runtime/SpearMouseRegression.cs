#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RPGKarawara.DevTools
{
    public static class SpearMouseRegression
    {
        public static async Task Run(string outputRoot)
        {
            var bootstrap = GameBootstrap.Instance;
            var player = bootstrap.Session.Player;
            var input = PlayerInputManager.instance;
            Require(bootstrap.Catalog != null && WorldCharacterEffectsManager.instance != null && WorldUtilityManager.Instance != null,
                "Application catalogs/effects/settings were destroyed with the menu.");
            Require(bootstrap.Catalog.transform.IsChildOf(bootstrap.transform), "Catalog has no application owner.");
            var mouse = InputSystem.AddDevice<Mouse>("Spear validation mouse");
            try
            {
                input.SendMessage("OnApplicationFocus", true);
                await Wait(.3f);
                Require(input.CanRouteGameplay && player.characterLocomotionManager.isGrounded, "Mouse test requires active grounded player.");
                bool light = false, window = false; int damaged = 0;
                var target = UnityEngine.Object.FindObjectsByType<CharacterManager>(FindObjectsSortMode.None)
                    .FirstOrDefault(c => c != player && c.characterGroup != player.characterGroup && c.IsInitialized && !c.State.IsDead && c.characterEffectsManager != null);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                float until = Time.realtimeSinceStartup + 1.7f;
                while (Time.realtimeSinceStartup < until)
                {
                    light |= (player.playerCombatManager.lastAttackAnimationPerformed ?? "").StartsWith("Main_Light_Attack", StringComparison.Ordinal);
                    var weapon = player.playerEquipmentManager.rightWeaponManager;
                    if (weapon != null && weapon.meleeDamageCollider != null && weapon.meleeDamageCollider.QaDamageCollider != null && weapon.meleeDamageCollider.QaDamageCollider.enabled)
                    {
                        window = true;
                        if (target != null && damaged == 0)
                        {
                            int before = target.State.Health;
                            weapon.meleeDamageCollider.DamagePublic(target);
                            int after = target.State.Health;
                            Require(after < before, "Open spear hit window did not apply production damage.");
                            weapon.meleeDamageCollider.DamagePublic(target);
                            Require(target.State.Health == after, "One swing damaged the same target twice.");
                            damaged++;
                        }
                    }
                    await Task.Yield();
                }
                InputSystem.QueueStateEvent(mouse, new MouseState()); await Wait(.15f);
                Require(light, "Left mouse never played the authored spear attack.");
                Require(window, "Authored spear animation events never opened the hit collider.");
                Require(target != null && damaged == 1, "No production actor damage target was exercised.");
                var definition = player.playerInventoryManager.currentRightHandWeapon;
                Require(definition != null && definition.itemID == 1, "Mouse attack failed to equip the authored spear.");
                var model = player.playerEquipmentManager.rightHandWeaponModel;
                Require(model != null && model.transform.parent == player.playerEquipmentManager.rightHandSlot.transform, "Spear model detached from its authored hand.");
                Require(model.GetComponentsInChildren<Renderer>().Any(r => r.enabled && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name != "Hidden/InternalErrorShader")), "Spear has no valid visible material/renderer.");
                Require(Vector3.Distance(model.transform.localScale, definition.weaponModel.transform.localScale) < .0001f, "Spear root scale changed.");
                if (!string.IsNullOrEmpty(outputRoot)) ScreenCapture.CaptureScreenshot(Path.Combine(outputRoot, "spear-equipped.png"));
                await Wait(1.0f);
                bool heavy = false;
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                until = Time.realtimeSinceStartup + .8f;
                while (Time.realtimeSinceStartup < until)
                {
                    heavy |= (player.playerCombatManager.lastAttackAnimationPerformed ?? "").StartsWith("Main_Heavy_Attack", StringComparison.Ordinal);
                    await Task.Yield();
                }
                InputSystem.QueueStateEvent(mouse, new MouseState()); await Wait(.3f);
                Require(heavy, "Right mouse never started the authored heavy attack.");
                using (input.Gate.Acquire(Karawara.Core.Input.InputBlockReason.Dialogue))
                {
                    string last = player.playerCombatManager.lastAttackAnimationPerformed;
                    InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); await Wait(.15f);
                    InputSystem.QueueStateEvent(mouse, new MouseState()); await Wait(.15f);
                    Require(last == player.playerCombatManager.lastAttackAnimationPerformed, "Mouse attack bypassed dialogue.");
                }
            }
            finally { InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.RemoveDevice(mouse); }
        }
        private static async Task Wait(float seconds)
        { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) await Task.Yield(); }
        private static void Require(bool condition, string detail) { if (!condition) throw new InvalidOperationException(detail); }
    }
}
#endif
