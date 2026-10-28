using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RPGKarawara;
using RPGKarawara.DevTools;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class NewGameMenuRegressionManager : WorldSaveGameManager
{
    public int transitions;
    protected override void RequestWorldSceneLoad() { transitions++; }
}

// Exercises the production scene's serialized event in a disposable preview scene.
// No host startup, player fabrication, save reads/writes, or active-scene replacement.
public static class NewGameMenuRegression
{
    [Serializable] private sealed class Case { public string name; public bool passed; public string detail; }
    [Serializable] private sealed class Report
    {
        public bool completed;
        public int expectedCases = 3, passed, capturedErrors;
        public string saveRoot;
        public bool initialWriteCommandObserved, retryCommandObserved;
        public string boundary = "EditMode preview scene; actual persistent StartNewGame binding invoked with listener state promoted to EditorAndRuntime only in the disposable preview. Host-not-ready failure and retry; real-host capture/IO/success require separate QA fixture.";
        public List<Case> cases = new List<Case>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("Karawara/Validation/TOBE/Run New Game Menu Regression")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening))
            throw new InvalidOperationException("New Game menu preview regression requires EditMode with no listening host.");
        var report = new Report();
        var previousTitle = TitleScreenManager.Instance;
        var previousManager = WorldSaveGameManager.instance;
        float previousTimeScale = Time.timeScale;
        Scene active = SceneManager.GetActiveScene();
        Scene preview = default;
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { report.capturedErrors++; report.errors.Add(message + "\n" + stack); }
        };
        Application.logMessageReceived += capture;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SceneMenu.unity");
            TitleScreenManager title = null;
            Button start = null;
            foreach (var root in preview.GetRootGameObjects())
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        if (button.onClick.GetPersistentMethodName(i) == "StartNewGame" && button.onClick.GetPersistentTarget(i) is TitleScreenManager target)
                        {
                            Require(start == null, "More than one serialized StartNewGame button found.");
                            start = button; title = target;
                        }
                    }
                }
            }
            Require(start != null && title != null, "Production StartNewGame event missing.");
            TitleScreenManager.Instance = title;
            WorldSaveGameManager.instance = null; // Host guard must return before manager/storage access.
            start.gameObject.SetActive(true);
            for (int i = 0; i < start.onClick.GetPersistentEventCount(); i++)
                start.onClick.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
            var visibility = new Dictionary<GameObject, bool>();
            var positions = new Dictionary<Transform, Vector3>();
            foreach (var root in preview.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                { visibility[transform.gameObject] = transform.gameObject.activeSelf; positions[transform] = transform.localPosition; }
            Action unchanged = () =>
            {
                Require(start.gameObject.activeSelf && start.interactable, "Failed new game hid/disabled the actual start button.");
                foreach (var item in visibility) Require(item.Key.activeSelf == item.Value, "Failed new game changed visibility: " + item.Key.name);
                foreach (var item in positions) Require(item.Key.localPosition == item.Value, "Failed new game moved UI/world object: " + item.Key.name);
                Require(SceneManager.GetActiveScene() == active, "Fixture changed the user's active scene.");
            };
            Check(report, "ProductionSerializedStartBinding", () => Require(start.onClick.GetPersistentEventCount() >= 1, "No persistent listener."));
            Check(report, "HostNotReadyPreservesMenu", () =>
            {
                start.onClick.Invoke();
                Require(title.HostStatus == "Host could not start. Try again.", "Host readiness failure was not reached.");
                unchanged();
            });
            Check(report, "HostNotReadyCanRetrySameControl", () => { start.onClick.Invoke(); unchanged(); });
            report.completed = report.cases.Count == report.expectedCases;
        }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            TitleScreenManager.Instance = previousTitle;
            WorldSaveGameManager.instance = previousManager;
            Time.timeScale = previousTimeScale;
            Application.logMessageReceived -= capture;
            string output = Path.Combine("Docs", "Validation", "TOBE", "01b-newgame-menu.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        Debug.Log("New Game Menu Regression: " + report.passed + "/" + report.expectedCases + ", errors=" + report.capturedErrors);
    }

    private static void Check(Report report, string name, Action action)
    {
        var result = new Case { name = name }; report.cases.Add(result);
        try { action(); result.passed = true; report.passed++; }
        catch (Exception ex) { result.detail = ex.GetBaseException().Message; }
    }

    [MenuItem("Karawara/Validation/TOBE/Run Ready Host New Game Menu Regression")]
    public static void RunReadyHost()
    {
        var controller = UnityEngine.Object.FindAnyObjectByType<DevPlaygroundController>();
        if (!EditorApplication.isPlaying || controller == null || !controller.Ready || controller.Network == null ||
            !controller.Network.IsHost || !controller.Network.IsListening || controller.Player == null ||
            !controller.Player.IsSpawned || !controller.Player.IsOwner)
            throw new InvalidOperationException("Requires an already ready DevPlayground production host.");
        var report = new Report
        {
            expectedCases = 4,
            boundary = "Already ready production NGO host/player; actual SceneMenu persistent event in disposable preview with listener state promoted to EditorAndRuntime (preview adapter, not natural playing menu proof); isolated temporary save root and real LoadScene component with fixture overlay. World transition intercepted; no production scene transition or standalone coverage."
        };
        string qaRoot = Path.Combine(Path.GetTempPath(), "Karawara-NewGameMenu-" + Guid.NewGuid().ToString("N"));
        report.saveRoot = qaRoot;
        var oldTitle = TitleScreenManager.Instance;
        var oldManager = WorldSaveGameManager.instance;
        bool oldIsolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
        float oldTime = Time.timeScale;
        var oldActive = SceneManager.GetActiveScene();
        var player = controller.Player;
        Vector3 oldPosition = player.transform.position;
        int oldVitality = player.playerNetworkManager.vitality.Value;
        int oldEndurance = player.playerNetworkManager.endurance.Value;
        int oldHealth = player.playerNetworkManager.currentHealth.Value;
        int oldMaxHealth = player.playerNetworkManager.maxHealth.Value;
        Scene preview = default;
        GameObject managerObject = null, overlay = null;
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            { report.capturedErrors++; report.errors.Add(message + "\n" + stack); }
        };
        Application.logMessageReceived += capture;
        try
        {
            Directory.CreateDirectory(qaRoot);
            TitleScreenManager.Instance = null;
            preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SceneMenu.unity");
            Require(NetworkManager.Singleton == controller.Network && controller.Network.IsHost, "Preview changed the production network singleton.");
            Button start = null;
            TitleScreenManager title = null;
            foreach (var root in preview.GetRootGameObjects())
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentMethodName(i) == "StartNewGame" && button.onClick.GetPersistentTarget(i) is TitleScreenManager target)
                        { Require(start == null, "Duplicate serialized StartNewGame event."); start = button; title = target; }
            Require(start != null, "Preview StartNewGame binding missing.");
            // Preview targets do not belong to the playing world: RuntimeOnly persistent
            // calls are suppressed even during PlayMode. Adapt state only in this preview.
            for (int i = 0; i < start.onClick.GetPersistentEventCount(); i++)
                start.onClick.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
            TitleScreenManager.Instance = title;
            WorldSaveGameManager.instance = null;
            WorldSaveGameManager.DevPlaygroundIsolationActive = false;
            managerObject = new GameObject("New Game menu isolated QA manager");
            var manager = managerObject.AddComponent<NewGameMenuRegressionManager>();
            manager.enabled = false; // This synchronous fixture owns command dispatch, not Update readiness.
            manager.SaveRootOverride = qaRoot;
            manager.player = player;
            var loading = managerObject.AddComponent<LoadScene>();
            overlay = new GameObject("New Game menu QA loading overlay"); overlay.SetActive(false);
            loading.image = overlay; manager.loadscene = loading;
            int firstEnabled = -1;
            foreach (var scene in EditorBuildSettings.scenes) if (scene.enabled) { firstEnabled = 0; break; }
            Require(firstEnabled >= 0, "At least one enabled build scene required.");
            typeof(WorldSaveGameManager).GetField("worldSceneIndex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, firstEnabled);
            Require(manager.CanCreateNewGame, "Actual host player/loading component not ready.");
            Require(!string.IsNullOrWhiteSpace(player.playerNetworkManager.characterName.Value.ToString()), "Actual host player must have a valid character name.");
            var previousData = new CharacterSaveData { characterName = "QA prior context", sceneIndex = 0, currentHealth = 17 };
            manager.currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_03;
            manager.currentCharacterData = previousData;
            string existingPath = Path.Combine(qaRoot, manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03));
            Require(new SaveFileDataWriter { saveDataDirectoryPath = qaRoot, saveFileName = Path.GetFileName(existingPath) }.TrySave(previousData).Success, "QA previous slot seed failed.");
            byte[] existingBytes = File.ReadAllBytes(existingPath);
            string primary = Path.Combine(qaRoot, manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_01));
            string temporary = primary + ".tmp";
            byte[] sentinel = System.Text.Encoding.UTF8.GetBytes("locked QA initial-write target");
            File.WriteAllBytes(temporary, sentinel);
            start.gameObject.SetActive(true);
            var states = new Dictionary<GameObject, bool>();
            foreach (var root in preview.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) states[transform.gameObject] = transform.gameObject.activeSelf;
            using (var locked = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                start.onClick.Invoke();
                report.initialWriteCommandObserved = title.HostStatus != null && title.HostStatus.Contains("IoFailure");
                Check(report, "InitialWriteIoFailureRetainsActualButton", () =>
                {
                    Require(report.initialWriteCommandObserved, "Actual event did not surface typed initial-write IO failure: " + title.HostStatus);
                    Require(start.gameObject.activeSelf && start.interactable, "IO failure hid/disabled actual retry control.");
                    foreach (var state in states) Require(state.Key.activeSelf == state.Value, "IO failure changed preview UI visibility: " + state.Key.name);
                });
                Check(report, "InitialWriteFailurePreservesPlayerContextAndLoading", () =>
                {
                    Require(report.initialWriteCommandObserved, "No typed initial-write failure was observed; preservation cannot pass on a suppressed event.");
                    Require(manager.currentCharacterSlotBeingUsed == CharacterSlot.CharacterSlot_03 && ReferenceEquals(manager.currentCharacterData, previousData), "IO failure committed active context.");
                    Require(player.transform.position == oldPosition && player.playerNetworkManager.vitality.Value == oldVitality && player.playerNetworkManager.endurance.Value == oldEndurance && player.playerNetworkManager.currentHealth.Value == oldHealth, "IO failure changed player state.");
                    Require(!overlay.activeSelf && manager.transitions == 0 && SceneManager.GetActiveScene() == oldActive, "IO failure opened loading UI or requested transition.");
                });
            }
            Check(report, "InitialWriteFailurePreservesQaFiles", () =>
            {
                Require(report.initialWriteCommandObserved, "No typed initial-write failure was observed; preserved files alone do not prove failure handling.");
                Require(!File.Exists(primary) && Convert.ToBase64String(File.ReadAllBytes(existingPath)) == Convert.ToBase64String(existingBytes) &&
                    Convert.ToBase64String(File.ReadAllBytes(temporary)) == Convert.ToBase64String(sentinel), "Failed write changed primary, previous slot, or locked temp bytes.");
            });
            Check(report, "SameSerializedControlRetryCommitsAndHidesOnSuccess", () =>
            {
                start.onClick.Invoke();
                report.retryCommandObserved = manager.transitions == 1 && manager.currentCharacterSlotBeingUsed == CharacterSlot.CharacterSlot_01 && File.Exists(primary);
                Require(report.retryCommandObserved, "Retry event did not reach a successful commit/transition request.");
                Require(!start.gameObject.activeSelf && overlay.activeSelf && manager.transitions == 1, "Successful retry did not hide actual control/show loading/request exactly one transition.");
                Require(manager.currentCharacterSlotBeingUsed == CharacterSlot.CharacterSlot_01 && File.Exists(primary), "Successful retry did not commit selected new slot.");
                Require(new SaveFileDataWriter { saveDataDirectoryPath = qaRoot, saveFileName = Path.GetFileName(primary) }.TryLoad().Success, "Retry primary is not valid legacy save.");
                Require(Convert.ToBase64String(File.ReadAllBytes(existingPath)) == Convert.ToBase64String(existingBytes), "Retry overwrote previous occupied slot.");
            });
            report.completed = report.cases.Count == report.expectedCases;
        }
        catch (Exception ex) { report.errors.Add("Setup/execution limitation: " + ex.GetBaseException().Message); }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay);
            // Restore only potentially changed player values; do not reapply skills or unrelated state.
            if (player != null)
            {
                player.playerNetworkManager.vitality.Value = oldVitality;
                player.playerNetworkManager.endurance.Value = oldEndurance;
                player.playerNetworkManager.currentHealth.Value = oldHealth;
                player.playerNetworkManager.maxHealth.Value = oldMaxHealth;
                if (player.transform.position != oldPosition)
                {
                    bool enabled = player.characterController.enabled; player.characterController.enabled = false;
                    player.transform.position = oldPosition; player.characterController.enabled = enabled;
                }
            }
            WorldSaveGameManager.instance = oldManager;
            TitleScreenManager.Instance = oldTitle;
            WorldSaveGameManager.DevPlaygroundIsolationActive = oldIsolation;
            Time.timeScale = oldTime;
            Application.logMessageReceived -= capture;
            if (Directory.Exists(qaRoot)) Directory.Delete(qaRoot, true);
            string output = Path.Combine("Docs", "Validation", "TOBE", "01b-newgame-menu-ready-host.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        Debug.Log("Ready Host New Game Menu Regression: " + report.passed + "/" + report.expectedCases + ", errors=" + report.capturedErrors);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
