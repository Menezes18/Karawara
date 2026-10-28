using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RPGKarawara;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HudPrefabActivationValidation
{
    [Serializable] sealed class Result { public string name, error; public bool passed; }
    [Serializable] sealed class Report { public string utc; public bool completed, passed, cleanedUp; public List<Result> cases = new List<Result>(); }
    [MenuItem("Karawara/Validation/TOBE/Validate HUD Prefab Activation")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("EditMode only.");
        var original = SceneManager.GetActiveScene();
        if (original.name != "SceneMenu") throw new InvalidOperationException("Open SceneMenu first.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Data/Prefabs/Boss Health Bar.prefab");
        if (asset == null) throw new InvalidOperationException("Missing authored boss bar asset.");
        bool assetState = asset.activeSelf;
        string assetPath = Path.GetFullPath("Assets/Data/Prefabs/Boss Health Bar.prefab");
        byte[] before = File.ReadAllBytes(assetPath);
        var report = new Report { utc = DateTime.UtcNow.ToString("O") };
        Scene owned = default;
        GameObject root = null, bar = null;
        try
        {
            owned = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            root = new GameObject("Owned inactive HUD activation fixture"); root.SetActive(false);
            var hud = root.AddComponent<PlayerUIHudManager>();
            bar = new GameObject("Owned boss bar instance");
            var configure = typeof(PlayerUIHudManager).GetMethod("ConfigureUIBasedOnScene", BindingFlags.Instance | BindingFlags.NonPublic);
            hud.bossHealthBarObject = asset;
            // Exercise the configuration that would change the current asset value.
            SceneManager.SetActiveScene(assetState ? original : owned);
            Check(report, "HUD leaves referenced prefab asset activation unchanged", () => {
                configure.Invoke(hud, null);
                Require(asset.activeSelf == assetState, "HUD changed the shared prefab asset activation.");
            });
            asset.SetActive(assetState);
            hud.bossHealthBarObject = bar;
            SceneManager.SetActiveScene(original);
            Check(report, "Menu hides actual scene instance", () => { bar.SetActive(true); configure.Invoke(hud, null); Require(!bar.activeSelf, "Scene instance remained visible in Menu."); });
            SceneManager.SetActiveScene(owned);
            Check(report, "Gameplay shows actual scene instance", () => { configure.Invoke(hud, null); Require(bar.activeSelf, "Scene instance remained hidden in gameplay."); });
            report.completed = true;
        }
        finally
        {
            asset.SetActive(assetState);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (bar != null) UnityEngine.Object.DestroyImmediate(bar);
            SceneManager.SetActiveScene(original);
            if (owned.IsValid()) EditorSceneManager.CloseScene(owned, true);
            report.cleanedUp = SceneManager.GetActiveScene() == original && Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(assetPath));
            report.passed = report.completed && report.cleanedUp && report.cases.TrueForAll(c => c.passed);
            string path = "Docs/Validation/TOBE/Execution/hud-prefab-activation-" + Guid.NewGuid().ToString("N") + ".json";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("HUD prefab activation fixture: " + path + " passed=" + report.passed);
        }
    }
    static void Check(Report report, string name, Action test)
    {
        var result = new Result { name = name };
        try { test(); result.passed = true; } catch (Exception error) { result.error = error.ToString(); }
        report.cases.Add(result);
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
