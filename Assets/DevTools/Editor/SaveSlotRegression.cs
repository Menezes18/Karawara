using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RPGKarawara;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class SaveSlotRegressionManager : WorldSaveGameManager
{
    public int sceneTransitions;
    protected override void RequestWorldSceneLoad() { sceneTransitions++; }
}

public static class SaveSlotRegression
{
    [Serializable] private sealed class Case { public string name; public bool passed; public string detail; }
    [Serializable] private sealed class Report { public bool completed; public int expectedCases; public int passed; public int capturedErrors; public string saveRoot; public List<string> errors = new List<string>(); public List<Case> cases = new List<Case>(); }

    [MenuItem("Karawara/Validation/TOBE/Run Save Slot Regression")]
    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "Karawara-SaveSlots-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var report = new Report { saveRoot = root, expectedCases = 11 };
        var oldManager = WorldSaveGameManager.instance;
        var oldTitle = TitleScreenManager.Instance;
        bool oldIsolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
        float oldTimeScale = Time.timeScale;
        var objects = new List<GameObject>();
        Application.LogCallback capture = (message, stack, type) => { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) { report.capturedErrors++; report.errors.Add(message + "\n" + stack); } };
        Application.logMessageReceived += capture;
        try
        {
            WorldSaveGameManager.instance = null;
            TitleScreenManager.Instance = null;
            WorldSaveGameManager.DevPlaygroundIsolationActive = false;
            var managerObject = new GameObject("SaveSlotRegression manager"); objects.Add(managerObject);
            var manager = managerObject.AddComponent<SaveSlotRegressionManager>();
            manager.SaveRootOverride = root;
            WorldSaveGameManager.instance = manager;
            var titleObject = new GameObject("SaveSlotRegression title"); objects.Add(titleObject);
            var title = titleObject.AddComponent<TitleScreenManager>();
            TitleScreenManager.Instance = title;
            var slots = new List<UI_Character_Save_Slot>();
            foreach (CharacterSlot slot in new[] { CharacterSlot.CharacterSlot_01, CharacterSlot.CharacterSlot_02, CharacterSlot.CharacterSlot_03 })
            {
                var go = new GameObject("SaveSlotRegression slot " + slot); objects.Add(go); go.SetActive(false);
                var labelGo = new GameObject("Name", typeof(RectTransform)); objects.Add(labelGo);
                var component = go.AddComponent<UI_Character_Save_Slot>(); component.characterSlot = slot;
                component.characterName = labelGo.AddComponent<TextMeshProUGUI>();
                slots.Add(component);
            }
            MethodInfo onEnable = typeof(UI_Character_Save_Slot).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            if (onEnable == null) throw new MissingMethodException(typeof(UI_Character_Save_Slot).FullName, "OnEnable");
            Action<UI_Character_Save_Slot> enable = slot =>
            {
                slot.gameObject.SetActive(false); slot.gameObject.SetActive(true);
                // EditMode validates the exact known callback body without Unity native lifecycle dispatch.
                if (!Application.isPlaying) onEnable.Invoke(slot, null);
            };
            Action<string, Action> check = (name, action) => { var c = new Case { name = name }; report.cases.Add(c); try { action(); c.passed = true; report.passed++; } catch (Exception ex) { c.detail = ex.Message; } };
            Action<bool, string> require = (condition, message) => { if (!condition) throw new InvalidOperationException(message); };
            var writer = new SaveFileDataWriter { saveDataDirectoryPath = root };
            check("empty slots enable without transition", () => { foreach (var slot in slots) enable(slot); require(manager.sceneTransitions == 0, "OnEnable initiated a scene transition"); });
            foreach (var slot in slots) { writer.saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot.characterSlot); writer.TrySave(new CharacterSaveData { characterName = "QA " + slot.characterSlot }); }
            manager.characterSlot01 = new CharacterSaveData { characterName = "QA CharacterSlot_01" };
            manager.characterSlot02 = new CharacterSaveData { characterName = "QA CharacterSlot_02" };
            manager.characterSlot03 = new CharacterSaveData { characterName = "QA CharacterSlot_03" };
            check("valid profiles enable without transition or selection", () => { manager.currentCharacterSlotBeingUsed = CharacterSlot.NO_SLOT; foreach (var slot in slots) enable(slot); require(manager.sceneTransitions == 0 && manager.currentCharacterSlotBeingUsed == CharacterSlot.NO_SLOT, "Displaying profiles changed gameplay/selection"); });
            check("valid names render for all slots", () => { foreach (var slot in slots) require(slot.characterName.text == "QA " + slot.characterSlot, "Missing profile presentation: " + slot.characterSlot); });
            check("slot2 selection respects legacy load binding", () => { slots[1].SelectCurrentSlot(); manager.SlotTeste(); require(title.currentSelectedSlot == CharacterSlot.CharacterSlot_02 && manager.currentCharacterSlotBeingUsed == CharacterSlot.NO_SLOT, "Selection changed active gameplay context"); manager.LoadGame(); require(manager.sceneTransitions == 1 && manager.currentCharacterSlotBeingUsed == CharacterSlot.CharacterSlot_02 && manager.currentCharacterData.characterName == "QA CharacterSlot_02", "Selected slot2 did not load"); });
            writer.saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03);
            File.WriteAllText(Path.Combine(root, writer.saveFileName), "broken");
            check("corrupt profile enable is safe", () => { int before = manager.sceneTransitions; enable(slots[2]); require(manager.sceneTransitions == before, "Corrupt display transitioned"); require(!slots[2].gameObject.activeSelf || slots[2].characterName.text != "QA CharacterSlot_03", "Stale corrupt name shown"); });
            check("corrupt explicit load remains menu", () => { int before = manager.sceneTransitions; var activeSlot = manager.currentCharacterSlotBeingUsed; var activeData = manager.currentCharacterData; slots[2].SelectCurrentSlot(); manager.SlotTeste(); manager.LoadGame(); require(manager.sceneTransitions == before && manager.currentCharacterSlotBeingUsed == activeSlot && manager.currentCharacterData == activeData, "Corrupt load transitioned or changed active context"); });
            check("overwrite refresh shows new name", () => { writer.saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_02); require(writer.TrySave(new CharacterSaveData { characterName = "QA replacement" }).Success, "Overwrite failed"); enable(slots[1]); require(slots[1].characterName.text == "QA replacement", "Refresh retained stale name"); });
            check("delete refresh hides removed profile", () => { manager.DeleteGame(CharacterSlot.CharacterSlot_02); enable(slots[1]); require(!slots[1].gameObject.activeSelf || slots[1].characterName.text != "QA replacement", "Deleted profile remained visible"); });
            check("backup recovery is presented", () =>
            {
                writer.saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_03);
                require(writer.TrySave(new CharacterSaveData { characterName = "QA backup" }).Success, "Backup seed failed");
                require(writer.TrySave(new CharacterSaveData { characterName = "QA primary" }).Success, "Primary seed failed");
                File.WriteAllText(Path.Combine(root, writer.saveFileName), "broken");
                var timeGo = new GameObject("Recovery status", typeof(RectTransform)); objects.Add(timeGo);
                slots[2].timedPlayed = timeGo.AddComponent<TextMeshProUGUI>();
                enable(slots[2]);
                require(slots[2].characterName.text == "QA backup" && slots[2].timedPlayed.text == "Recuperado do backup", "Recovery provenance not presented");
            });
            check("IO load failure remains menu", () =>
            {
                string path = Path.Combine(root, manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_01));
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    int before = manager.sceneTransitions;
                    slots[0].SelectCurrentSlot(); manager.SlotTeste();
                    require(manager.TryLoadSelectedSlot(CharacterSlot.CharacterSlot_01).Error == Karawara.Core.Persistence.SaveError.IoFailure, "Expected locked file IO failure");
                    manager.LoadGame();
                    require(manager.sceneTransitions == before && !string.IsNullOrEmpty(manager.SaveStatus), "IO failure transitioned or omitted status");
                }
            });
            check("IO delete failure retains profile and file", () =>
            {
                string path = Path.Combine(root, manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(CharacterSlot.CharacterSlot_01));
                manager.RefreshCharacterProfiles();
                var profile = manager.characterSlot01;
                using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var deletion = manager.TryDeleteGame(CharacterSlot.CharacterSlot_01);
                    require(!deletion.Success && deletion.Error == Karawara.Core.Persistence.SaveError.IoFailure, "Expected locked file deletion failure");
                    require(File.Exists(path) && manager.characterSlot01 == profile, "Failed deletion replaced profile/file");
                }
            });
            report.completed = report.cases.Count == report.expectedCases;
        }
        finally
        {
            foreach (var go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            WorldSaveGameManager.instance = oldManager; TitleScreenManager.Instance = oldTitle; WorldSaveGameManager.DevPlaygroundIsolationActive = oldIsolation; Time.timeScale = oldTimeScale;
            Application.logMessageReceived -= capture;
            Directory.Delete(root, true);
            string output = Path.Combine("Docs", "Validation", "TOBE", "01a-save-slots.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        Debug.Log("Save Slot Regression: " + report.passed + "/" + report.expectedCases + ", errors=" + report.capturedErrors);
    }
}
