#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools
{
    public static class InputRoutingRegression
    {
        [Serializable] private class Result { public string name, error; public bool passed; }
        [Serializable] private class Report { public List<Result> cases = new List<Result>(); public string boundary = "Production components in Editor PlayMode with isolated QA player; not standalone acceptance."; }
        [MenuItem("Karawara/Validation/TOBE/Run Input Routing Regression")]
        public static void Run()
        {
            if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SceneMenu")
                throw new InvalidOperationException("Requires PlayMode at production SceneMenu.");
            var bootstrap = GameBootstrap.EnsureInstance(); var session = bootstrap.Session;
            if (session.HasActiveSession || session.IsBusy) throw new InvalidOperationException("Requires idle menu.");
            var report = new Report();
            try
            {
                var player = session.SpawnQaPlayer(bootstrap.PlayerPrefab, Vector3.zero, Quaternion.identity);
                var input = PlayerInputManager.instance;
                void Check(string name, Action action)
                {
                    var result = new Result { name = name }; report.cases.Add(result);
                    try { action(); result.passed = true; } catch (Exception e) { result.error = e.GetBaseException().Message; }
                }
                object Gate() => input.GetType().GetProperty("Gate")?.GetValue(input) ?? throw new Exception("Missing production input gate");
                IDisposable Lock(string reason)
                {
                    var gate = Gate(); var acquire = gate.GetType().GetMethod("Acquire");
                    return (IDisposable)acquire.Invoke(gate, new[] { Enum.Parse(acquire.GetParameters()[0].ParameterType, reason) });
                }
                bool Blocked() => (bool)Gate().GetType().GetProperty("IsBlocked").GetValue(Gate());
                Check("TwoLocksRequireTwoReleases", () =>
                {
                    using (Lock("Dialogue"))
                    {
                        using (Lock("Pause")) Require(!input.playerControls.PlayerActions.enabled, "Actions enabled during overlap");
                        Require(Blocked() && !input.playerControls.PlayerMovement.enabled, "Releasing pause released dialogue");
                    }
                    Require(!Blocked() && input.playerControls.PlayerMovement.enabled, "Final release failed");
                });
                Check("PauseBackStillRoutesWhenGameplayBlocked", () =>
                {
                    using (Lock("Dialogue")) Require(input.playerControls.UI.enabled && !input.playerControls.PlayerActions.enabled, "UI must remain enabled independently");
                });
                Check("SkillHotkeyDoesNotBypassDialogue", () =>
                {
                    var router = input.GetType().GetProperty("Router")?.GetValue(input) ?? throw new Exception("Missing router");
                    using (Lock("Dialogue")) Require(!(bool)router.GetType().GetProperty("CanRouteGameplay").GetValue(router), "Skills bypass dialogue");
                });
                Check("LegacyEnableCannotReleaseDialogue", () =>
                {
                    using (Lock("Dialogue")) { input.DisableInput(); input.EnableInput(); Require(Blocked() && !input.playerControls.PlayerActions.enabled, "Legacy enable released unrelated owner"); }
                });
                Check("BlockClearsHeldAndQueuedCommands", () =>
                {
                    input.vertical_Input = 1; input.horizontal_Input = 1; input.moveAmount = 1; input.cameraVertical_Input = 1;
                    using (Lock("Dialogue")) Require(input.vertical_Input == 0 && input.moveAmount == 0 && input.cameraVertical_Input == 0, "Held input survived block");
                });
                Check("UnbindStopsCommands", () =>
                {
                    input.BindSessionPlayer(null);
                    Require(!input.playerControls.asset.enabled, "Unbound input actions remain enabled");
                    input.BindSessionPlayer(player); input.SetSessionInputEnabled(true);
                });
                Check("DialogueSurvivesPlayerReenable", () =>
                {
                    using (Lock("Dialogue"))
                    {
                        player.gameObject.SetActive(false); player.gameObject.SetActive(true);
                        Require(Blocked() && !input.playerControls.PlayerActions.enabled, "Temporary player disable discarded dialogue owner");
                    }
                });
                Check("ReviveAfterPauseRestoresActions", () =>
                {
                    player.State.SetHealth(0);
                    using (Lock("Pause")) { }
                    player.ReviveCharacter();
                    Require(input.playerControls.PlayerActions.enabled, "Revive left gameplay maps disabled");
                });
                Check("DisabledSessionRouterCannotRoute", () =>
                {
                    input.SetSessionInputEnabled(false);
                    var router = input.GetType().GetProperty("Router")?.GetValue(input) ?? throw new Exception("Missing router");
                    Require(!(bool)router.GetType().GetProperty("CanRouteGameplay").GetValue(router), "Router ignores session disabled state");
                    input.SetSessionInputEnabled(true);
                });
            }
            finally { session.DisposeSession(); }
            Directory.CreateDirectory("Docs/Validation/TOBE/Execution");
            var path = "Docs/Validation/TOBE/Execution/g3a-input-routing-" + Guid.NewGuid().ToString("N") + ".json";
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("Input Routing Regression: " + path);
        }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
#endif
