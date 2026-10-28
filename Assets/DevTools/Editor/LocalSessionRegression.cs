#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Karawara.Core.Session;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace RPGKarawara.DevTools
{
    public static class LocalSessionRegression
    {
        [Serializable] private sealed class Case { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report { public List<Case> cases = new List<Case>(); public string saveRoot; public string boundary = "Editor PlayMode, production session and scene transitions, isolated temporary saves; standalone gameplay remains a separate gate."; }
        private sealed class ControlledTransition : ISceneTransitionService
        {
            public readonly TaskCompletionSource<SessionResult> Completion = new TaskCompletionSource<SessionResult>();
            public Task<SessionResult> LoadAsync(string scenePath, CancellationToken token) => Completion.Task;
        }
        [MenuItem("Karawara/Validation/TOBE/Run Local Session Regression")]
        public static void Run()
        {
            var assembly = typeof(TitleScreenManager).Assembly;
            foreach (string name in new[] { "GameBootstrap", "GameSessionController", "SceneContext", "SceneTransitionService" })
                Require(assembly.GetType("RPGKarawara." + name) != null, "Local session missing: " + name);
            Debug.Log("Local Session Regression: contracts passed. Run Local Session PlayMode Regression for the eight behavioral cases; these contract checks are not gameplay evidence.");
        }
        [MenuItem("Karawara/Validation/TOBE/Run Local Session PlayMode Regression")]
        public static async void RunPlayMode()
        {
            if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "SceneMenu")
                throw new InvalidOperationException("Requires PlayMode at the production menu; this runner loads game scenes using temporary saves.");
            var bootstrap = GameBootstrap.EnsureInstance();
            var session = bootstrap.Session;
            var saves = WorldSaveGameManager.instance;
            Require(saves != null && bootstrap.PlayerPrefab != null && !session.IsBusy, "Bootstrap/save service/prefab must be configured and idle.");
            var report = new Report { saveRoot = Path.Combine(Path.GetTempPath(), "Karawara-SessionRegression-" + Guid.NewGuid().ToString("N")) };
            string previousRoot = saves.SaveRootOverride;
            bool previousIsolation = WorldSaveGameManager.DevPlaygroundIsolationActive;
            float previousTime = Time.timeScale;
            saves.SaveRootOverride = report.saveRoot;
            WorldSaveGameManager.DevPlaygroundIsolationActive = false;
            async Task Check(string name, Func<Task> action)
            {
                var result = new Case { name = name }; report.cases.Add(result);
                try { await action(); result.passed = true; }
                catch (Exception exception) { result.detail = exception.GetBaseException().Message; }
            }
            try
            {
                await Check("MenuCreatesNoHost", () =>
                {
                    Require(!session.HasActiveSession, "Menu created a player.");
                    Require(!UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x != null && x.GetType().FullName == "Unity.Netcode.NetworkManager"), "Networking manager remains in menu.");
                    return Task.CompletedTask;
                });
                await Check("NewGameCreatesExactlyOnePlayer", async () =>
                {
                    Require(await session.StartNewGameAsync(1) == SessionResult.Success, session.LastError);
                    Require(session.Player != null && session.Player.IsInitialized && UnityEngine.Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Length == 1, "Session must own exactly one initialized player.");
                });
                await Check("LoadAppliesOnceAfterReady", async () =>
                {
                    Require(await session.LoadGameAsync(1) == SessionResult.Success, session.LastError);
                    Require(SceneContext.GetOrCreate(SceneManager.GetActiveScene()).IsReady, "Context was not ready.");
                    int health = session.Player.State.Health;
                    session.Player.State.SetHealth(Math.Max(1, health - 1));
                    Require(!saves.ApplyPendingSaveWhenReady() && session.Player.State.Health == Math.Max(1, health - 1), "Save reapplied over gameplay health.");
                });
                await Check("ReturnToMenuDisposesSession", async () =>
                {
                    Require(await session.ReturnToMenuAsync() == SessionResult.Success, session.LastError);
                    Require(!session.HasActiveSession && saves.player == null && PlayerInputManager.instance.player == null, "Menu retained session references.");
                });
                await Check("DuplicateTransitionReturnsBusy", async () =>
                {
                    var controlled = new ControlledTransition();
                    session.Configure(bootstrap, controlled, "Assets/Scenes/SceneMenu.unity");
                    try
                    {
                        Task<SessionResult> first = session.ReturnToMenuAsync();
                        Require(await session.ReturnToMenuAsync() == SessionResult.Busy, "Concurrent command did not return Busy.");
                        controlled.Completion.SetResult(SessionResult.Success);
                        Require(await first == SessionResult.Success, "Original transition did not complete.");
                    }
                    finally { controlled.Completion.TrySetResult(SessionResult.Failed); session.Configure(bootstrap, new SceneTransitionService(), "Assets/Scenes/SceneMenu.unity"); }
                });
                await Check("PausedLoadCompletes", async () =>
                {
                    Time.timeScale = 0;
                    var result = await session.LoadGameAsync(1);
                    Require(result == SessionResult.Success || (result == SessionResult.Failed && !string.IsNullOrEmpty(session.LastError)), "Paused loading did not complete/recover.");
                });
                await Check("CancelledLoadReleasesInput", async () =>
                {
                    Task<SessionResult> command = session.LoadGameAsync(1);
                    session.CancelTransition();
                    Require(await command == SessionResult.Failed && !session.IsBusy && PlayerInputManager.instance.player == null, "Cancellation left a player/input/transition binding.");
                });
                await Check("DirectQaSceneUsesIsolatedSession", () =>
                {
                    string previous = saves.SaveRoot;
                    session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
                    Require(session.IsQaSession && saves.SaveRoot != Application.persistentDataPath && !saves.TrySaveGame().Success, "Direct QA session may write personal saves.");
                    Require(saves.currentCharacterData != null && saves.currentCharacterData.sitesOfGrace != null, "Direct QA lacks in-memory world progress.");
                    session.DisposeSession();
                    Require(saves.SaveRoot == previous, "QA disposal did not restore the previous save root.");
                    return Task.CompletedTask;
                });
            }
            finally
            {
                if (!session.IsBusy) await session.ReturnToMenuAsync();
                saves.SaveRootOverride = previousRoot;
                WorldSaveGameManager.DevPlaygroundIsolationActive = previousIsolation;
                Time.timeScale = previousTime;
                Directory.CreateDirectory("Docs/Validation/TOBE/Execution");
                File.WriteAllText("Docs/Validation/TOBE/Execution/g2-local-session-regression.json", JsonUtility.ToJson(report, true));
            }
            Debug.Log("Local session behavioral cases: " + report.cases.Count(x => x.passed) + "/8. QA saves retained at " + report.saveRoot);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message ?? "Local session assertion failed."); }
    }
}
#endif

