using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceRouteRehearsalValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate Performance Route Rehearsal")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bounded contract fixture requires EditMode.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            test("Explicit API exists without scene bootstrap", () => Require(Harness().GetMethod("Begin") != null && Harness().GetMethod("End") != null, "Begin/End missing"));
            test("Immutable schedule accepts explicit release tail", () => Schedule(Timeline(), 1d, .05d));
            test("Authored heading turn bound accepts30deg/s", () => Schedule(Timeline().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"cameraMode\":\"movement-heading-v1\",\"cameraTurnDegreesPerSecond\":30"), 1d, .05d));
            test("Authored heading turn bound rejects values outside10..90deg/s", () => Reject(() => Schedule(Timeline().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"cameraMode\":\"movement-heading-v1\",\"cameraTurnDegreesPerSecond\":91"), 1d, .05d)));
            test("Authored heading turn bound requires heading mode", () => Reject(() => Schedule(Timeline().Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"cameraTurnDegreesPerSecond\":30"), 1d, .05d)));
            test("Duration cannot end before release", () => Reject(() => Schedule(Timeline(), .05d, .01d)));
            test("Nonfinite and unbounded lateness rejected", () => { Reject(() => Schedule(Timeline(), 1d, double.NaN)); Reject(() => Schedule(Timeline(), 1d, 2d)); });
            test("Multiple due packets rejected before collapsed press release", () => Reject(() => Dispatch(new[] { 0d, .1d }, 0, .2d, .3d)));
            test("Late packet rejected before queue", () => Reject(() => Dispatch(new[] { 0d, .1d }, 1, .2d, .05d)));
            test("Single on-time packet dispatches", () => Require(Dispatch(new[] { 0d, .1d }, 1, .11d, .05d) == 1, "Expected exactly one due packet"));
            test("Constructed acquired-before-start scope disposes actual replay devices and restores controls", PartialCleanup);
            test("Reject output/save paths that overlap personal root or each other", () =>
            {
                string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Karawara-route-root-fixture"));
                string personal = Path.Combine(root, "personal"), saves = Path.Combine(root, "saves"), output = Path.Combine(root, "output");
                Roots(personal, saves, output);
                Reject(() => Roots(personal, Path.Combine(personal, "nested"), output));
                Reject(() => Roots(personal, root, output));
                Reject(() => Roots(personal, saves, Path.Combine(saves, "nested")));
                Reject(() => Roots(personal, Path.Combine(output, "nested"), output));
                Reject(() => Roots(personal, saves, personal));
            });
            test("Require actual world scene and health UI readiness", () =>
            {
                Readiness(true, true); Reject(() => Readiness(false, true)); Reject(() => Readiness(true, false));
            });
            test("Repeated ended-owner disposal preserves replacement registry ownership", RepeatedOwnerDisposal);
            test("Report serializes Unity poses as finite scalar components", () =>
            {
                var result = new RPGKarawara.DevTools.Performance.PerformanceRouteRehearsal.Result {
                    frames = new[] { new RPGKarawara.DevTools.Performance.PerformanceRouteRehearsal.Frame {
                        playerPosition = new Vector3(1, 2, 3), playerRotation = Quaternion.identity,
                        movementAction = new Vector2(.25f, -.5f),
                        cameras = new[] { new RPGKarawara.DevTools.Performance.PerformanceRouteRehearsal.CameraPose { position = new Vector3(4, 5, 6), rotation = Quaternion.identity } }
                    } },
                    cameras = new[] { new RPGKarawara.DevTools.Performance.PerformanceRouteRehearsal.CameraContext { viewport = new Rect(0, 0, 1, 1) } }
                };
                var serialize = Harness().GetMethod("SerializeResult", BindingFlags.Static | BindingFlags.NonPublic);
                string json = serialize == null ? JsonConvert.SerializeObject(result) : (string)serialize.Invoke(null, new object[] { result });
                var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);
                Require((float)parsed["frames"][0]["playerPosition"]["z"] == 3 && (float)parsed["frames"][0]["movementAction"]["y"] == -.5f, "Position/input components lost");
                Require((float)parsed["frames"][0]["cameras"][0]["rotation"]["w"] == 1 && (float)parsed["cameras"][0]["viewport"]["width"] == 1, "Camera pose/viewport lost");
                Require(!json.Contains("normalized") && !json.Contains("eulerAngles"), "Derived Unity properties must not enter report");
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-route-rehearsal-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), readyForTiming = false, cases, boundary = "Pure schedule/API contract only; no host, route, timing or population acceptance" };
            using (var stream = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Route rehearsal fixture: " + path);
        }
        static Type Harness() => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceRouteRehearsal")).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("Route rehearsal implementation missing");
        static string Timeline() => "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"W\"]},{\"timeSeconds\":0.1,\"keys\":[]}]}";
        static void Schedule(string timeline, double duration, double late) => Harness().GetMethod("ValidateSchedule").Invoke(null, new object[] { timeline, duration, late });
        static int Dispatch(double[] times, int next, double elapsed, double late) => (int)Harness().GetMethod("DuePacketCount").Invoke(null, new object[] { times, next, elapsed, late });
        static void Roots(string personal, string saves, string output)
        {
            var method = Harness().GetMethod("ValidateRoots", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Require(method != null, "Disjoint root guard missing"); method.Invoke(null, new object[] { personal, saves, output });
        }
        static void Readiness(bool sceneMatchesWorld, bool healthUiReady)
        {
            var method = Harness().GetMethod("ValidateWorldReadiness", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Require(method != null, "World scene/HUD readiness guard missing"); method.Invoke(null, new object[] { sceneMatchesWorld, healthUiReady });
        }
        static void PartialCleanup()
        {
            var controls = new PlayerControls();
            var baseline = InputSystem.devices.ToArray();
            var actions = controls.asset.ToArray();
            var originalStates = actions.Select(a => a.enabled).ToArray();
            var mask = controls.asset.bindingMask;
            var originalDevices = controls.asset.devices;
            var deviceCopy = originalDevices.HasValue ? originalDevices.Value.ToArray() : null;
            var owned = new GameObject("Owned partial route fixture"); owned.SetActive(false);
            IDisposable replay = null;
            Component session = null;
            try
            {
                var replayType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceInputReplay")).FirstOrDefault(t => t != null);
                Require(replayType != null, "Actual replay missing");
                replay = (IDisposable)replayType.GetMethod("Create").Invoke(null, new object[] { new[] { "-karawaraPerformanceReplay" }, controls, Timeline() });
                Require(InputSystem.devices.Count == baseline.Length + 2, "Must acquire actual replay keyboard/mouse first");
                session = owned.AddComponent(Harness());
                var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                Action<string, object> set = (name, value) => { var field = Harness().GetField(name, fields); Require(field != null, "Missing partial owner field " + name); field.SetValue(session, value); };
                set("replay", replay); set("controls", controls); set("actions", actions); set("originalActionEnabled", originalStates);
                set("originalDeviceRestriction", originalDevices.HasValue); set("originalAssetDevices", deviceCopy); set("originalAllDevices", baseline); set("originalMask", mask);
                Require(!(bool)Harness().GetProperty("IsRunning").GetValue(session), "Constructed state must precede clock/start");
                ((IDisposable)session).Dispose();
                var restored = controls.asset.devices;
                Require(InputSystem.devices.SequenceEqual(baseline) && controls.asset.bindingMask.Equals(mask) && controls.asset.Select(a => a.enabled).SequenceEqual(originalStates) &&
                    restored.HasValue == originalDevices.HasValue && (!restored.HasValue || restored.Value.SequenceEqual(deviceCopy)), "Partial Begin leaked owned devices/actions or failed original state restoration");
                Require(Harness().GetProperty("LastResult").GetValue(session) == null, "Partial initialization must not fabricate a completed session/report");
                ((IDisposable)session).Dispose();
            }
            finally
            {
                replay?.Dispose(); UnityEngine.Object.DestroyImmediate(owned);
                controls.Disable(); UnityEngine.Object.DestroyImmediate(controls.asset);
            }
        }
        static void RepeatedOwnerDisposal()
        {
            var owned = new GameObject("Owned inactive registry fixture"); owned.SetActive(false);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var owners = (HashSet<PlayerManager>)Harness().GetField("owners", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            PlayerManager actor = null;
            try
            {
                actor = owned.AddComponent<PlayerManager>();
                var oldSession = owned.AddComponent(Harness());
                Harness().GetField("player", flags).SetValue(oldSession, actor);
                // Both implementations share the real registry; the corrected implementation owns an explicit latch.
                var latch = Harness().GetField("ownerRegistered", flags); latch?.SetValue(oldSession, true);
                Require(owners.Add(actor), "Owned unique player must register once");
                ((IDisposable)oldSession).Dispose();
                Require(!owners.Contains(actor), "First disposal must release its own registration");
                Require(owners.Add(actor), "Replacement registration must acquire the released actor");
                ((IDisposable)oldSession).Dispose();
                Require(owners.Contains(actor), "Old disposed owner removed the replacement registration");
            }
            finally { if (actor != null) owners.Remove(actor); UnityEngine.Object.DestroyImmediate(owned); }
        }
        static void Reject(Action body) { try { body(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException || e.InnerException is InvalidOperationException) { return; } throw new InvalidOperationException("Expected rejection"); }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
