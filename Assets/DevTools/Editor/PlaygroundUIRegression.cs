using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class PlaygroundUIRegression
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string ReportPath = "Docs/HomolUIRegression.json";
        [Serializable] public class Case
        {
            public string name, details;
            public bool passed, skipped;
            public List<string> errors = new List<string>();
        }
        [Serializable] public class Report
        {
            public string startedUtc, finishedUtc, scene;
            public bool completed, passed;
            public string boundary = "Runs inside an already ready DevPlayground. Checks the actual HUD and spawns one production network boss. No physical keyboard input, production scene transition, or production asset save is performed.";
            public List<Case> cases = new List<Case>();
        }
        sealed class Step { public string name; public Action begin; public Func<string> verify; public double wait; }
        sealed class Skip : Exception { public Skip(string message) : base(message) { } }
        static readonly Queue<Step> steps = new Queue<Step>();
        static readonly List<Object> fixtures = new List<Object>();
        static DevPlaygroundController controller;
        static Report report;
        static Step current;
        static Case result;
        static double deadline;
        static bool running, oldGod, oldCursor;
        static CursorLockMode oldLock;
        static int oldSelection, oldCount;
        static CharacterSaveData oldSave;
        static string saveFingerprint;
        static AIBossCharacterManager boss;
        static UI_Boss_HP_Bar bossBar;
        [MenuItem("Karawara/Validation/Run Playground UI Regression")]
        public static void Run()
        {
            if (running) { Debug.LogWarning("Playground UI regression already running."); return; }
            controller = Object.FindAnyObjectByType<DevPlaygroundController>();
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "DevPlayground" || controller == null || !controller.Ready)
            { Debug.LogError("UI regression requires an already running, ready DevPlayground. It does not change Play Mode or scenes."); return; }
            if (controller.SpawnedEnemyCount != 0 || !WorldSaveGameManager.DevPlaygroundIsolationActive || Time.timeScale <= 0)
            { Debug.LogError("UI regression requires no tracked enemies, advancing game time, and active save isolation."); return; }
            oldGod = controller.Player.characterNetworkManager.isInvulnerable.Value;
            oldCursor = Cursor.visible; oldLock = Cursor.lockState;
            oldSelection = (int)Field(controller, "enemyIndex"); oldCount = (int)Field(controller, "spawnCount");
            oldSave = WorldSaveGameManager.instance.currentCharacterData;
            saveFingerprint = SaveFingerprint();
            fixtures.Clear(); steps.Clear(); current = null;
            report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), scene = SceneManager.GetActiveScene().path };
            controller.SetGodMode(true);
            Add("Production HUD canvas and quick slots visible", null, () =>
            {
                var hud = PlayerUIManager.instance.playerUIHudManager;
                var canvas = hud.GetComponentInParent<Canvas>();
                Require(canvas != null && canvas.enabled && canvas.gameObject.activeInHierarchy, "HUD canvas is inactive or missing.");
                Require(canvas.transform.lossyScale.sqrMagnitude > .001f, "HUD root retains zero scale.");
                var health = (UI_StatBar)Field(hud, "healthBar");
                Require(health != null && health.gameObject.activeInHierarchy, "Production player health bar is hidden.");
                foreach (string name in new[] { "rightWeaponQuickSlotIcon", "leftWeaponQuickSlotIcon" })
                {
                    var icon = (Image)Field(hud, name);
                    Require(icon != null && icon.gameObject.activeInHierarchy, "Quick-slot hierarchy hidden: " + name);
                }
                return "Active production HUD canvas, nonzero scale, health bar and both quick-slot hierarchies.";
            }, .1);
            Add("Network boss owns exactly one health bar", () =>
            {
                int index = Array.FindIndex(controller.catalog.enemies, e => e.prefab != null && e.prefab.GetComponent<AIBossCharacterManager>() != null);
                if (index < 0) throw new Skip("Catalog contains no AIBossCharacterManager enemy; boss bar regression not run.");
                WorldSaveGameManager.instance.currentCharacterData = new CharacterSaveData();
                controller.SetEnemySelection(index, 1); controller.SpawnEnemies();
            }, () =>
            {
                if (controller.SpawnedEnemyCount == 0) throw new Skip("No network boss fixture spawned; boss cleanup not run.");
                boss = Object.FindObjectsByType<AIBossCharacterManager>(FindObjectsInactive.Exclude).Single(b => b.IsSpawned);
                var bars = Object.FindObjectsByType<UI_Boss_HP_Bar>(FindObjectsInactive.Include).Where(b => Field(b, "bossCharacter") as AIBossCharacterManager == boss).ToArray();
                Require(bars.Length == 1, "Spawned boss should own exactly one bar, got " + bars.Length);
                bossBar = bars[0];
                boss.characterNetworkManager.currentHealth.Value = 0;
                return "Real network boss spawned and owned one UI bar; lethal health change sent through production callback.";
            }, 1.5);
            Add("Boss death removes its health bar", null, () =>
            {
                if (boss == null) throw new Skip("Network boss fixture unavailable; death cleanup not run.");
                Require(boss.isDead.Value && bossBar == null, "Defeated boss retained its health bar after its configured delay.");
                controller.ClearEnemies();
                return "Boss death callback completed and delayed bar was destroyed; spawned fixture despawned.";
            }, 3.2);
            Add("Cleanup preserves saves and sandbox state", Cleanup, () =>
            {
                Require(fixtures.All(o => o == null), "Disposable fixture objects were retained.");
                Require(SaveFingerprint() == saveFingerprint, "Persistent character save files changed during UI regression.");
                Require(SceneManager.GetActiveScene().path == report.scene && WorldSaveGameManager.DevPlaygroundIsolationActive, "Scene or save isolation changed.");
                return "Fixtures removed; original save context, enemy selection, god mode and cursor restored; save files/hash/timestamps and active scene unchanged.";
            }, .3);
            running = true; Application.logMessageReceived += OnLog; EditorApplication.update += Tick;
            Flush();
        }

        static void Add(string name, Action begin, Func<string> verify, double wait)
            => steps.Enqueue(new Step { name = name, begin = begin, verify = verify, wait = wait });
        static void Tick()
        {
            if (!running) return;
            if (!EditorApplication.isPlaying || controller == null) { Finish(false); return; }
            if (current == null)
            {
                if (steps.Count == 0) { Finish(true); return; }
                current = steps.Dequeue(); result = new Case { name = current.name, passed = true }; report.cases.Add(result);
                try { current.begin?.Invoke(); }
                catch (Skip e) { result.skipped = true; result.details = e.Message; }
                catch (Exception e) { result.passed = false; result.errors.Add(e.ToString()); }
                deadline = EditorApplication.timeSinceStartup + current.wait;
            }
            if (EditorApplication.timeSinceStartup < deadline) return;
            if (!result.skipped && result.passed)
                try { result.details = current.verify?.Invoke(); }
                catch (Skip e) { result.skipped = true; result.details = e.Message; }
                catch (Exception e) { result.passed = false; result.errors.Add(e.ToString()); }
            result.passed &= result.errors.Count == 0;
            Flush(); current = null;
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (result != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            { result.errors.Add(message + "\n" + stack); result.passed = false; }
        }
        static void Finish(bool completed)
        {
            EditorApplication.update -= Tick;
            try { Cleanup(); }
            catch (Exception e) { report.cases.Add(new Case { name = "Final cleanup", passed = false, errors = new List<string> { e.ToString() } }); }
            Application.logMessageReceived -= OnLog;
            running = false; report.completed = completed; report.passed = completed && report.cases.All(c => c.passed && !c.skipped);
            report.finishedUtc = DateTime.UtcNow.ToString("O"); Flush();
            Debug.Log("UI regression completed=" + completed + ", passed=" + report.passed + "; " + ReportPath);
        }
        static void Cleanup()
        {
            if (controller != null && controller.Ready)
            {
                controller.ClearEnemies(); controller.SetEnemySelection(oldSelection, oldCount); controller.SetGodMode(oldGod);
            }
            foreach (var item in fixtures)
                if (item != null) { if (item is GameObject root) root.SetActive(false); Object.Destroy(item); }
            if (WorldSaveGameManager.instance != null) WorldSaveGameManager.instance.currentCharacterData = oldSave;
            Cursor.visible = oldCursor; Cursor.lockState = oldLock;
        }
        static void Flush() { Directory.CreateDirectory("Docs"); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true)); }
        static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string SaveFingerprint()
        {
            if (!Directory.Exists(Application.persistentDataPath)) return "";
            using (var hash = SHA256.Create())
                return string.Join("\n", Directory.GetFiles(Application.persistentDataPath, "characterSlot*").OrderBy(p => p)
                    .Select(p => p + "|" + File.GetLastWriteTimeUtc(p).Ticks + "|" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(p)))));
        }
    }
}
