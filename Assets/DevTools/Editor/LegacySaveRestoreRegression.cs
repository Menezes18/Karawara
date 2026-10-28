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
        RPGKarawara.DevTools.LocalSessionRegression.RunPlayMode();
    }
    private static CharacterSaveData Data(int scene, int vitality, int health, CharacterSaveData position, Skill skill) => new CharacterSaveData { sceneIndex = scene, vitality = vitality, endurance = 10, currentHealth = health, xPosition = position.xPosition, yPosition = position.yPosition, zPosition = position.zPosition, skillsSlot = new[] { skill }, SkillAttack = new[] { skill } };
    private static void Write(WorldSaveGameManager manager, CharacterSlot slot, CharacterSaveData data) { Require(new SaveFileDataWriter { saveDataDirectoryPath = manager.SaveRoot, saveFileName = manager.DecideCharacterFileNameBasedOnCharacterSlotBeingUsed(slot) }.TrySave(data).Success, "QA save write failed."); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
