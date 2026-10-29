#if DEVELOPMENT_BUILD || KARAWARA_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Karawara.Core.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools
{
    // Explicit QA process only. No timing or performance acceptance is produced.
    public sealed class LocalRuntimeProcessRegression : MonoBehaviour
    {
        [Serializable] private sealed class Case { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string utc, finishedUtc, buildGuid, saveRoot;
            public string boundary = "Standalone local-runtime functional checks; virtual Input System keyboard; background input permitted only in this explicit QA process. Not timing or hardware-input certification.";
            public bool completed, passed, personalFilesUnchanged;
            public List<Case> cases = new List<Case>();
            public List<string> errors = new List<string>();
            public float movementDistance;
            public int pauseDismissals;
        }
        private Report report;
        private string output;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior previousBackground;
        private GameSessionController session;
        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Any(a => a == "-karawaraG2Validation" || a == "-karawaraG3AValidation")) return;
            var owner = new GameObject("Explicit G2 process validation");
            DontDestroyOnLoad(owner); owner.AddComponent<LocalRuntimeProcessRegression>();
        }
        private async void Start()
        {
            string root = Argument("-karawaraSaveRoot");
            if (!Karawara.Core.Persistence.SaveRootPolicy.Resolve(Application.persistentDataPath, new[] { "-karawaraSaveRoot", root }, true).Success)
            { Debug.LogError("G2 validation requires an isolated absolute save root."); Application.Quit(2); return; }
            bool g3a = Environment.GetCommandLineArgs().Contains("-karawaraG3AValidation");
            output = Path.Combine(root, g3a ? "g3a-standalone.json" : "g2-standalone.json"); Directory.CreateDirectory(root);
            report = new Report { utc = DateTime.UtcNow.ToString("O"), buildGuid = Application.buildGUID, saveRoot = root };
            var before = PersonalFingerprint();
            previousBackground = InputSystem.settings.backgroundBehavior;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.logMessageReceived += OnLog;
            try
            {
                var bootstrap = GameBootstrap.EnsureInstance(); session = bootstrap.Session;
                var saves = WorldSaveGameManager.instance;
                if (saves == null) throw new InvalidOperationException("Production save service unavailable.");
                saves.SaveRootOverride = root;
                await Check("MenuHasNoPlayerOrNetworkSession", () =>
                {
                    Require(SceneManager.GetActiveScene().name == "SceneMenu" && !session.IsActive && FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Length == 0, "Menu created a gameplay player.");
                    Require(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("Unity.Netcode", StringComparison.Ordinal)), "Gameplay networking assembly loaded.");
                    return Task.CompletedTask;
                });
                await Check("NewGameRestoresPlayerHudOnce", async () =>
                {
                    Require(await session.StartNewGameAsync(1) == SessionResult.Success, session.LastError);
                    Require(session.Player != null && session.Player.State.Health == 225 && FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Length == 1, "New-game player/state mismatch.");
                    Require(PlayerInputManager.instance.player == session.Player && PlayerUIManager.instance.playerUIHudManager.IsHealthBarReady, "Player/input/HUD binding mismatch.");
                });
                await Check("ProductionKeyboardMovementJumpAndCtrl", async () =>
                {
                    var player = session.Player;
                    Require(player != null, "No player.");
                    float deadline = Time.realtimeSinceStartup + 40;
                    while (!player.characterLocomotionManager.isGrounded && Time.realtimeSinceStartup < deadline) await Wait(.1f);
                    Require(player.characterLocomotionManager.isGrounded, "Player did not reach authored ground.");
                    keyboard = InputSystem.AddDevice<Keyboard>("G2 virtual keyboard");
                    // This explicit functional QA process is hidden and permits background
                    // virtual input. Exercise the production focus handler explicitly;
                    // this is not evidence that a hidden window has native focus.
                    if (g3a) PlayerInputManager.instance.SendMessage("OnApplicationFocus", true);
                    PlayerInputManager.instance.SetSessionInputEnabled(true);
                    Vector3 origin = player.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    await Wait(1.2f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Vector3 delta = player.transform.position - origin; delta.y = 0;
                    report.movementDistance = delta.magnitude;
                    Require(delta.magnitude > .15f, "Generated movement binding did not move the actual player.");
                    await Wait(.3f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    bool jumped = false; float until = Time.realtimeSinceStartup + .6f;
                    while (Time.realtimeSinceStartup < until) { jumped |= player.State.IsJumping; await Task.Yield(); }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(jumped, "Space did not enter the production jump state.");
                    await Wait(2f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftCtrl));
                    bool dodged = false; until = Time.realtimeSinceStartup + .6f;
                    while (Time.realtimeSinceStartup < until) { dodged |= player.isPerformingAction; await Task.Yield(); }
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(dodged, "Ctrl did not enter a production dodge action.");
                    await Wait(1f);
                    ScreenCapture.CaptureScreenshot(Path.Combine(root, "tutorial-player.png"));
                    await Wait(.5f);
                });
                if (g3a) await Check("ActualDialoguePauseSkillFocusOwnership", () => InputRoutingFlowRegression.Run(keyboard));
                if (g3a) await Check("MouseSpearModelAnimationAndDamage", () => SpearMouseRegression.Run(root));
                for (int slot = 1; slot <= 3; slot++)
                {
                    int selected = slot;
                    await Check("SaveLoadSlot" + selected, async () =>
                    {
                        Require(session.Player != null, "No active player.");
                        saves.currentCharacterSlotBeingUsed = (CharacterSlot)(selected - 1);
                        session.Player.State.SetHealth(90 + selected * 10);
                        Require(saves.TrySaveGame().Success, saves.SaveStatus);
                        Require(await session.LoadGameAsync(selected) == SessionResult.Success, session.LastError);
                        Require(session.Player.State.Health == 90 + selected * 10, "Selected slot HP did not round-trip.");
                        session.Player.State.SetHealth(80 + selected);
                        Require(!saves.ApplyPendingSaveWhenReady() && session.Player.State.Health == 80 + selected, "Completed load generation applied twice.");
                    });
                }
                await Check("PausedLoadCompletes", async () =>
                {
                    Time.timeScale = 0;
                    Require(await session.LoadGameAsync(1) == SessionResult.Success, session.LastError);
                    Require(Time.timeScale == 1, "Completed loading did not release pause.");
                });
                await Check("ReturnToMenuDisposesPlayer", async () =>
                {
                    Require(await session.ReturnToMenuAsync() == SessionResult.Success, session.LastError);
                    await Wait(.2f);
                    Require(!session.IsActive && saves.player == null && PlayerInputManager.instance.player == null && FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Length == 0, "Menu retained gameplay player/bindings.");
                });
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                Application.logMessageReceived -= OnLog;
                report.personalFilesUnchanged = before == PersonalFingerprint();
                report.completed = true; report.finishedUtc = DateTime.UtcNow.ToString("O");
                report.passed = report.personalFilesUnchanged && report.errors.Count == 0 && report.cases.Count == (g3a ? 10 : 8) && report.cases.TrueForAll(c => c.passed);
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Application.Quit(report.passed ? 0 : 2);
            }
        }
        private async Task Check(string name, Func<Task> action)
        {
            var row = new Case { name = name }; report.cases.Add(row);
            try { await action(); row.passed = true; } catch (Exception e) { row.detail = e.ToString();
                var input = PlayerInputManager.instance;
                if (input != null) row.detail += "\nInput guard: blocked=" + input.Gate.IsBlocked + "; session=" + input.SessionInputEnabled +
                    "; focus=" + typeof(PlayerInputManager).GetField("hasFocus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(input) +
                    "; router=" + input.Router.CanRouteGameplay + "; scale=" + Time.timeScale;
                 }
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        private async Task Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                if (Time.timeScale == 0)
                    foreach (var popup in FindObjectsByType<PopUpTutorial>(FindObjectsSortMode.None))
                        if (!popup.passou && popup.IsShowing)
                        { popup.close(); report.pauseDismissals++; }
                await Task.Yield();
            }
        }
        private void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message + "\n" + stack); }
        private static void Require(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
        private static string PersonalFingerprint()
        {
            if (!Directory.Exists(Application.persistentDataPath)) return "missing";
            using (var sha = SHA256.Create())
                return string.Join("\n", Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
                    .Select(p => p + "|" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p)))));
        }
    }
}
#endif
