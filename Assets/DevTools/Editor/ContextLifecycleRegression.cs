#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Karawara.Core.Characters;
using Karawara.Core.Session;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RPGKarawara.DevTools
{
    public static class ContextLifecycleRegression
    {
        [Serializable] private class Case { public string name, error; public bool passed; }
        [Serializable] private class Report { public List<Case> cases = new List<Case>(); public string boundary = "Production contexts/presenters in Editor PlayMode, temporary saves; not standalone acceptance."; }
        [MenuItem("Karawara/Validation/TOBE/Run Context Lifecycle Regression")]
        public static async void Run()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != "SceneMenu") throw new InvalidOperationException("Requires idle PlayMode SceneMenu.");
            var bootstrap = GameBootstrap.EnsureInstance();
            if (bootstrap.Session.IsBusy || bootstrap.Session.HasActiveSession) throw new InvalidOperationException("Requires idle menu session.");
            var report = new Report();
            async Task Check(string name, Func<Task> action)
            {
                var result = new Case { name = name }; report.cases.Add(result);
                try { await action(); result.passed = true; } catch (Exception e) { result.error = e.GetBaseException().Message; }
            }
            await Check("HudRebindShowsCurrentState", () => { HudRebindShowsCurrentState(); return Task.CompletedTask; });
            await Check("TenEnableCyclesHaveOneSubscription", () => { TenEnableCyclesHaveOneSubscription(bootstrap); return Task.CompletedTask; });
            await Check("SceneUnloadReleasesSceneReferences", SceneUnloadReleasesSceneReferences);
            await Check("ReturnToMenuRemovesPlayerContext", () => ReturnToMenuRemovesPlayerContext(bootstrap));
            await Check("LoadFailureLeavesRecoverableMenu", () => LoadFailureLeavesRecoverableMenu(bootstrap));
            var output = Path.GetFullPath("Docs/Validation/TOBE/Execution/g3a-context-lifecycle-latest.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("Context lifecycle regression: " + report.cases.Count(x => x.passed) + "/" + report.cases.Count + "; " + output);
        }
        public static void HudRebindShowsCurrentState()
        {
            var owner = new GameObject("G3A HUD regression", typeof(RectTransform));
            try
            {
                var slider = owner.AddComponent<Slider>(); var bar = owner.AddComponent<UI_StatBar>();
                typeof(UI_StatBar).GetField("scaleBarLengthWithStats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bar, false);
                var hud = owner.AddComponent<PlayerUIHudManager>();
                typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, bar);
                var presenter = Activator.CreateInstance(Contract("RPGKarawara.HudPresenter"), new object[] { hud });
                var oldState = new CharacterRuntimeState(); oldState.SetMaxHealth(500); oldState.SetHealth(321);
                var next = new CharacterRuntimeState(); next.SetMaxHealth(650); next.SetHealth(217);
                Invoke(presenter, "Bind", oldState); Require(slider.maxValue == 500 && slider.value == 321, "Initial binding did not render current health.");
                Invoke(presenter, "Bind", next); Require(slider.maxValue == 650 && slider.value == 217, "Rebind did not render replacement state immediately.");
                oldState.SetHealth(12); Require(slider.value == 217, "Detached state still updates replacement HUD.");
                Invoke(presenter, "Unbind"); next.SetHealth(11); Require(slider.value == 217, "Unbound presenter still updates HUD.");
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
        private static void TenEnableCyclesHaveOneSubscription(GameBootstrap bootstrap)
        {
            var player = bootstrap.Session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
            try
            {
                var context = Property(player, "Context"); Require(context != null, "Session has no player context.");
                for (int i = 0; i < 10; i++) { player.gameObject.SetActive(false); player.gameObject.SetActive(true); }
                var subscribers = (Delegate)typeof(CharacterRuntimeState).GetField("HealthChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player.State);
                int count = subscribers?.GetInvocationList().Count(d => d.Target?.GetType().FullName == "RPGKarawara.HudPresenter") ?? 0;
                Require(count == 1, "Expected one active HUD health subscription after ten enable cycles, found " + count);
                player.State.SetHealth(171);
                var hud = PlayerUIManager.instance.playerUIHudManager;
                var bar = (UI_StatBar)typeof(PlayerUIHudManager).GetField("healthBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
                Require(bar.GetComponent<Slider>().value == 171, "Current HUD did not receive health after enable cycles.");
            }
            finally { bootstrap.Session.DisposeSession(); }
        }
        private static async Task SceneUnloadReleasesSceneReferences()
        {
            var scene = SceneManager.CreateScene("G3A scene release");
            var context = SceneContext.GetOrCreate(scene); context.TryPrepare();
            var operation = SceneManager.UnloadSceneAsync(scene); while (operation != null && !operation.isDone) await Task.Yield();
            // Managed properties remain readable after Unity destroys the native component.
            Require(!context.IsReady && context.Input == null && context.UI == null, "Unloaded scene context retained input/UI references.");
        }
        private static async Task ReturnToMenuRemovesPlayerContext(GameBootstrap bootstrap)
        {
            var session = bootstrap.Session; var player = session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
            session.Configure(bootstrap, new ImmediateTransition(SessionResult.Success), "SceneMenu");
            try
            {
                var context = Property(player, "Context"); Require(context != null, "Session did not create a player context.");
                Require(await session.ReturnToMenuAsync() == SessionResult.Success, "Return command failed.");
                Require(!session.HasActiveSession && (bool)Property(context, "IsDisposed"), "Menu return retained active player context.");
                Require(PlayerInputManager.instance.player == null && WorldSaveGameManager.instance.player == null, "Menu return retained input/save player.");
            }
            finally { session.DisposeSession(); session.Configure(bootstrap, new SceneTransitionService(), "Assets/Scenes/SceneMenu.unity"); }
        }
        private static async Task LoadFailureLeavesRecoverableMenu(GameBootstrap bootstrap)
        {
            var session = bootstrap.Session; var saves = WorldSaveGameManager.instance;
            var root = saves.SaveRootOverride;
            saves.SaveRootOverride = Path.Combine(Path.GetTempPath(), "Karawara-G3A-context-" + Guid.NewGuid().ToString("N"));
            session.Configure(bootstrap, new ImmediateTransition(SessionResult.Failed), "SceneMenu");
            try
            {
                Require(await session.StartNewGameAsync(1) == SessionResult.Failed, "Injected scene failure did not fail load.");
                Require(!session.HasActiveSession && !session.IsBusy && !string.IsNullOrEmpty(session.LastError), "Failed load has no recoverable error or retained player.");
                Require(!PlayerInputManager.instance.SessionInputEnabled, "Failed load enabled gameplay input.");
                session.Configure(bootstrap, new ImmediateTransition(SessionResult.Success), "SceneMenu");
                Require(await session.ReturnToMenuAsync() == SessionResult.Success && session.LastError == null, "Failed load cannot recover through return command.");
            }
            finally { session.DisposeSession(); saves.SaveRootOverride = root; session.Configure(bootstrap, new SceneTransitionService(), "Assets/Scenes/SceneMenu.unity"); }
        }
        private sealed class ImmediateTransition : ISceneTransitionService
        {
            private readonly SessionResult result;
            public ImmediateTransition(SessionResult result) { this.result = result; }
            public Task<SessionResult> LoadAsync(string path, CancellationToken token) => Task.FromResult(result);
        }
        private static Type Contract(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("Missing contract " + name);
        private static object Property(object target, string name) => target.GetType().GetProperty(name)?.GetValue(target) ?? throw new InvalidOperationException("Missing binding " + target.GetType().Name + "." + name);
        private static void Invoke(object target, string name, params object[] args) => (target.GetType().GetMethod(name) ?? throw new InvalidOperationException("Missing lifecycle " + name)).Invoke(target, args);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
