using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RPGKarawara.DevTools.Editor
{
    public static class PlaygroundInputRegression
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public class Report
        {
            public string utc, result;
            public bool completed, dodgeAnimationObserved, projectileObserved;
            public int dodgePerformed, lightAttackPerformed;
            public float dodgeDisplacement;
            public List<string> observations = new List<string>(), errors = new List<string>();
        }
        static Report report;
        static DevPlaygroundController controller;
        static PlayerInputManager input;
        static TesteSpellManager selector;
        static Keyboard keyboard, previousKeyboard;
        static Mouse mouse, previousMouse;
        static bool previousOpen, previousInput, previousTroca, previousWeaponActive;
        static float previousTimeScale;
        static bool previousCursor;
        static CursorLockMode previousLock;
        static WeaponItem[] previousSlots;
        static int previousWeaponIndex, previousWeaponID, previousSpellID;
        static readonly List<bool> previousMapStates = new List<bool>();
        static HashSet<int> existingProjectiles;
        static Vector3 dodgePosition;
        static int stage;
        static double deadline, overallDeadline;

        [MenuItem("Karawara/Validation/Run Playground Input Regression")]
        public static void Run()
        {
            if (report != null) return;
            controller = UnityEngine.Object.FindAnyObjectByType<DevPlaygroundController>();
            if (!EditorApplication.isPlaying || controller == null || !controller.Ready)
            {
                Debug.LogError("Enter Play Mode and wait for DevPlayground readiness before the input regression.");
                return;
            }
            input = PlayerInputManager.instance;
            selector = controller.Player.GetComponent<TesteSpellManager>();
            if (input?.playerControls == null || selector == null)
            {
                Debug.LogError("Production input actions and Q selector are required.");
                return;
            }
            var inventory = controller.Player.playerInventoryManager;
            previousOpen = (bool)typeof(DevPlaygroundController).GetField("open", Flags).GetValue(controller);
            previousInput = input.enabled;
            previousTroca = selector.troca;
            previousWeaponActive = controller.Player.playerCombatManager.isWeaponActive;
            previousSlots = inventory.weaponsInRightHandSlots.ToArray();
            previousWeaponIndex = inventory.rightHandWeaponIndex;
            previousWeaponID = controller.Player.playerNetworkManager.currentRightHandWeaponID.Value;
            previousSpellID = controller.Player.playerNetworkManager.currentSpellID.Value;
            previousMapStates.Clear();
            foreach (var map in input.playerControls.asset.actionMaps) previousMapStates.Add(map.enabled);
            previousTimeScale = Time.timeScale;
            previousCursor = Cursor.visible;
            previousLock = Cursor.lockState;
            previousKeyboard = Keyboard.current;
            previousMouse = Mouse.current;
            report = new Report { utc = DateTime.UtcNow.ToString("O"), result = "running" };
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard.MakeCurrent(); mouse.MakeCurrent();
                Time.timeScale = 1;
                typeof(DevPlaygroundController).GetMethod("SetOpen", Flags).Invoke(controller, new object[] { false });
                input.playerControls.PlayerActions.Dodge.performed += OnDodge;
                input.playerControls.PlayerActions.RB.performed += OnAttack;
                stage = 0;
                deadline = EditorApplication.timeSinceStartup + 1;
                overallDeadline = EditorApplication.timeSinceStartup + 25;
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
        }

        static void OnDodge(InputAction.CallbackContext context) => report.dodgePerformed++;
        static void OnAttack(InputAction.CallbackContext context) => report.lightAttackPerformed++;
        static void Tick()
        {
            if (report == null) return;
            try
            {
                if (!EditorApplication.isPlaying || controller == null) throw new InvalidOperationException("Play Mode or playground ended.");
                double now = EditorApplication.timeSinceStartup;
                if (now > overallDeadline) throw new TimeoutException("Input regression exceeded 25 seconds.");
                if (stage == 1)
                {
                    var animator = controller.Player.animator;
                    bool rolling = false;
                    for (int layer = 0; layer < animator.layerCount; layer++)
                        rolling |= animator.GetCurrentAnimatorStateInfo(layer).IsName("Roll_Forward_01") ||
                            animator.GetNextAnimatorStateInfo(layer).IsName("Roll_Forward_01");
                    if (rolling)
                    {
                        if (!report.dodgeAnimationObserved) dodgePosition = controller.Player.transform.position;
                        report.dodgeAnimationObserved = true;
                        report.dodgeDisplacement = Mathf.Max(report.dodgeDisplacement,
                            Vector3.Distance(dodgePosition, controller.Player.transform.position));
                    }
                }
                if (stage >= 5)
                    report.projectileObserved |= UnityEngine.Object.FindObjectsByType<FireBallManager>(FindObjectsSortMode.None)
                        .Any(projectile => !existingProjectiles.Contains(projectile.GetInstanceID()));
                if (now < deadline) return;
                var player = controller.Player;
                report.observations.Add("stage=" + stage + " action=" + player.isPerformingAction +
                    " canDodge=" + player.playerLocomotionManager.canDodge + " actionsEnabled=" + input.playerControls.PlayerActions.enabled +
                    " timeScale=" + Time.timeScale + " weapon=" + player.playerInventoryManager.currentRightHandWeapon?.name);
                switch (stage)
                {
                    case 0:
                        if (player.isPerformingAction) { deadline = now + .2; return; }
                        dodgePosition = player.transform.position;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftCtrl));
                        stage = 1; deadline = now + .65; break;
                    case 1:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        stage = 2; deadline = now + 1; break;
                    case 2:
                        if (player.isPerformingAction) { deadline = now + .2; return; }
                        var fireball = controller.catalog.spells.OfType<FireBallSpell>().FirstOrDefault();
                        if (fireball == null) throw new InvalidOperationException("Catalog has no FireBallSpell.");
                        controller.EquipSpell(fireball);
                        // Exercise Q both ways if starting with caster selected; end with caster.
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                        stage = 3; deadline = now + .1; break;
                    case 3:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        stage = 4; deadline = now + .65; break;
                    case 4:
                        if (selector.troca)
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                            stage = 3; deadline = now + .1; break;
                        }
                        existingProjectiles = UnityEngine.Object.FindObjectsByType<FireBallManager>(FindObjectsSortMode.None)
                            .Select(projectile => projectile.GetInstanceID()).ToHashSet();
                        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
                        stage = 5; deadline = now + .12; break;
                    case 5:
                        InputSystem.QueueStateEvent(mouse, new MouseState());
                        stage = 6; deadline = now + 4; break;
                    case 6:
                        bool success = report.dodgePerformed > 0 && report.dodgeAnimationObserved &&
                            report.dodgeDisplacement > .1f && report.lightAttackPerformed > 0 && report.projectileObserved;
                        Finish(success, success ? "Actual Ctrl/Q/mouse input produced roll movement and fireball." :
                            "One or more input, roll movement, or first-click projectile checks failed.");
                        break;
                }
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                report.errors.Add(message + "\n" + stack);
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Finish(false, "Play Mode stopped.");
        }
        static void Finish(bool completed, string result)
        {
            if (report == null) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayState;
            Application.logMessageReceived -= OnLog;
            report.completed = completed && report.errors.Count == 0;
            report.result = result;
            try
            {
                if (input?.playerControls != null)
                {
                    input.playerControls.PlayerActions.Dodge.performed -= OnDodge;
                    input.playerControls.PlayerActions.RB.performed -= OnAttack;
                }
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                previousKeyboard?.MakeCurrent(); previousMouse?.MakeCurrent();
                if (controller != null && controller.Player != null)
                {
                    var player = controller.Player;
                    player.playerCombatManager.DeactivateActiveWeapon();
                    selector.troca = previousTroca;
                    player.playerInventoryManager.weaponsInRightHandSlots = previousSlots;
                    player.playerInventoryManager.rightHandWeaponIndex = previousWeaponIndex;
                    player.playerNetworkManager.currentRightHandWeaponID.Value = previousWeaponID;
                    player.playerNetworkManager.currentSpellID.Value = previousSpellID;
                    player.playerCombatManager.isWeaponActive = previousWeaponActive;
                    typeof(DevPlaygroundController).GetMethod("SetOpen", Flags).Invoke(controller, new object[] { previousOpen });
                }
                if (input != null && input.playerControls != null)
                {
                    input.enabled = previousInput;
                    int index = 0;
                    foreach (var map in input.playerControls.asset.actionMaps)
                    {
                        if (previousMapStates[index++]) map.Enable(); else map.Disable();
                    }
                }
            }
            catch (Exception ex) { report.errors.Add("Restore failed: " + ex); report.completed = false; }
            finally
            {
                Time.timeScale = previousTimeScale;
                Cursor.visible = previousCursor; Cursor.lockState = previousLock;
                Directory.CreateDirectory("Docs");
                File.WriteAllText("Docs/HomolInputRegression.json", JsonUtility.ToJson(report, true));
                Debug.Log("Playground input regression: " + report.completed + ". " + report.result);
                report = null; keyboard = null; mouse = null;
            }
        }
    }
}
