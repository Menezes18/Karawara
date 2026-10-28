using System;
using System.Collections.Generic;
using System.IO;
using RPGKarawara;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

public static class PopUpTutorialRegression
{
    [Serializable] sealed class Case { public string name, detail; public bool passed; }
    [Serializable] sealed class Report
    {
        public string utc;
        public bool completed, passed, cleanedUp;
        public List<Case> cases = new List<Case>();
    }
    [MenuItem("Karawara/Validation/TOBE/Validate Popup Tutorial Regression")]
    public static void Run()
    {
        var report = new Report { utc = DateTime.UtcNow.ToString("O") };
        Require(!EditorApplication.isPlaying, "Run the isolated popup regression in EditMode.");
        float timeScale = Time.timeScale;
        bool cursorVisible = Cursor.visible;
        var cursorLock = Cursor.lockState;
        foreach (bool withVideo in new[] { false, true })
        {
            var result = new Case { name = withVideo ? "Close retains assigned-video cleanup" : "Close succeeds without optional VideoPlayer" };
            report.cases.Add(result);
            GameObject root = null;
            RenderTexture target = null;
            try
            {
                root = new GameObject("QA popup isolated fixture");
                var popup = root.AddComponent<PopUpTutorial>();
                var ui = new GameObject("Tutorial UI", typeof(RectTransform));
                ui.transform.SetParent(root.transform);
                popup.UI = ui;
                var textObject = new GameObject("Text", typeof(RectTransform));
                textObject.transform.SetParent(ui.transform);
                var text = textObject.AddComponent<TextMeshProUGUI>();
                text.text = "Visible authored tutorial";
                var imageObject = new GameObject("Image", typeof(RectTransform));
                imageObject.transform.SetParent(ui.transform);
                var image = imageObject.AddComponent<RawImage>();
                image.texture = Texture2D.whiteTexture;
                if (withVideo)
                {
                    popup.videoPlayer = root.AddComponent<VideoPlayer>();
                    target = new RenderTexture(16, 16, 0);
                    popup.videoPlayer.targetTexture = target;
                    string[] videos = AssetDatabase.FindAssets("t:VideoClip");
                    if (videos.Length > 0)
                        popup.videoPlayer.clip = AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(videos[0]));
                }
                Time.timeScale = 0;
                popup.close(); // Actual production command, no reflection or replacement implementation.
                Require(!ui.activeSelf && popup.passou, "Close must hide UI and mark the tutorial completed.");
                Require(Time.timeScale == 1 && Cursor.lockState == CursorLockMode.Locked, "Close must resume gameplay and restore its existing cursor lock.");
                Require(text.text == "" && image.texture == null, "Close must clear existing tutorial content.");
                if (withVideo) Require(popup.videoPlayer.clip == null && popup.videoPlayer.targetTexture == null, "Assigned video resources must still clear.");
                result.passed = true;
                result.detail = "Actual production close completed and all UI/time/cursor/video postconditions held.";
            }
            catch (Exception ex) { result.detail = ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message; }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (target != null) Object.DestroyImmediate(target);
                Time.timeScale = timeScale; Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
            }
        }
        var openResult = new Case { name = "Player opens text tutorial without optional VideoPlayer" };
        report.cases.Add(openResult);
        GameObject openingRoot = null, player = null;
        tutorialInfo info = null;
        try
        {
            openingRoot = new GameObject("QA text tutorial trigger");
            var trigger = openingRoot.AddComponent<BoxCollider>();
            var popup = openingRoot.AddComponent<PopUpTutorial>();
            var ui = new GameObject("Tutorial UI", typeof(RectTransform));
            ui.transform.SetParent(openingRoot.transform);
            popup.UI = ui;
            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(ui.transform);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            var imageObject = new GameObject("Image", typeof(RectTransform));
            imageObject.transform.SetParent(ui.transform);
            imageObject.AddComponent<RawImage>();
            var buttonObject = new GameObject("Continue", typeof(RectTransform));
            buttonObject.transform.SetParent(ui.transform);
            var button = buttonObject.AddComponent<Button>();
            info = ScriptableObject.CreateInstance<tutorialInfo>();
            info.textoExplicativo = "Use WASD to move";
            popup.info = info;
            player = new GameObject("QA player trigger");
            player.tag = "Player";
            var collider = player.AddComponent<BoxCollider>();
            ui.SetActive(false);
            Time.timeScale = 1;
            popup.OnTriggerEnter(collider); // Actual production opening command.
            Require(ui.activeSelf && Time.timeScale == 0 && !trigger.enabled, "Text tutorial must open, pause gameplay and consume its trigger.");
            Require(text.text == info.textoExplicativo, "Missing video must retain authored tutorial text.");
            button.onClick.Invoke();
            Require(!ui.activeSelf && popup.passou && Time.timeScale == 1, "Actual registered Continue listener must close the text tutorial.");
            openResult.passed = true;
            openResult.detail = "Actual Player trigger opened text UI and registered Continue closed it without video.";
        }
        catch (Exception ex) { openResult.detail = ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message; }
        finally
        {
            if (openingRoot != null) Object.DestroyImmediate(openingRoot);
            if (player != null) Object.DestroyImmediate(player);
            if (info != null) Object.DestroyImmediate(info);
            Time.timeScale = timeScale; Cursor.visible = cursorVisible; Cursor.lockState = cursorLock;
        }
        report.completed = true;
        report.cleanedUp = Time.timeScale == timeScale && Cursor.visible == cursorVisible && Cursor.lockState == cursorLock;
        report.passed = report.cleanedUp && report.cases.TrueForAll(c => c.passed);
        string path = "Docs/Validation/TOBE/Execution/popup-tutorial-regression-" + Guid.NewGuid().ToString("N") + ".json";
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
        Debug.Log("Popup tutorial regression: passed=" + report.passed + ", report=" + path);
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
