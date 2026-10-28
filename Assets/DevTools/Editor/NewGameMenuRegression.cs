using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RPGKarawara;
using RPGKarawara.DevTools;
using Karawara.Core.Characters;
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

// Legacy entry points delegate to the local session PlayMode regression at SceneMenu.
// Historical fixture types remain for reading prior evidence, not for new acceptance.
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
        RPGKarawara.DevTools.LocalSessionRegression.RunPlayMode();
    }

    private static void Check(Report report, string name, Action action)
    {
        var result = new Case { name = name }; report.cases.Add(result);
        try { action(); result.passed = true; report.passed++; }
        catch (Exception ex) { result.detail = ex.GetBaseException().Message; }
    }

    [MenuItem("Karawara/Validation/TOBE/Run Local New Game Menu Regression (Legacy Alias)")]
    public static void RunReadyHost()
    {
        RPGKarawara.DevTools.LocalSessionRegression.RunPlayMode();
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
