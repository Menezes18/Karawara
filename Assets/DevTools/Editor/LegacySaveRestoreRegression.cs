using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RPGKarawara;
using RPGKarawara.DevTools;
using RPGKarawara.SkillTree;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class LegacyRestoreRegressionManager : WorldSaveGameManager
{
    public int transitions;
    protected override void RequestWorldSceneLoad() { transitions++; }
}

// Runs synchronously in an already ready production host. No fabricated NGO ownership.
public static class LegacySaveRestoreRegression
{
    [Serializable] private sealed class Case { public string name; public bool passed; public string detail; }
    [Serializable] private sealed class Report
    {
        public bool completed, realHost;
        public int passed, expectedCases = 10, capturedErrors;
        public string boundary = "Already ready DevPlayground host; transition requests observed without loading production scenes. Actual production-scene spawn and standalone remain separate gates.";
        public string saveRoot;
        public List<Case> cases = new List<Case>();
        public List<string> errors = new List<string>();
    }

    [MenuItem("Karawara/Validation/TOBE/Run Save Root Consumer Regression")]
    public static void RunRootConsumer()
    {
        var previous = WorldSaveGameManager.instance;
        bool isolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
        var go = new GameObject("Save root consumer QA");
        var report = new Report { expectedCases = 3, boundary = "WorldSaveGameManager root consumer in Editor, no personal file operations." };
        try
        {
            WorldSaveGameManager.instance = null;
            WorldSaveGameManager.DevPlaygroundIsolationActive = false;
            var manager = go.AddComponent<LegacyRestoreRegressionManager>();
            Action<string, Action> check = (name, action) => { var c = new Case { name = name }; report.cases.Add(c); try { action(); c.passed = true; report.passed++; } catch (Exception ex) { c.detail = ex.GetBaseException().Message; } };
            check("RelativeOverrideIsRejected", () => { manager.SaveRootOverride = "relative-QA-root"; Require(manager.SaveRoot == null, "Relative override was not rejected before IO."); });
            check("PersonalOverrideIsRejected", () => { manager.SaveRootOverride = Application.persistentDataPath; Require(manager.SaveRoot == null, "Override allowed the personal root."); });
            check("InvalidRootReturnsTypedFailure", () => { manager.SaveRootOverride = "relative-QA-root"; var result = manager.TryLoadSelectedSlot(CharacterSlot.CharacterSlot_01); Require(!result.Success && result.Error == Karawara.Core.Persistence.SaveError.InvalidData, "Invalid root was not surfaced as InvalidData."); });
            report.completed = report.cases.Count == report.expectedCases;
        }
        finally { Object.DestroyImmediate(go); WorldSaveGameManager.instance = previous; WorldSaveGameManager.DevPlaygroundIsolationActive = isolation; File.WriteAllText("Docs/Validation/TOBE/Execution/legacy-root-consumer-regression.json", JsonUtility.ToJson(report, true)); }
        Debug.Log("Save Root Consumer Regression: " + report.passed + "/" + report.expectedCases);
    }

