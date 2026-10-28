using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RPGKarawara;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class UIProximityRegression
{
    [Serializable] sealed class Case { public string name, detail; public bool passed; }
    [Serializable] sealed class Report { public string utc, error; public bool completed, passed, cleanedUp, originalSetupPreserved; public List<Case> cases = new List<Case>(); }
    [MenuItem("Karawara/Validation/TOBE/Validate UI Proximity Regression")]
    public static void Run()
    {
        var report = new Report { utc = DateTime.UtcNow.ToString("O") };
        string originalSetup = Setup();
        var originalScene = SceneManager.GetActiveScene();
        Scene scene = default;
        try
        {
            Require(!EditorApplication.isPlaying, "Isolated proximity fixture requires EditMode.");
            Require(GameObject.FindGameObjectsWithTag("Player").Length == 0, "Existing active tagged players would contaminate the missing-player test; no existing objects are disabled.");
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            Test(report, "Missing player at Start hides prompt without exception", () => WithPrompt((proximity, ui) =>
            { Invoke(proximity, "Start"); Require(!ui.activeSelf, "Missing player must leave prompt hidden."); Invoke(proximity, "Update"); Require(!ui.activeSelf, "Missing-player Update must remain safe and hidden."); }));
            Test(report, "Destroyed bound player hides prompt without exception", () => WithPrompt((proximity, ui) =>
            {
                var player = Player(Vector3.zero);
                try { Invoke(proximity, "Start"); Invoke(proximity, "Update"); Require(ui.activeSelf, "Nearby live player must first show prompt."); Object.DestroyImmediate(player); player = null; Invoke(proximity, "Update"); Require(!ui.activeSelf, "Destroyed player must hide prompt."); }
                finally { if (player != null) Object.DestroyImmediate(player); }
            }));
            Test(report, "Valid near/far player preserves radius and text", () => WithPrompt((proximity, ui) =>
            {
                var player = Player(Vector3.zero);
                try { Invoke(proximity, "Start"); Invoke(proximity, "Update"); Require(ui.activeSelf && ui.GetComponent<TextMeshProUGUI>().text == proximity.textGUI, "Near prompt/text must be preserved."); player.transform.position = Vector3.right * 10; Invoke(proximity, "Update"); Require(!ui.activeSelf, "Far prompt must hide."); player.transform.position = Vector3.right * proximity.detectionRadius; Invoke(proximity, "Update"); Require(ui.activeSelf, "Exact existing radius boundary remains inclusive."); }
                finally { Object.DestroyImmediate(player); }
            }));
            Test(report, "Late tagged player binds after missing Start", () => WithPrompt((proximity, ui) =>
            {
                Invoke(proximity, "Start"); var player = Player(Vector3.zero);
                try { Invoke(proximity, "Update"); Require(ui.activeSelf, "Player appearing after Start must be discoverable."); }
                finally { Object.DestroyImmediate(player); }
            }));
            Test(report, "Recreated tagged player replaces destroyed binding", () => WithPrompt((proximity, ui) =>
            {
                var player = Player(Vector3.zero);
                try { Invoke(proximity, "Start"); Invoke(proximity, "Update"); Object.DestroyImmediate(player); player = Player(Vector3.zero); Invoke(proximity, "Update"); Require(ui.activeSelf, "New live player must replace stale destroyed binding."); player.transform.position = Vector3.right * 10; Invoke(proximity, "Update"); Require(!ui.activeSelf, "Prompt must track recreated player position."); }
                finally { if (player != null) Object.DestroyImmediate(player); }
            }));
            report.completed = true;
        }
        catch (Exception ex) { report.error = ex.GetBaseException().Message; }
        finally
        {
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            report.cleanedUp = !scene.IsValid() || !scene.isLoaded;
            report.originalSetupPreserved = Setup() == originalSetup;
            report.passed = report.completed && string.IsNullOrEmpty(report.error) && report.cleanedUp && report.originalSetupPreserved && report.cases.All(c => c.passed);
            string path = "Docs/Validation/TOBE/Execution/ui-proximity-regression-" + Guid.NewGuid().ToString("N") + ".json";
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Debug.Log("UI proximity regression: passed=" + report.passed + ", report=" + path);
        }
    }
    static void WithPrompt(Action<UIProximity, GameObject> action)
    {
        var root = new GameObject("QA UI proximity");
        try
        {
            var proximity = root.AddComponent<UIProximity>();
            var ui = new GameObject("QA proximity text", typeof(RectTransform), typeof(TextMeshProUGUI));
            ui.transform.SetParent(root.transform); proximity.uiElement = ui; proximity.textGUI = "QA interact";
            action(proximity, ui);
        }
        finally { Object.DestroyImmediate(root); }
    }
    static GameObject Player(Vector3 position) { var player = new GameObject("QA tagged player"); player.tag = "Player"; player.transform.position = position; return player; }
    static void Invoke(UIProximity proximity, string method) => typeof(UIProximity).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(proximity, null);
    static void Test(Report report, string name, Action action) { var result = new Case { name = name }; report.cases.Add(result); try { action(); result.passed = true; result.detail = "Actual production lifecycle method and prompt postconditions passed."; } catch (Exception ex) { result.detail = ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message; } }
    static string Setup() => string.Join("|", EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive));
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
