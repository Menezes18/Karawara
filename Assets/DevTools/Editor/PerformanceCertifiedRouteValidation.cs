using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCertifiedRouteValidation
    {
        const string TypeName = "RPGKarawara.DevTools.Performance.PerformanceCertifiedRouteProcess";
        const string Build = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        const string PlayerGuid = "11111111111111111111111111111111";
        static readonly string Distribution = new string('D', 64);
        [MenuItem("Karawara/Validation/TOBE/Validate Certified Route Manifest")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Manifest fixture requires EditMode");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            test("Frozen manifest parsed without any observed success", () =>
            {
                var sample = Sample(); var parsed = Parse(sample, Build);
                var dto = JObject.FromObject(parsed);
                Require(dto["accepted"] == null && dto["populationVerified"] == null && dto["comparisonValid"] == null, "Manifest alone granted observed acceptance");
                var snapshot = parsed.GetType().GetProperty("Snapshot"); Require(snapshot != null, "Detached snapshot accessor missing");
                var first = snapshot.GetValue(parsed); var groupField = first.GetType().GetField("initialComposition");
                var groups = (Array)groupField.GetValue(first); groups.GetValue(0).GetType().GetField("count").SetValue(groups.GetValue(0), 999);
                var second = (Array)groupField.GetValue(snapshot.GetValue(parsed));
                Require((int)second.GetValue(0).GetType().GetField("count").GetValue(second.GetValue(0)) != 999, "Returned manifest aliases frozen composition");
            });
            foreach (string mutation in new[] { "missing-field", "wrong-build", "wrong-player-same-dll", "wrong-distribution-same-dll", "timeline-hash", "seed-hash", "unknown-property", "source-revision", "duplicate-counter", "pause-policy", "duplicate-tutorial", "schema", "scene", "route", "warmup", "duration", "settings", "camera-mode", "camera-rate", "missing-camera", "duplicate-camera", "missing-npc", "choice", "creation-count", "window-order", "window-end", "missing-state", "unsupported-type", "composition-delta" })
                test("Reject manifest " + mutation, () =>
                {
                    var sample = Sample(); var data = (JObject)sample["manifest"];
                    switch (mutation)
                    {
                        case "missing-field": data.Remove("timelineSha256"); break;
                        case "wrong-build": data["buildAssemblySha256"] = new string('B', 64); break;
                        case "wrong-player-same-dll": data["playerBuildGuid"] = new string('2', 32); break;
                        case "wrong-distribution-same-dll": data["distributionSha256"] = new string('E', 64); break;
                        case "pause-policy": data["pausePolicy"] = "allow-any"; break;
                        case "duplicate-tutorial": data["allowedTutorialPaths"] = new JArray("fixture/popup", "fixture/popup"); break;
                        case "timeline-hash": data["timelineSha256"] = new string('B', 64); break;
                        case "seed-hash": data["seedSha256"] = new string('B', 64); break;
                        case "unknown-property": data["accepted"] = true; break;
                        case "source-revision": data["sourceRevision"] = "unidentified"; break;
                        case "duplicate-counter": data["counterNames"] = new JArray("fixture-counter", "fixture-counter"); break;
                        case "schema": data["schemaVersion"] = 2; break;
                        case "scene": data["scene"] = "SceneMenu"; break;
                        case "route": data["routeId"] = "video1-exploration-discovery-v1"; break;
                        case "warmup": data["warmupSeconds"] = 0; break;
                        case "duration": data["captureSeconds"] = 7; break;
                        case "settings": data["renderScale"] = .5; break;
                        case "camera-mode": data["cameraMode"] = "static"; break;
                        case "camera-rate": data["cameraTurnDegreesPerSecond"] = 90; break;
                        case "missing-camera": data["cameraPaths"] = new JArray(); break;
                        case "duplicate-camera": ((JArray)data["cameraPaths"]).Add(data["cameraPaths"][0].DeepClone()); break;
                        case "missing-npc": data["missionNpcPath"] = ""; break;
                        case "choice": data["missionChoice"] = 0; break;
                        case "creation-count": data["expectedCreations"] = 14; break;
                        case "window-order": data["transitionEarliestSeconds"] = 95; break;
                        case "window-end": data["transitionLatestSeconds"] = 151; break;
                        case "missing-state": ((JObject)data["initialComposition"][0]).Remove("active"); break;
                        case "unsupported-type": data["initialComposition"][0]["primaryType"] = "Missing.Actor"; break;
                        case "composition-delta": data["settledComposition"][0]["count"] = 99; break;
                    }
                    Rejected(() => Parse(sample, Build));
                });
            test("Duplicate JSON field rejected", () =>
            {
                var sample = Sample(); string json = sample["manifest"].ToString(Formatting.None);
                Rejected(() => Invoke(json.Insert(1, "\"schemaVersion\":1,"), (string)sample["timeline"], (string)sample["seed"], Build));
            });
            test("Actual population proof requires raw full-window evidence", () => Population(PopulationSample(), true));
            test("Timing session rejects missing actual standalone owners", () =>
            {
                var type = Parse(Sample(), Build).GetType().DeclaringType; var method = type.GetMethod("Begin", BindingFlags.Public | BindingFlags.Static);
                Require(method != null, "Actual timing session Begin missing");
                try { method.Invoke(null, new object[5]); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return; }
                throw new InvalidOperationException("Missing actual owners accepted");
            });
            test("Partially initialized session disposal is idempotent", () =>
            {
                var type = Parse(Sample(), Build).GetType().DeclaringType; var session = Activator.CreateInstance(type, true); var method = type.GetMethod("Dispose");
                Require(method != null, "Owned session disposal missing"); method.Invoke(session, null); method.Invoke(session, null);
                Require(!(bool)type.GetProperty("IsRunning").GetValue(session), "Disposed session still running");
                Require((int)type.GetProperty("OwnedRecorderCount").GetValue(session) == 0, "Partial session retained recorders");
            });
            test("Explicit certified process config parsed without runtime acceptance", () => ProcessConfig(true, false));
            test("Discovery mode rejects a certification manifest", () => ProcessConfig(false, true));
            foreach (string scenario in new[] { "valid", "missing", "baseline-mode", "late", "too-long", "negative", "nonfinite" })
                test("Profiler attribution is explicit and bounded: " + scenario, () => ProcessConfig(true, scenario != "valid", scenario));
            test("Attribution evidence cannot become timing-qualified even with a positive capture flag", () =>
            {
                var type = Parse(Sample(), Build).GetType().DeclaringType.GetNestedType("EvidenceReport");
                var evidence = Activator.CreateInstance(type);
                JsonConvert.PopulateObject("{\"capture\":{\"comparisonValid\":true},\"moved\":true,\"jumped\":true,\"cameraMoved\":true,\"finalRelease\":true,\"controlsRestored\":true,\"attribution\":{\"started\":true,\"completed\":true,\"restored\":true}}", evidence);
                var population = type.GetField("population").FieldType;
                type.GetField("population").SetValue(evidence, Activator.CreateInstance(population, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, null));
                Require(!(bool)type.GetProperty("routePopulationQualified").GetValue(evidence), "Attribution must be independently excluded from timing qualification.");
            });
            foreach (string conditions in new[] { "unknown", "changed" })
                test("Attribution rejects unexpected or unobserved conditions: " + conditions, () =>
                {
                    var type = Parse(Sample(), Build).GetType().DeclaringType.GetNestedType("EvidenceReport");
                    var evidence = Activator.CreateInstance(type);
                    JsonConvert.PopulateObject("{\"capture\":{\"fullWindowCompleted\":true},\"moved\":true,\"jumped\":true,\"cameraMoved\":true,\"finalRelease\":true,\"controlsRestored\":true,\"attribution\":{\"started\":true,\"completed\":true,\"restored\":true},\"unexpectedConditionsChanged\":" + (conditions == "changed" ? "true" : "null") + "}", evidence);
                    var population = type.GetField("population").FieldType;
                    type.GetField("population").SetValue(evidence, Activator.CreateInstance(population, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, null));
                    Require(!(bool)type.GetProperty("attributionRouteCompleted").GetValue(evidence), "Unexpected settings changes or absent observation cannot pass diagnostic route validity.");
                });
            for (int setting = -1; setting < 7; setting++)
            {
                int changed = setting;
                test("Profiler owned settings continuity: " + changed, () =>
                {
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceProfilerAttribution")).FirstOrDefault(t => t != null);
                    var method = type?.GetMethod("SettingsMatch"); Require(method != null, "Profiler ownership continuity predicate missing.");
                    object[] args = { changed != 0, changed == 1 ? "foreign.raw" : "owned.raw", "owned.raw", changed != 2, changed != 3, true, changed != 4, changed != 5 };
                    // Setting6 is a changed expected callstack policy rather than an actual value.
                    if (changed == 6) args[5] = false;
                    Require((bool)method.Invoke(null, args) == (changed == -1), "Changed recording owner/configuration accepted.");
                });
            }
            foreach (string scenario in new[] { "missing", "known", "negative" })
                test("Allocation metadata stays explicit: " + scenario, () =>
                {
                    var method = typeof(PerformanceProfilerAttributionReader).GetMethod("ReadAllocationBytes"); Require(method != null, "Allocation availability mapping missing.");
                    try
                    {
                        var value = method.Invoke(null, new object[] { scenario == "missing" ? 0 : 1, scenario == "negative" ? -1L : 32L });
                        Require(scenario != "negative" && (scenario == "missing" ? value == null : (long)value == 32), "Unavailable bytes became zero or invalid bytes passed.");
                    }
                    catch (TargetInvocationException e) when (scenario == "negative" && e.InnerException is ArgumentException) { }
                });
            test("Native Profiler log path normalization preserves owned destination", () =>
            {
                if (UnityEngine.Profiling.Profiler.enabled) throw new InvalidOperationException("Stop Editor profiling before the native path fixture.");
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceProfilerAttribution")).First(t => t != null);
                var method = type.GetMethod("SettingsMatch");
                string prior = UnityEngine.Profiling.Profiler.logFile;
                string path = Path.Combine(Path.GetTempPath(), "Karawara-native-profiler-path-" + Guid.NewGuid().ToString("N") + ".raw");
                try
                {
                    UnityEngine.Profiling.Profiler.logFile = path;
                    Require((bool)method.Invoke(null, new object[] { true, UnityEngine.Profiling.Profiler.logFile, path, true, true, true, true, true }), "Native normalized log destination was mistaken for a foreign owner.");
                }
                finally { UnityEngine.Profiling.Profiler.logFile = prior; }
            });
            foreach (bool allowed in new[] { true, false }) test("Typed authored tutorial close requires frozen permission=" + allowed, () =>
            {
                var root = new GameObject("Certified popup fixture") { hideFlags = HideFlags.HideAndDontSave }; root.SetActive(false); float scale = Time.timeScale;
                var cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
                try
                {
                    var popup = root.AddComponent<RPGKarawara.PopUpTutorial>(); var box = root.AddComponent<BoxCollider>(); box.enabled = false;
                    var ui = new GameObject("UI"); ui.transform.SetParent(root.transform); popup.UI = ui;
                    var text = new GameObject("text", typeof(RectTransform)); text.transform.SetParent(ui.transform); text.AddComponent<TMPro.TextMeshProUGUI>();
                    var graphic = new GameObject("image", typeof(RectTransform)); graphic.transform.SetParent(ui.transform); graphic.AddComponent<UnityEngine.UI.RawImage>();
                    root.SetActive(true); Time.timeScale = 0;
                    var type = Parse(Sample(), Build).GetType().DeclaringType.GetNestedType("PlannedTutorialJournal");
                    Require(type != null, "Typed planned-pause journal missing");
                    Action close = () => { var journal = Activator.CreateInstance(type, new object[] { new[] { popup }, allowed ? new[] { root.name } : new[] { "other/path" } }); type.GetMethod("CloseIfPaused").Invoke(journal, new object[] { 10, 5d }); };
                    if (!allowed)
                    {
                        try { close(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { Require(Time.timeScale == 0 && !popup.passou, "Unknown pause mutated gameplay"); return; }
                        throw new InvalidOperationException("Unknown pause accepted");
                    }
                    close();
                    Require(Time.timeScale == 1 && popup.passou && !ui.activeSelf, "Authored close did not restore gameplay");
                }
                finally { Time.timeScale = scale; Cursor.lockState = cursor; Cursor.visible = cursorVisible; UnityEngine.Object.DestroyImmediate(root); }
            });
            test("Planned tutorial numeric journal is allocation-free and detached", () =>
            {
                var roots = new GameObject[4];
                try
                {
                    var owners = new RPGKarawara.PopUpTutorial[4]; var paths = new string[4];
                    for (int i = 0; i < roots.Length; i++) { roots[i] = new GameObject("Certified journal fixture " + i) { hideFlags = HideFlags.HideAndDontSave }; roots[i].SetActive(false); owners[i] = roots[i].AddComponent<RPGKarawara.PopUpTutorial>(); paths[i] = roots[i].name; }
                    var type = Parse(Sample(), Build).GetType().DeclaringType.GetNestedType("PlannedTutorialJournal"); Require(type != null, "Preallocated tutorial journal missing");
                    var warm = Activator.CreateInstance(type, new object[] { owners, paths }); var journal = Activator.CreateInstance(type, new object[] { owners, paths });
                    var method = type.GetMethod("Record"); Require(method != null, "Numeric journal append missing");
                    var warmRecord = (Action<int, int, double>)Delegate.CreateDelegate(typeof(Action<int, int, double>), warm, method);
                    var record = (Action<int, int, double>)Delegate.CreateDelegate(typeof(Action<int, int, double>), journal, method);
                    warmRecord(0, 1, 1); long before = GC.GetAllocatedBytesForCurrentThread();
                    for (int i = 0; i < 4; i++) record(i, 10 + i, 5 + i);
                    long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                    Require(bytes == 0, "Harness event append allocated " + bytes + " bytes");
                    var capture = type.GetMethod("Capture"); var first = (Array)capture.Invoke(journal, null); Require(first.Length == 4, "Numeric events missing");
                    first.GetValue(0).GetType().GetField("frame").SetValue(first.GetValue(0), 999);
                    var second = (Array)capture.Invoke(journal, null); Require((int)second.GetValue(0).GetType().GetField("frame").GetValue(second.GetValue(0)) == 10, "Exported rows alias event buffer");
                    try { record(0, 20, 20); } catch (ArgumentException) { return; }
                    throw new InvalidOperationException("Duplicate tutorial close event accepted");
                }
                finally { foreach (var root in roots) if (root != null) UnityEngine.Object.DestroyImmediate(root); }
            });
            foreach (string mutation in new[] { "missing-declarations", "wrong-choice", "wrong-owner", "wrong-prefab", "preexisting", "late-declaration", "declaration-issue", "early-generation", "late-generation", "short-window", "fault", "unknown-transient", "wrong-census" })
                test("Bound manifest rejects population " + mutation, () =>
                {
                    var sample = PopulationSample(); var observed = sample["population"]; var declarations = (JArray)sample["declarations"];
                    switch (mutation)
                    {
                        case "missing-declarations": declarations.RemoveAt(0); break;
                        case "wrong-choice": foreach (var row in declarations) row["choiceIndex"] = 1; break;
                        case "wrong-owner": foreach (var row in declarations) row["npcId"] = 999; break;
                        case "wrong-prefab": declarations[0]["prefabId"] = 999; break;
                        case "preexisting": declarations[0]["existingCreatedId"] = 999; break;
                        case "late-declaration": declarations[0]["time"] = 11; break;
                        case "declaration-issue": declarations[0]["issue"] = "ambiguous"; break;
                        case "early-generation": sample["manifest"]["transitionEarliestSeconds"] = 80; break;
                        case "late-generation": sample["manifest"]["transitionLatestSeconds"] = 75.1; break;
                        case "short-window": observed["lifecycle"]["endedTime"] = 159; break;
                        case "fault": observed["lifecycle"]["sourceFaulted"] = true; break;
                        case "unknown-transient": var events = (JArray)observed["lifecycle"]["events"]; events.Add(new JObject { ["componentId"] = 999, ["rootId"] = 999, ["componentType"] = "RPGKarawara.AICharacterManager", ["change"] = 2, ["frame"] = 20, ["time"] = 150 }); observed["lifecycle"]["notifications"] = events.Count; break;
                        case "wrong-census": observed["after"]["actors"][0]["objectId"] = 999; break;
                    }
                    Population(sample, false);
                });
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                boundary = "Manifest identity/shape fixture only; no Player input, runtime reconciliation or timing acceptance" };
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-certified-manifest-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Certified manifest fixture: " + path); if (!report.passed) Debug.LogError("Certified manifest fixture FAILED: " + path);
        }
        static JObject Sample()
        {
            var template = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/video1-heading-authored-skills-template.json"));
            string timeline = (string)template["rehearsal"]["timelineJson"], seed = (string)template["rehearsal"]["seedSaveJson"];
            var groups = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/video1-bounded-semantic-composition-2026-10-20.json"));
            return new JObject { ["timeline"] = timeline, ["seed"] = seed, ["manifest"] = new JObject {
                ["schemaVersion"] = 1, ["routeId"] = "video1-exploration-baseline-v1", ["scene"] = "Video1", ["buildAssemblySha256"] = Build,
                ["sourceRevision"] = new string('1', 40), ["counterNames"] = new JArray("fixture-counter"),
                ["playerBuildGuid"] = PlayerGuid, ["distributionSha256"] = Distribution, ["pausePolicy"] = "authored-immediate-v1", ["allowedTutorialPaths"] = new JArray("fixture/popup"),
                ["timelineSha256"] = Hash(timeline), ["seedSha256"] = Hash(seed), ["warmupSeconds"] = 30, ["captureSeconds"] = 120,
                ["width"] = 1920, ["height"] = 1080, ["quality"] = "High Fidelity", ["vsync"] = 0, ["targetFrameRate"] = -1, ["renderScale"] = 1,
                ["maxLatenessSeconds"] = .25, ["cameraMode"] = "movement-heading-v1", ["cameraTurnDegreesPerSecond"] = 30,
                ["cameraPaths"] = new JArray("fixture/actual-primary-camera", "fixture/actual-minimap-camera"), ["missionNpcPath"] = "fixture/actual-NPC-owner",
                ["missionChoice"] = 2, ["expectedCreations"] = 17, ["transitionEarliestSeconds"] = 75, ["transitionLatestSeconds"] = 90,
                ["initialComposition"] = groups["initial"].DeepClone(), ["settledComposition"] = groups["settled"].DeepClone() } };
        }
        static JObject PopulationSample()
        {
            var sample = Sample(); var method = typeof(PerformanceBoundedRoutePopulationValidation).GetMethod("Sample", BindingFlags.Static | BindingFlags.NonPublic);
            var source = (JObject)method.Invoke(null, new object[] { true, 17 }); var observed = source["observed"];
            observed["lifecycle"]["endedTime"] = 160;
            foreach (var row in observed["aiAfter"]["events"].Where(e => (int)e["token"] > 24)) row["time"] = (double)row["time"] + 70;
            foreach (var row in observed["lifecycle"]["events"]) row["time"] = (double)row["time"] + 70;
            foreach (string endpoint in new[] { "before", "after" })
                ((JArray)observed[endpoint]["actors"]).Add(new JObject { ["objectId"] = 600, ["sceneHandle"] = 1, ["scene"] = "Video1", ["path"] = "fixture/actual-NPC-owner",
                    ["primaryType"] = "RPGKarawara.NPCDialogue", ["role"] = "ambient-npc", ["components"] = new JArray("RPGKarawara.NPCDialogue"), ["active"] = true, ["enabled"] = true, ["supportedBinding"] = true, ["spawned"] = false });
            ((JArray)observed["lifecycle"]["initiallyBoundSubjects"]).Add(new JObject { ["componentId"] = 10600, ["rootId"] = 600, ["ownerRootId"] = 600, ["componentType"] = "RPGKarawara.NPCDialogue" });
            foreach (var composition in new[] { source["expected"]["phase"]["composition"], source["expected"]["resultingComposition"] })
                ((JArray)composition).Add(new JObject { ["role"] = "ambient-npc", ["primaryType"] = "RPGKarawara.NPCDialogue", ["count"] = 1, ["active"] = true, ["enabled"] = true, ["spawned"] = false });
            sample["manifest"]["initialComposition"] = source["expected"]["phase"]["composition"].DeepClone();
            sample["manifest"]["settledComposition"] = source["expected"]["resultingComposition"].DeepClone();
            sample["manifest"]["transitionEarliestSeconds"] = 75; sample["manifest"]["transitionLatestSeconds"] = 90;
            sample["population"] = observed;
            sample["declarations"] = new JArray(source["expected"]["creations"].Select(row => new JObject { ["npcId"] = 10600, ["choiceIndex"] = 2, ["choiceRootId"] = 700, ["missionId"] = 701,
                ["spawnerId"] = row["spawnerId"].DeepClone(), ["prefabId"] = row["prefabId"].DeepClone(), ["primaryType"] = row["primaryType"].DeepClone(), ["existingCreatedId"] = 0, ["frame"] = 9, ["time"] = 9 }));
            return sample;
        }
        static void Population(JObject sample, bool expected)
        {
            var frozen = Parse(sample, Build); var method = frozen.GetType().DeclaringType.GetMethod("ReconcilePopulation", BindingFlags.Static | BindingFlags.Public);
            Require(method != null, "Bound manifest population reconciliation missing"); var args = method.GetParameters();
            var result = JObject.FromObject(method.Invoke(null, new[] { frozen, (object)10d, sample["population"].ToObject(args[2].ParameterType), sample["declarations"].ToObject(args[3].ParameterType) }));
            Require((bool)result["accepted"] == expected, "Unexpected bounded proof: " + result);
            Require((string)result["scope"] == "bounded-typed-actor-population" && result["g1bAccepted"] == null, "Population proof claimed global certification");
        }
        static object Parse(JObject sample, string build) => Invoke(sample["manifest"].ToString(Formatting.None), (string)sample["timeline"], (string)sample["seed"], build);
        static void ProcessConfig(bool certified, bool shouldReject, string attributionScenario = null)
        {
            var sample = Sample(); var config = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/video1-heading-authored-skills-template.json"));
            string root = Path.Combine(Path.GetTempPath(), "KarawaraCertifiedFixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "config.json");
            try
            {
                config["mode"] = certified ? "video1-certified-route" : "video1-route-rehearsal";
                config["certifiedManifestJson"] = sample["manifest"].ToString(Formatting.None);
                config["rehearsal"]["outputRoot"] = Path.Combine(root, "output");
                if (certified) config["rehearsal"]["routeId"] = "video1-exploration-baseline-v1";
                if (attributionScenario != null)
                {
                    config["mode"] = attributionScenario == "baseline-mode" ? "video1-certified-route" : "video1-route-attribution";
                    if (attributionScenario != "missing") config["profilerAttribution"] = new JObject {
                        ["startSeconds"] = attributionScenario == "late" ? 149 : attributionScenario == "negative" ? -1 : attributionScenario == "nonfinite" ? double.NaN : 35,
                        ["durationSeconds"] = attributionScenario == "too-long" ? 120 : 5, ["allocationCallstacks"] = true };
                }
                File.WriteAllText(path, config.ToString(Formatting.None));
                var response = RPGKarawara.DevTools.Performance.PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", Path.Combine(root, "saves") }, Path.Combine(root, "personal"));
                Require(response.success != shouldReject, "Unexpected process parse result: " + JsonConvert.SerializeObject(response.errors));
                Require(response.result == null, "Parsing granted an observed capture result");
            }
            finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(root); }
        }
        static object Invoke(string json, string timeline, string seed, string build)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(TypeName)).FirstOrDefault(t => t != null);
            Require(type != null, "Certified route process missing"); var method = type.GetMethod("ParseManifest", BindingFlags.Public | BindingFlags.Static);
            Require(method != null, "Manifest parser missing"); return method.Invoke(null, new object[] { json, timeline, seed, build, PlayerGuid, Distribution });
        }
        static string Hash(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
        static void Rejected(Func<object> body)
        {
            try { body(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { return; }
            throw new InvalidOperationException("Malformed or unbound manifest accepted");
        }
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    }
}