    [MenuItem("Karawara/Validation/TOBE/Run Legacy Save Restore Regression")]
    public static void Run()
    {
        var controller = Object.FindAnyObjectByType<DevPlaygroundController>();
        if (!EditorApplication.isPlaying || controller == null || !controller.Ready || !controller.Network.IsHost || !controller.Player.IsSpawned || !controller.Player.IsOwner)
        { Debug.LogError("Legacy restore regression requires an already ready DevPlayground production host."); return; }
        var report = new Report { realHost = true, saveRoot = Path.Combine(Path.GetTempPath(), "Karawara-LegacyRestore-" + Guid.NewGuid().ToString("N")) };
        var previousManager = WorldSaveGameManager.instance;
        var previousTitle = TitleScreenManager.Instance;
        bool isolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
        var player = controller.Player;
        var skillManager = PlayerSkillManager.instance;
        var previousSkillUi = SkillTreeUiManager.instance;
        var skillUi = previousSkillUi != null ? previousSkillUi : Object.FindAnyObjectByType<SkillTreeUiManager>(FindObjectsInactive.Include);
        var original = new CharacterSaveData();
        bool capturedOriginal = false;
        Skill skillA = null, skillB = null;
        var buildScenes = EditorBuildSettings.scenes;
        var managerObject = new GameObject("Legacy restore QA manager");
        var savedScene = SceneManager.GetActiveScene();
        PlayerManager replacement = null;
        bool originalDespawned = false;
        Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { report.capturedErrors++; report.errors.Add(message + "\n" + stack); } };
        Application.logMessageReceived += capture;
        try
        {
            Require(skillUi != null && skillManager != null, "Production skill components are missing.");
            // Inactive authored skill panel has not run Awake; use that actual component.
            SkillTreeUiManager.instance = skillUi;
            player.SaveGameDataToCurrentCharacterData(ref original);
            capturedOriginal = true;
            // QA settings are restored in finally; never rewrite a production scene or builder.
            var qaScenes = new List<EditorBuildSettingsScene>(buildScenes);
            if (savedScene.buildIndex < 0) qaScenes.Add(new EditorBuildSettingsScene(savedScene.path, true));
            EditorBuildSettings.scenes = qaScenes.ToArray();
            int sceneIndex = SceneManager.GetActiveScene().buildIndex;
            Require(sceneIndex >= 0, "QA scene did not obtain an enabled build index.");
            WorldSaveGameManager.instance = null;
            TitleScreenManager.Instance = null;
            WorldSaveGameManager.DevPlaygroundIsolationActive = false;
            var manager = managerObject.AddComponent<LegacyRestoreRegressionManager>();
            manager.SaveRootOverride = report.saveRoot;
            manager.player = player;
            skillA = Object.Instantiate(controller.catalog.skills[0]);
            skillB = Object.Instantiate(controller.catalog.skills[controller.catalog.skills.Length - 1]);
            Require(skillA != skillB, "QA requires two distinct catalog skills.");
            var slot1 = Data(sceneIndex, 10, 70, original, skillA);
            var slot3 = Data(sceneIndex, 20, 90, original, skillB);
            Write(manager, CharacterSlot.CharacterSlot_01, slot1);
            Write(manager, CharacterSlot.CharacterSlot_03, slot3);
            manager.characterSlot01 = slot1;
            MethodInfo pending = typeof(WorldSaveGameManager).GetMethod("ApplyPendingSaveWhenReady", Type.EmptyTypes);
            MethodInfo apply = typeof(PlayerManager).GetMethod("ApplyLegacySave", new[] { typeof(CharacterSaveData) });
            Func<bool> tryPending = () => pending != null && (bool)pending.Invoke(manager, null);
            Action<string, Action> check = (name, action) => { var c = new Case { name = name }; report.cases.Add(c); try { action(); c.passed = true; report.passed++; } catch (Exception ex) { c.detail = ex.GetBaseException().Message; } };
            Action load3 = () => { manager.currentCharacterSlotBeingUsed = CharacterSlot.CharacterSlot_03; manager.LoadGame(); };
            Action<object, FieldInfo> deferredHud = (owner, field) =>
            {
                object binding = field.GetValue(owner);
                player.playerNetworkManager.currentHealth.Value = 80;
                Vector3 beforePosition = player.transform.position;
                int applyCount = 0;
                Unity.Netcode.NetworkVariable<int>.OnValueChangedDelegate count = (oldHealth, health) => { if (health == 90) applyCount++; };
                player.playerNetworkManager.currentHealth.OnValueChanged += count;
                try
                {
                    field.SetValue(owner, null);
                    load3();
                    string acceptedContext = JsonUtility.ToJson(manager.currentCharacterData);
                    Require(!tryPending() && player.playerNetworkManager.currentHealth.Value == 80 && player.transform.position == beforePosition &&
                        JsonUtility.ToJson(manager.currentCharacterData) == acceptedContext && applyCount == 0,
                        "Incomplete actual HUD binding mutated or consumed pending restore.");
                    field.SetValue(owner, binding);
                    Require(tryPending() && player.playerNetworkManager.currentHealth.Value == 90 && applyCount == 1, "Ready HUD did not permit exactly one restore.");
                    var hud = PlayerUIManager.instance.playerUIHudManager;
                    var bar = (UI_StatBar)typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
                    Require(bar.GetComponent<Slider>().value == 90 && !tryPending(), "Ready HUD did not display 90 or restore repeated.");
                }
                finally { field.SetValue(owner, binding); player.playerNetworkManager.currentHealth.OnValueChanged -= count; }
            };
            check("MissingHealthBarDefersWithoutMutation", () => deferredHud(PlayerUIManager.instance.playerUIHudManager,
                typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.NonPublic | BindingFlags.Instance)));
            check("UninitializedHealthSliderDefersWithoutMutation", () => {
                var bar = (UI_StatBar)typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PlayerUIManager.instance.playerUIHudManager);
                deferredHud(bar, typeof(UI_StatBar).GetField("slider", BindingFlags.NonPublic | BindingFlags.Instance));
            });
            check("LocalHostRestoresSelectedSlot", () => { load3(); tryPending(); Require(player.playerNetworkManager.currentHealth.Value == 90 && player.playerNetworkManager.vitality.Value == 20, "Host did not restore HP90/vitality20 from slot3."); var bar = (UI_StatBar)typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(PlayerUIManager.instance.playerUIHudManager); Require(bar != null && bar.GetComponent<Slider>().value == 90, "Real HUD health did not restore to 90."); });
            check("ExistingPlayerRestoresOnce", () =>
            {
                int applyCount = 0;
                Unity.Netcode.NetworkVariable<int>.OnValueChangedDelegate count = (oldHealth, health) => { if (health == 90) applyCount++; };
                player.playerNetworkManager.currentHealth.Value = 80;
                player.playerNetworkManager.currentHealth.OnValueChanged += count;
                try { load3(); tryPending(); player.playerNetworkManager.currentHealth.Value = 83; Require(!tryPending() && player.playerNetworkManager.currentHealth.Value == 83 && applyCount == 1, "Late readiness reapplied save over live damage; applyCount=" + applyCount); load3(); tryPending(); Require(player.playerNetworkManager.currentHealth.Value == 90 && applyCount == 2, "New accepted generation did not restore same player exactly once."); }
                finally { player.playerNetworkManager.currentHealth.OnValueChanged -= count; }
            });
            check("SlotThreeSkillsDoNotUseSlotOne", () => { Require(skillManager.slot[0].skillSlot == skillB && skillUi.attackSkills[0] == skillB, "Selected slot3 skills came from slot1."); });
            check("RestorePositionDoesNotAddHeight", () => { player.characterController.enabled = false; player.transform.position += new Vector3(2, 2, 2); player.characterController.enabled = true; load3(); tryPending(); Require(Vector3.Distance(player.transform.position, new Vector3(slot3.xPosition, slot3.yPosition, slot3.zPosition)) < .001f, "Saved XYZ changed (legacy +100 height or absent restore)."); Require(player.characterController.enabled, "Restore left character controller disabled."); });
            check("SavedSceneIsUsed", () => { Write(manager, CharacterSlot.CharacterSlot_03, Data(2, 20, 90, original, skillB)); load3(); Require(manager.GetWorldSceneIndex() == 2, "Load retained default scene instead of saved scene."); Require(!tryPending(), "Applied before saved scene became active."); Write(manager, CharacterSlot.CharacterSlot_03, slot3); });
            check("PendingSaveReturnsBusy", () =>
            {
                Write(manager, CharacterSlot.CharacterSlot_03, Data(2, 20, 90, original, skillB));
                load3();
                string path = Path.Combine(manager.SaveRoot, manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03));
                string primary = File.ReadAllText(path), context = JsonUtility.ToJson(manager.currentCharacterData);
                int health = player.playerNetworkManager.currentHealth.Value;
                Vector3 position = player.transform.position;
                try
                {
                    var result = manager.TrySaveGame();
                    Require(!result.Success && result.Error == Karawara.Core.Persistence.SaveError.Busy, "Pending scene save was not Busy.");
                    Require(File.ReadAllText(path) == primary && JsonUtility.ToJson(manager.currentCharacterData) == context &&
                        player.playerNetworkManager.currentHealth.Value == health && player.transform.position == position,
                        "Pending save changed primary, accepted context or live player.");
                }
                finally { Write(manager, CharacterSlot.CharacterSlot_03, slot3); }
            });
            check("InvalidSceneDoesNotTransition", () => { var invalid = Data(int.MaxValue, 20, 90, original, skillB); Write(manager, CharacterSlot.CharacterSlot_03, invalid); int before = manager.transitions; var data = manager.currentCharacterData; load3(); Require(manager.transitions == before && manager.currentCharacterData == data && !string.IsNullOrEmpty(manager.SaveStatus), "Invalid scene transitioned or replaced accepted context."); Write(manager, CharacterSlot.CharacterSlot_03, slot3); });
            check("RecreatedPlayerRestoresOnce", () =>
            {
                load3(); tryPending();
                player.NetworkObject.Despawn(false); originalDespawned = true;
                replacement = Object.Instantiate(controller.catalog.playerPrefab).GetComponent<PlayerManager>();
                var recreatedSkills = replacement.GetComponent<PlayerSkillManager>();
                if (recreatedSkills == null) recreatedSkills = replacement.gameObject.AddComponent<PlayerSkillManager>();
                recreatedSkills.slot = new[] { new SkillSlot(), new SkillSlot(), new SkillSlot() };
                PlayerSkillManager.instance = recreatedSkills;
                int applyCount = 0;
                Unity.Netcode.NetworkVariable<int>.OnValueChangedDelegate count = (oldHealth, health) => { if (health == 90) applyCount++; };
                replacement.playerNetworkManager.currentHealth.OnValueChanged += count;
                replacement.NetworkObject.SpawnAsPlayerObject(controller.Network.LocalClientId);
                tryPending();
                Require(replacement.IsOwner && replacement.IsServer && replacement.playerNetworkManager.currentHealth.Value == 90 && replacement.playerNetworkManager.vitality.Value == 20, "Real recreated host player did not restore selected save.");
                replacement.playerNetworkManager.currentHealth.Value = 82;
                Require(!tryPending() && replacement.playerNetworkManager.currentHealth.Value == 82 && recreatedSkills.slot[0].skillSlot == skillB && applyCount == 1, "Recreated player restore repeated or selected wrong skills; applyCount=" + applyCount);
                replacement.playerNetworkManager.currentHealth.OnValueChanged -= count;
            });
            report.completed = report.cases.Count == report.expectedCases;
        }
        finally
        {
            Action<Action> cleanup = action => { try { action(); } catch (Exception ex) { report.capturedErrors++; report.errors.Add("Cleanup: " + ex.GetBaseException().Message); } };
            WorldSaveGameManager.DevPlaygroundIsolationActive = true;
            cleanup(() => { if (replacement != null) { if (replacement.IsSpawned) replacement.NetworkObject.Despawn(); else Object.DestroyImmediate(replacement.gameObject); } });
            WorldSaveGameManager.instance = previousManager;
            PlayerSkillManager.instance = skillManager;
            cleanup(() => { if (originalDespawned) player.NetworkObject.SpawnAsPlayerObject(controller.Network.LocalClientId); });
            // GREEN uses the actual apply API; RED retains the original legacy fallback.
            cleanup(() => { if (capturedOriginal)
            {
                var restore = typeof(PlayerManager).GetMethod("ApplyLegacySave", new[] { typeof(CharacterSaveData) });
                if (restore != null) Require((bool)restore.Invoke(player, new object[] { original }), "Original player restore was not ready.");
                else player.LoadGameDataFromCurrentCharacterData(ref original);
            } });
            cleanup(() => {
                bool controllerEnabled = player.characterController.enabled;
                try { player.characterController.enabled = false; if (capturedOriginal) player.transform.position = new Vector3(original.xPosition, original.yPosition, original.zPosition); }
                finally { player.characterController.enabled = controllerEnabled; }
            });
            cleanup(() => { if (capturedOriginal) { skillManager.LoadSkillSlot(original); skillUi.LoadSkillsFromData(original); } });
            SkillTreeUiManager.instance = previousSkillUi;
            cleanup(() => { if (skillA != null) Object.DestroyImmediate(skillA); });
            cleanup(() => { if (skillB != null) Object.DestroyImmediate(skillB); });
            if (previousManager != null) previousManager.player = player;
            TitleScreenManager.Instance = previousTitle;
            WorldSaveGameManager.DevPlaygroundIsolationActive = isolation;
            cleanup(() => EditorBuildSettings.scenes = buildScenes);
            cleanup(() => Object.DestroyImmediate(managerObject));
            Application.logMessageReceived -= capture;
            cleanup(() => { if (Directory.Exists(report.saveRoot)) Directory.Delete(report.saveRoot, true); });
            string output = "Docs/Validation/TOBE/01a-legacy-restore.json";
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        Debug.Log("Legacy Save Restore Regression: " + report.passed + "/" + report.expectedCases + ", errors=" + report.capturedErrors);
    }
    private static CharacterSaveData Data(int scene, int vitality, int health, CharacterSaveData position, Skill skill) => new CharacterSaveData { sceneIndex = scene, vitality = vitality, endurance = 10, currentHealth = health, xPosition = position.xPosition, yPosition = position.yPosition, zPosition = position.zPosition, skillsSlot = new[] { skill }, SkillAttack = new[] { skill } };
    private static void Write(WorldSaveGameManager manager, CharacterSlot slot, CharacterSaveData data) { Require(new SaveFileDataWriter { saveDataDirectoryPath = manager.SaveRoot, saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot) }.TrySave(data).Success, "QA save write failed."); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
