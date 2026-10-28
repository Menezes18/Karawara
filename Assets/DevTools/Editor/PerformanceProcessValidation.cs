using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceProcessValidation
    {
        [Serializable] sealed class Case { public string name, detail; public bool passed; }
        [Serializable] sealed class Report { public string utc; public bool completed, passed, cleanedUp, actualSceneMenuBindingVerified; public List<Case> cases = new List<Case>(); public List<string> limits = new List<string>(); }
        [MenuItem("Karawara/Validation/TOBE/Validate Performance Process Harness")]
        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            string root = Path.Combine(Path.GetTempPath(), "Karawara-PerformanceProcess-" + Guid.NewGuid().ToString("N"));
            string save = Path.Combine(root, "saves"), output = Path.Combine(root, "output"), personal = Path.Combine(root, "personal-unused");
            string configPath = Path.Combine(root, "config.json");
            Test(report, "No flag creates no files or QA context", () =>
            {
                var result = InvokeJson("ParseRequest", new[] { "-karawaraSaveRoot", save }, personal);
                Require(!(bool)result["handled"] && !Directory.Exists(root), "Missing performance activation is a no-op.");
            });
            Test(report, "Missing config and root fail before I/O", () =>
            {
                var noValue = InvokeJson("ParseRequest", new[] { "-karawaraPerformanceConfig" }, personal);
                var noRoot = InvokeJson("ParseRequest", new[] { "-karawaraPerformanceConfig", configPath }, personal);
                Require(!(bool)noValue["success"] && !(bool)noRoot["success"] && !Directory.Exists(root), "Invalid activation/root must not create output.");
            });
            foreach (string invalidRoot in new[] { "relative", personal })
                Test(report, "Invalid save root rejected: " + invalidRoot, () =>
                {
                    var result = InvokeJson("ParseRequest", Args(configPath, invalidRoot), personal);
                    Require(!(bool)result["success"] && !Directory.Exists(root), "Invalid save root rejected before config/output I/O.");
                });
            Test(report, "Duplicate activation rejected before I/O", () =>
            {
                var result = InvokeJson("ParseRequest", new[] { "-karawaraPerformanceConfig", configPath, "-karawaraPerformanceConfig", configPath, "-karawaraSaveRoot", save }, personal);
                Require(!(bool)result["success"] && !Directory.Exists(root), "Duplicate activation must fail.");
            });
            var config = Config(output);
            Test(report, "Parse fixed inventory config without creating save/output", () =>
            {
                WriteConfig(configPath, config);
                var result = InvokeJson("ParseRequest", Args(configPath, save), personal);
                Require((bool)result["handled"] && (bool)result["success"] && (string)result["mode"] == "inventory", "Valid inventory request must parse.");
                Require(!Directory.Exists(save) && !Directory.Exists(output), "Parsing must not create save/output.");
            });
            Test(report, "Stationary config accepted without starting capture", () =>
            {
                var stationary = (JObject)config.DeepClone(); stationary["mode"] = "stationary-scene-menu";
                WriteConfig(configPath, stationary);
                var result = InvokeJson("ParseRequest", Args(configPath, save), personal);
                Require((bool)result["success"] && (string)result["mode"] == "stationary-scene-menu" && !Directory.Exists(save) && !Directory.Exists(output), "Stationary parsing performs no scene/host/capture operation.");
            });
            Test(report, "Video seed rejects cross-process Unity instance IDs before deserialization", () =>
            {
                var seed = SafeSkillSeed(); seed["SkillDefense"] = new JArray(new JObject { ["instanceID"] = 19378 });
                bool rejected = false;
                try { Harness().GetMethod("ParseVideoSeed", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { seed.ToString() }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
                Require(rejected, "A frozen QA seed must not deserialize stale Unity object IDs.");
            });
            foreach (string scenario in new[] { "exact", "missing", "ambiguous", "wrong-type" })
                Test(report, "Video seed binds explicit current-process authored skills: " + scenario, () =>
                {
                    var skill = AssetDatabase.LoadAssetAtPath<Skill>("Assets/Scripts/SkillTree/Data/Escudo.asset");
                    Require(skill != null, "Actual authored skill fixture missing.");
                    var seed = SafeSkillSeed();
                    seed["qaSkillBindings"] = new JObject { ["SkillDefense"] = new JArray(new JObject { ["assetName"] = ((UnityEngine.Object)skill).name, ["type"] = scenario == "wrong-type" ? "Wrong.Type" : skill.GetType().FullName }, JValue.CreateNull()) };
                    var clone = scenario == "ambiguous" ? UnityEngine.Object.Instantiate(skill) : null;
                    if (clone != null) ((UnityEngine.Object)clone).name = ((UnityEngine.Object)skill).name;
                    try
                    {
                        var method = Harness().GetMethod("BindVideoSeed", BindingFlags.NonPublic | BindingFlags.Static);
                        Require(method != null, "Current-process seed binding is missing.");
                        Skill[] catalog = scenario == "missing" ? Array.Empty<Skill>() : clone != null ? new[] { skill, clone } : new[] { skill };
                        CharacterSaveData data = null; bool rejected = false;
                        try { data = (CharacterSaveData)method.Invoke(null, new object[] { seed.ToString(), catalog }); }
                        catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
                        if (scenario == "exact") Require(!rejected && data.SkillDefense.Length == 2 && ReferenceEquals(data.SkillDefense[0], skill) && data.SkillDefense[1] == null && data.skillsSlot.Length == 3, "Exact actual reference, array order and empty slots must survive.");
                        else Require(rejected, "Missing, ambiguous or wrong-type assets must reject, never silently remove or substitute skills.");
                    }
                    finally { if (clone != null) UnityEngine.Object.DestroyImmediate(clone); }
                });
            foreach (string scenario in new[] { "valid", "valid-full-route", "missing-seed", "wrong-scene", "dead-seed" })
                Test(report, "Explicit Video1 standalone discovery: " + scenario, () =>
                {
                    bool full = scenario == "valid-full-route";
                    var changed = (JObject)config.DeepClone(); changed["mode"] = full ? "video1-route-rehearsal" : "video1-path-inspection"; changed.Remove("capture");
                    var seed = new JObject { ["sceneIndex"] = scenario == "wrong-scene" ? 1 : 2, ["characterName"] = "Owned Video QA", ["xPosition"] = -320.88, ["yPosition"] = 182.38, ["zPosition"] = 590.61,
                        ["currentHealth"] = scenario == "dead-seed" ? 0 : 225, ["vitality"] = 15, ["endurance"] = 10 };
                    changed["rehearsal"] = new JObject { ["routeId"] = full ? "video1-exploration-discovery-v1" : "video1-path-inspection-v1", ["outputRoot"] = output, ["durationSeconds"] = full ? 150 : 20, ["maxLatenessSeconds"] = .25,
                        ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":0.5,\"keys\":[\"D\"]},{\"timeSeconds\":2,\"keys\":[]}]}",
                        ["seedSaveJson"] = scenario == "missing-seed" ? null : seed.ToString(Formatting.None) };
                    WriteConfig(configPath, changed);
                    var parsed = InvokeJson("ParseRequest", Args(configPath, save), personal);
                    Require((bool)parsed["success"] == (scenario == "valid" || full), "Video1 requires an explicit healthy scene2 seed.");
                    Require(!Directory.Exists(save) && !Directory.Exists(output), "Video1 parsing must perform no save/output writes.");
                });
            foreach (string scenario in new[] { "valid", "no-release", "overlap", "occupied-save", "competing-bootstrap" })
                Test(report, "Explicit standalone tutorial rehearsal: " + scenario, () =>
                {
                    var changed = (JObject)config.DeepClone(); changed["mode"] = "tutorial-rehearsal"; changed.Remove("capture");
                    changed["rehearsal"] = new JObject {
                        ["routeId"] = "tutorial-controls-functional-v1", ["outputRoot"] = scenario == "overlap" ? save : output,
                        ["durationSeconds"] = 7, ["maxLatenessSeconds"] = .075,
                        ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":0.5,\"keys\":[\"W\"]},{\"timeSeconds\":1.5,\"keys\":" + (scenario == "no-release" ? "[\"W\"]" : "[]") + "}]}"
                    };
                    WriteConfig(configPath, changed);
                    if (scenario == "occupied-save") { Directory.CreateDirectory(save); File.WriteAllText(Path.Combine(save, "owned-marker"), "preserve"); }
                    try
                    {
                        var args = Args(configPath, save);
                        if (scenario == "competing-bootstrap") args = args.Concat(new[] { "-karawaraHostSaveRegression", "write-three" }).ToArray();
                        var result = InvokeJson("ParseRequest", args, personal);
                        Require((bool)result["success"] == (scenario == "valid"), "Rehearsal parsing must enforce activation, timeline and fresh isolated roots");
                        Require(!Directory.Exists(output), "Parsing must never start host or create output");
                    }
                    finally { if (scenario == "occupied-save") Directory.Delete(save, true); }
                });
            foreach (string scenario in new[] { "valid", "empty", "position-length", "quaternion-length", "zero-quaternion", "negative-tolerance", "late", "unordered", "nonfinite", "too-many" })
                Test(report, "Standalone observed pose checkpoints: " + scenario, () =>
                {
                    var changed = (JObject)config.DeepClone(); changed["mode"] = "tutorial-path-inspection"; changed.Remove("capture");
                    var checkpoint = new JObject { ["elapsed"] = 1, ["position"] = new JArray(1, 2, 3), ["cameraRotation"] = new JArray(0, 0, 0, 1),
                        ["positionTolerance"] = 2, ["cameraAngleTolerance"] = 10 };
                    var checkpoints = new JArray(checkpoint);
                    if (scenario == "empty") checkpoints = new JArray();
                    if (scenario == "position-length") checkpoint["position"] = new JArray(1, 2);
                    if (scenario == "quaternion-length") checkpoint["cameraRotation"] = new JArray(0, 0, 1);
                    if (scenario == "zero-quaternion") checkpoint["cameraRotation"] = new JArray(0, 0, 0, 0);
                    if (scenario == "negative-tolerance") checkpoint["positionTolerance"] = -1;
                    if (scenario == "late") checkpoint["elapsed"] = 21;
                    if (scenario == "unordered") { var earlier = (JObject)checkpoint.DeepClone(); earlier["elapsed"] = .5; checkpoints.Add(earlier); }
                    if (scenario == "nonfinite") checkpoint["position"][0] = float.NaN;
                    if (scenario == "too-many") for (int i = 1; i < 129; i++) { var extra = (JObject)checkpoint.DeepClone(); extra["elapsed"] = 1 + i * .1; checkpoints.Add(extra); }
                    changed["rehearsal"] = new JObject { ["routeId"] = "tutorial-path-inspection-v1", ["outputRoot"] = output,
                        ["durationSeconds"] = 20, ["maxLatenessSeconds"] = .25, ["checkpoints"] = checkpoints,
                        ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":1,\"keys\":[\"W\"]},{\"timeSeconds\":2,\"keys\":[]}]}" };
                    WriteConfig(configPath, changed);
                    var parsed = InvokeJson("ParseRequest", Args(configPath, save), personal);
                    bool expected = scenario == "valid" || scenario == "empty";
                    Require((bool)parsed["success"] == expected && !Directory.Exists(save) && !Directory.Exists(output), "Checkpoint validity must be decided before runtime/filesystem ownership.");
                    if (scenario == "valid")
                    {
                        Require((parsed["config"]["rehearsal"]["checkpoints"] as JArray)?.Count == 1, "Observed checkpoint must survive config export.");
                        var method = typeof(PerformanceProcessHarness).GetMethod("BuildRehearsalCheckpoints", BindingFlags.Static | BindingFlags.NonPublic);
                        Require(method != null, "Checkpoint conversion for the actual process owner is missing.");
                        var dto = parsed["config"]["rehearsal"].ToObject<TutorialRehearsalConfig>();
                        var runtime = (PerformanceRouteRehearsal.Checkpoint[])method.Invoke(null, new object[] { dto });
                        Require(runtime.Length == 1 && runtime[0].position == new Vector3(1, 2, 3) && runtime[0].cameraRotation == Quaternion.identity &&
                            runtime[0].positionTolerance == 2 && runtime[0].cameraAngleTolerance == 10 && runtime[0].elapsed == 1, "Runtime owner must receive the declared scalar poses and tolerances.");
                    }
                });
            foreach (bool fullDuration in new[] { true, false })
                Test(report, "Tutorial full route rehearsal requires its explicit 150-second protocol: " + fullDuration, () =>
                {
                    var changed = (JObject)config.DeepClone(); changed["mode"] = "tutorial-route-rehearsal"; changed.Remove("capture");
                    changed["rehearsal"] = new JObject { ["routeId"] = "tutorial-exploration-discovery-v1", ["outputRoot"] = output,
                        ["durationSeconds"] = fullDuration ? 150 : 7, ["maxLatenessSeconds"] = .25,
                        ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":1,\"keys\":[\"W\"]},{\"timeSeconds\":2,\"keys\":[]}]}" };
                    WriteConfig(configPath, changed);
                    var parsed = InvokeJson("ParseRequest", Args(configPath, save), personal);
                    Require((bool)parsed["success"] == fullDuration && !Directory.Exists(save) && !Directory.Exists(output), "Full-route discovery must have a separate explicit protocol without starting gameplay");
                });
            Test(report, "Explicit twenty-second path inspection has a separate screenshot protocol", () =>
            {
                var changed = (JObject)config.DeepClone(); changed["mode"] = "tutorial-path-inspection"; changed.Remove("capture");
                changed["rehearsal"] = new JObject { ["routeId"] = "tutorial-path-inspection-v1", ["outputRoot"] = output,
                    ["durationSeconds"] = 20, ["maxLatenessSeconds"] = .25,
                    ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":1,\"keys\":[\"W\"]},{\"timeSeconds\":2,\"keys\":[]}]}" };
                WriteConfig(configPath, changed);
                var parsed = InvokeJson("ParseRequest", Args(configPath, save), personal);
                Require((bool)parsed["success"] && !Directory.Exists(save) && !Directory.Exists(output), "Explicit path inspection must parse without starting or creating files");
            });
            foreach (string invalid in new[] { "duration", "mode", "relativeOutput", "personalOutput", "sameSaveOutput", "resolution", "renderScale", "quality" })
                Test(report, "Reject invalid configuration: " + invalid, () =>
                {
                    var changed = (JObject)config.DeepClone();
                    if (invalid == "duration") changed["capture"]["captureSeconds"] = 1;
                    if (invalid == "mode") changed["mode"] = "arbitrary-movement";
                    if (invalid == "relativeOutput") changed["capture"]["outputRoot"] = "relative";
                    if (invalid == "personalOutput") changed["capture"]["outputRoot"] = personal;
                    if (invalid == "sameSaveOutput") changed["capture"]["outputRoot"] = save;
                    if (invalid == "resolution") changed["width"] = 640;
                    if (invalid == "renderScale") changed["capture"]["renderScale"] = 0.5;
                    if (invalid == "quality") changed["quality"] = "unknown-quality";
                    WriteConfig(configPath, changed);
                    var result = InvokeJson("ParseRequest", Args(configPath, save), personal);
                    Require(!(bool)result["success"] && !Directory.Exists(save) && !Directory.Exists(output), "Rejected config must not create save/output.");
                });
            Test(report, "Inventory exports actual descriptors and unique files", () =>
            {
                WriteConfig(configPath, config);
                var first = InvokeJson("ExecuteInventory", Args(configPath, save), personal);
                var second = InvokeJson("ExecuteInventory", Args(configPath, save), personal);
                Require((bool)first["success"] && (bool)second["success"], "Inventory runs must succeed.");
                var a = first["result"];
                var b = second["result"];
                Require((string)a["inventoryPath"] != (string)b["inventoryPath"], "Inventory files must be exclusive.");
                var inventory = JArray.Parse(File.ReadAllText((string)a["inventoryPath"]));
                Require(inventory.Count > 0 && inventory.All(d => !string.IsNullOrEmpty((string)d["name"])), "Export contains enumerated Unity descriptors.");
                Require(!(bool)a["comparisonValid"] && !(bool)a["fullWindowCompleted"] && ((JArray)a["frames"]).Count == 0, "Inventory is not a timed baseline.");
                Require(!Directory.Exists(save), "Inventory does not initialize save/host session.");
            });
            Test(report, "Stationary binding requires actual SceneMenu rendering context", () =>
            {
                var go = new GameObject("Performance process binding fixture") { hideFlags = HideFlags.HideAndDontSave };
                var controller = go.AddComponent<PerformanceCaptureController>();
                try
                {
                    bool bound = (bool)Harness().GetMethod("BindStationaryContext", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { controller });
                    var active = SceneManager.GetActiveScene();
                    var production = PlayerCamera.instance;
                    var cameras = Camera.allCameras.Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null).ToArray();
                    bool canBind = active.name == "SceneMenu" && production != null && cameras.Length == 2 &&
                        cameras.Contains(production.cameraObject) && production.cameraObject.transform.IsChildOf(production.transform) && cameras.Count(c => c != production.cameraObject && c.gameObject.scene == active) == 1 &&
                        cameras.All(c => c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() != null &&
                            c.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderType == UnityEngine.Rendering.Universal.CameraRenderType.Base) &&
                        (cameras[0].targetDisplay != cameras[1].targetDisplay || cameras[0].depth != cameras[1].depth);
                    Require(bound == canBind, "Wrong scene/missing or unowned/tied screen-output sets must fail closed.");
                    if (canBind)
                    {
                        Require(controller.CameraIdProvider != null && controller.EnemyCountProvider != null && controller.EnemyCountProvider() >= 0 && !string.IsNullOrEmpty(controller.CameraIdProvider()), "Providers report actual bound camera/preflight roster.");
                        report.actualSceneMenuBindingVerified = Application.isPlaying;
                    }
                    Require(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Binding itself starts no recorder/host.");
                }
                finally { controller.End(); UnityEngine.Object.DestroyImmediate(go); }
            });
            Test(report, "Explicit ordered two-camera set preserves both output references", () => WithFixedContext(output, (controller, camera) =>
            {
                string identity = controller.CameraIdProvider();
                Require(identity.Contains("camera-set-v1") && identity.Contains("scene-output") && !identity.Contains("instanceId"), "Stable descriptor must represent the ordered camera set without process-local IDs.");
                var descriptor = JArray.Parse(identity.Substring(identity.IndexOf("/[", StringComparison.Ordinal) + 1));
                Require(descriptor.Count == 2 && (float)descriptor[0]["depth"] == 0 && (float)descriptor[1]["depth"] == 1,
                    "Descriptor freezes both expected output cameras in rendering order.");
                Require(camera.enabled && camera.transform.GetChild(0).GetComponent<Camera>().enabled, "Binding preserves both authored output states.");
                var result = controller.End();
                Require(result.cameraVerified && !result.populationVerified && !result.comparisonValid, "Stable camera diagnostic cannot certify population or route comparison.");
            }));
            foreach (string mutation in new[] { "disabled", "depth", "viewport", "display", "culling", "clear", "fov", "projection", "targetTexture", "urpType", "urpStack", "replacement" })
                Test(report, "Camera set rejects invariant mutation: " + mutation, () => WithFixedContext(output, (controller, camera) =>
                {
                    RenderTexture texture = null;
                    GameObject replacement = null;
                    try
                    {
                        if (mutation == "disabled") camera.enabled = false;
                        if (mutation == "depth") camera.depth += 1;
                        if (mutation == "viewport") camera.rect = new Rect(0, 0, .5f, 1);
                        if (mutation == "display") camera.targetDisplay = 1;
                        if (mutation == "culling") camera.cullingMask ^= 1;
                        if (mutation == "clear") camera.clearFlags = CameraClearFlags.Nothing;
                        if (mutation == "fov") camera.fieldOfView += 1;
                        if (mutation == "projection") camera.orthographic = true;
                        if (mutation == "targetTexture") { texture = new RenderTexture(8, 8, 0); camera.targetTexture = texture; }
                        var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                        if (mutation == "urpType") data.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
                        if (mutation == "urpStack") data.cameraStack.Add(camera);
                        if (mutation == "replacement") { replacement = new GameObject("QA replacement"); replacement.AddComponent<Camera>(); UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(camera); }
                        bool rejected = false;
                        try { controller.CameraIdProvider(); } catch (InvalidOperationException) { rejected = true; }
                        Require(rejected, "Cached provider must reject " + mutation + " before export.");
                        var result = controller.End();
                        Require(!result.cameraVerified && result.conditionsChanged && !result.comparisonValid, "Postflight must preserve camera-set rejection.");
                    }
                    finally { if (texture != null) { if (camera != null) camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(texture); } if (replacement != null) UnityEngine.Object.DestroyImmediate(replacement); }
                }));
            Test(report, "No-start lifetime release rejects stale provider", () => WithFixedContext(output, (controller, camera) =>
            {
                controller.GetComponent<PerformanceContextLifetime>().Release();
                bool rejected = false;
                try { controller.CameraIdProvider(); } catch (ObjectDisposedException) { rejected = true; }
                Require(rejected, "Released context cannot keep servicing stale providers.");
            }, false));
            Test(report, "New screen camera invalidates actual exported postflight", () => WithFixedContext(output, (controller, camera) =>
            {
                var extra = new GameObject("New QA screen camera");
                try
                {
                    extra.AddComponent<Camera>();
                    var result = controller.End();
                    Require(!result.cameraVerified && result.conditionsChanged && result.errors.Any(e => e.Contains("camera")), "A newly created eligible camera invalidates the whole result.");
                    var exported = JObject.Parse(File.ReadAllText(result.jsonPath));
                    Require(!(bool)exported["cameraVerified"] && !(bool)exported["comparisonValid"], "Postflight rejection must precede actual JSON export.");
                }
                finally { UnityEngine.Object.DestroyImmediate(extra); }
            }));
            Test(report, "New AI invalidates actual exported postflight", () => WithFixedContext(output, (controller, camera) =>
            {
                var extra = new GameObject("New QA roster AI");
                extra.SetActive(false);
                try
                {
                    extra.AddComponent<AICharacterManager>();
                    extra.SetActive(true);
                    var result = controller.End();
                    Require(!result.populationVerified && result.conditionsChanged && result.errors.Any(e => e.Contains("population")), "A newly created active AI invalidates the whole result.");
                    var exported = JObject.Parse(File.ReadAllText(result.jsonPath));
                    Require(!(bool)exported["populationVerified"] && !(bool)exported["comparisonValid"], "Population rejection must precede actual JSON export.");
                }
                finally { UnityEngine.Object.DestroyImmediate(extra); }
            }));
            Test(report, "Unexpected pause transition invalidates actual sampling", () => WithFixedContext(output, (controller, camera) =>
            {
                float previousScale = Time.timeScale;
                try
                {
                    Require(!string.IsNullOrEmpty(controller.CameraIdProvider()), "Initial fixed-context provider is valid before pause.");
                    Sample(controller, 29.5);
                    Time.timeScale = previousScale == 0 ? 1 : 0;
                    Sample(controller, 30.01);
                    var result = controller.End();
                    Require(!controller.IsCapturing && result.conditionsChanged && !result.comparisonValid && result.errors.Any(e => e.Contains("timeScale")), "A changed pause state fails through the actual sampling/provider path.");
                    var exported = JObject.Parse(File.ReadAllText(result.jsonPath));
                    Require((bool)exported["conditionsChanged"] && !(bool)exported["comparisonValid"], "Pause failure is preserved in exported diagnostic.");
                }
                finally { Time.timeScale = previousScale; }
            }));
            Test(report, "Tutorial terminal evidence survives a locked personal fingerprint in an owned temporary tree", () =>
            {
                string fakePersonal = Path.Combine(root, "locked-personal-fixture"), terminalPath = Path.Combine(root, "locked-terminal.json");
                Directory.CreateDirectory(fakePersonal);
                string slot = Path.Combine(fakePersonal, "characterSlot_01"); File.WriteAllText(slot, "owned fixture only");
                var owned = new GameObject("Owned tutorial terminal fixture"); owned.SetActive(false);
                try
                {
                    var process = owned.AddComponent<PerformanceTutorialProcess>();
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    Action<string, object> set = (name, value) => typeof(PerformanceTutorialProcess).GetField(name, flags).SetValue(process, value);
                    set("personalRoot", fakePersonal); set("outputPath", terminalPath);
                    set("personalBefore", new Dictionary<string, string>()); set("request", new PerformanceProcessResponse { saveRoot = save });
                    using (var held = new FileStream(slot, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        typeof(PerformanceTutorialProcess).GetMethod("Finish", flags).Invoke(process, new object[] { false });
                    var terminal = JObject.Parse(File.ReadAllText(terminalPath));
                    Require((bool)terminal["completed"] && !(bool)terminal["passed"] && !(bool)terminal["personalUnchanged"] && ((JArray)terminal["errors"]).Count > 0,
                        "Fingerprint failure must produce terminal failed evidence, not strand the process");
                }
                finally { UnityEngine.Object.DestroyImmediate(owned); }
            });
            Test(report, "Tutorial startup errors survive until process adoption", () =>
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
                var prepare = typeof(PerformanceTutorialProcess).GetMethod("PrepareStartupDiagnostics", flags);
                var take = typeof(PerformanceTutorialProcess).GetMethod("TakeStartupDiagnostics", flags);
                Require(prepare != null && take != null, "BeforeSceneLoad runtime error collector missing");
                const string marker = "Expected owned tutorial startup diagnostic fixture";
                try
                {
                    prepare.Invoke(null, null); Debug.LogError(marker);
                    var collected = (List<string>)take.Invoke(null, null);
                    Require(collected.Any(e => e.Contains(marker)), "Actual startup log event must reach the eventual process collector");
                    Require(((List<string>)take.Invoke(null, null)).Count == 0, "Startup diagnostics must be drained once");
                }
                finally { take.Invoke(null, null); }
            });
            Test(report, "Mission discovery copies actual choice target state into scalar JSON", () =>
            {
                var npcRoot = new GameObject("Owned mission observation NPC"); npcRoot.SetActive(false);
                var choice = new GameObject("Owned selected mission"); choice.SetActive(false);
                var enemy = new GameObject("Owned mission enemy");
                try
                {
                    var npc = npcRoot.AddComponent<NPCDialogue>(); npc.canvasEscolha = npcRoot; npc.dialogueUI = npcRoot; npc.escolha1 = choice;
                    var mission = choice.AddComponent<MissionManager>(); mission.enemies.Add(enemy); mission.isMissionCompleted = true;
                    var method = typeof(PerformanceTutorialProcess).GetMethod("CaptureMissionState", BindingFlags.Static | BindingFlags.NonPublic);
                    Require(method != null, "Actual mission discovery snapshot is missing.");
                    var snapshot = JObject.Parse(JsonConvert.SerializeObject(method.Invoke(null, new object[] { npc, 1.25 })));
                    Require((bool)snapshot["present"] && !(bool)snapshot["choiceVisible"] && (double)snapshot["elapsed"] == 1.25,
                        "Snapshot must read actual hierarchy visibility and elapsed time.");
                    Require((string)snapshot["choice1"]["name"] == choice.name && (bool)snapshot["choice1"]["completed"] &&
                        (int)snapshot["choice1"]["enemyReferences"] == 1 && (int)snapshot["choice1"]["activeEnemyReferences"] == 1,
                        "Actual selected-target objective state must survive scalar JSON export.");
                    Require(snapshot["choice2"].Type == JTokenType.Null, "Absent target is unavailable, not a completed mission.");
                    UnityEngine.Object.DestroyImmediate(choice); choice = null;
                    var removed = JObject.Parse(JsonConvert.SerializeObject(method.Invoke(null, new object[] { npc, 2.0 })));
                    Require(removed["choice1"].Type == JTokenType.Null, "Destroyed Unity target wrappers must export as unavailable.");
                    UnityEngine.Object.DestroyImmediate(npcRoot); npcRoot = null;
                    var gone = JObject.Parse(JsonConvert.SerializeObject(method.Invoke(null, new object[] { npc, 3.0 })));
                    Require(!(bool)gone["present"], "Destroyed NPC wrappers must not cause a diagnostic exception.");
                }
                finally { if (npcRoot != null) UnityEngine.Object.DestroyImmediate(npcRoot); if (choice != null) UnityEngine.Object.DestroyImmediate(choice); UnityEngine.Object.DestroyImmediate(enemy); }
            });
            Test(report, "AI reset audit cannot activate outside full Video1 mode", () =>
            {
                var invalid = (JObject)config.DeepClone(); invalid["mode"] = "tutorial-rehearsal"; invalid.Remove("capture");
                invalid["rehearsal"] = new JObject { ["routeId"] = "tutorial-controls-functional-v1", ["outputRoot"] = Path.Combine(root, "reset-policy-output"),
                    ["durationSeconds"] = 7, ["maxLatenessSeconds"] = .075,
                    ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":0.5,\"keys\":[\"W\"]},{\"timeSeconds\":1.5,\"keys\":[]}]}" };
                string resetSave = Path.Combine(root, "reset-policy-save");
                WriteConfig(configPath, invalid);
                Require((bool)InvokeJson("ParseRequest", Args(configPath, resetSave), personal)["success"], "Positive control tutorial config must be valid");
                invalid["rehearsal"]["auditAiSpawnerReset"] = true; WriteConfig(configPath, invalid);
                var response = InvokeJson("ParseRequest", Args(configPath, resetSave), personal);
                Require(!(bool)response["success"], "AI reset audit escaped its explicit full Video1 rehearsal mode");
            });
            Test(report, "AI reset audit requires completed functional source evidence", () =>
            {
                var method = typeof(PerformanceTutorialProcess).GetMethod("CanAuditAiReset", BindingFlags.Static | BindingFlags.NonPublic);
                Require(method != null, "AI reset evidence gate missing");
                Func<bool, bool, bool, bool> allowed = (functional, phases, history) => (bool)method.Invoke(null, new object[] { functional, phases, history });
                Require(allowed(true, true, true) && !allowed(false, true, true) && !allowed(true, false, true) && !allowed(true, true, false), "AI reset ran before all required evidence was available");
            });
            foreach (string scenario in new[] { "default", "ten-with-snapshots", "zero", "too-many", "cycles-unflagged", "snapshots-unflagged" })
                Test(report, "Bounded AI reset and memory audit configuration: " + scenario, () =>
                {
                    var method = typeof(PerformanceProcessHarness).GetMethod("ValidateAiResetAudit", BindingFlags.Static | BindingFlags.NonPublic);
                    Require(method != null, "Bounded reset audit policy missing");
                    var fields = new JObject { ["auditAiSpawnerReset"] = true };
                    if (scenario == "ten-with-snapshots") { fields["aiSpawnerResetCycles"] = 10; fields["auditMemorySnapshots"] = true; }
                    if (scenario == "zero") fields["aiSpawnerResetCycles"] = 0;
                    if (scenario == "too-many") fields["aiSpawnerResetCycles"] = 11;
                    if (scenario == "cycles-unflagged") { fields["auditAiSpawnerReset"] = false; fields["aiSpawnerResetCycles"] = 2; }
                    if (scenario == "snapshots-unflagged") { fields["auditAiSpawnerReset"] = false; fields["auditMemorySnapshots"] = true; }
                    var cfg = new PerformanceProcessConfig { mode = "video1-route-rehearsal", rehearsal = fields.ToObject<TutorialRehearsalConfig>() };
                    bool rejected = false;
                    try { method.Invoke(null, new object[] { cfg }); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                    Require(rejected == (scenario != "default" && scenario != "ten-with-snapshots"), "Unbounded/unflagged reset or memory audit policy accepted");
                });
            Test(report, "Non-reset rehearsal cannot silently request extra cycles", () =>
            {
                var changed = (JObject)config.DeepClone(); changed["mode"] = "tutorial-route-rehearsal"; changed.Remove("capture");
                changed["rehearsal"] = new JObject { ["routeId"] = "tutorial-exploration-discovery-v1", ["outputRoot"] = output,
                    ["durationSeconds"] = 150, ["maxLatenessSeconds"] = .25, ["aiSpawnerResetCycles"] = 2,
                    ["timelineJson"] = "{\"schemaVersion\":1,\"frames\":[{\"timeSeconds\":0,\"keys\":[]},{\"timeSeconds\":1,\"keys\":[\"W\"]},{\"timeSeconds\":2,\"keys\":[]}]}" };
                WriteConfig(configPath, changed);
                Require(!(bool)InvokeJson("ParseRequest", Args(configPath, save), personal)["success"], "Extra cycles escaped parser validation without activation");
            });
            foreach (string scenario in new[] { "valid", "zero-cycles", "too-many", "warmup", "settle", "wrong-route", "wrong-scene", "dead-seed", "relative-output", "personal-output", "save-overlap", "mixed-capture", "mixed-rehearsal", "wrong-mode", "unsupported-skill-bindings" })
                Test(report, "Menu-world retention process policy: " + scenario, () =>
                {
                    string retentionSave = Path.Combine(root, "retention-policy-" + scenario, "saves"), retentionOutput = Path.Combine(root, "retention-policy-" + scenario, "output");
                    var changed = (JObject)config.DeepClone(); changed["mode"] = "world-retention"; changed.Remove("capture");
                    var seed = new CharacterSaveData { sceneIndex = 2, characterName = "Retention QA", currentHealth = 225, vitality = 15, endurance = 10,
                        xPosition = -237.878f, yPosition = 102.004f, zPosition = 529.331f };
                    var retention = new JObject { ["routeId"] = "video1-menu-world-retention-v1", ["outputRoot"] = retentionOutput,
                        ["cycles"] = 10, ["warmupSeconds"] = 30, ["settleSeconds"] = 3, ["seedSaveJson"] = JsonUtility.ToJson(seed) };
                    if (scenario == "zero-cycles") retention["cycles"] = 0;
                    if (scenario == "too-many") retention["cycles"] = 11;
                    if (scenario == "warmup") retention["warmupSeconds"] = 29;
                    if (scenario == "settle") retention["settleSeconds"] = 0;
                    if (scenario == "wrong-route") retention["routeId"] = "arbitrary-retention";
                    if (scenario == "wrong-scene") { seed.sceneIndex = 3; retention["seedSaveJson"] = JsonUtility.ToJson(seed); }
                    if (scenario == "dead-seed") { seed.currentHealth = 0; retention["seedSaveJson"] = JsonUtility.ToJson(seed); }
                    if (scenario == "relative-output") retention["outputRoot"] = "relative";
                    if (scenario == "personal-output") retention["outputRoot"] = personal;
                    if (scenario == "save-overlap") retention["outputRoot"] = retentionSave;
                    if (scenario == "mixed-capture") changed["capture"] = config["capture"].DeepClone();
                    if (scenario == "mixed-rehearsal") changed["rehearsal"] = new JObject();
                    if (scenario == "wrong-mode") changed["mode"] = "inventory";
                    if (scenario == "unsupported-skill-bindings")
                    {
                        var boundSeed = JObject.Parse((string)retention["seedSaveJson"]);
                        boundSeed["qaSkillBindings"] = new JObject { ["SkillDefense"] = new JArray(new JObject { ["assetName"] = "Escudo", ["type"] = "RPGKarawara.SkillTree.DefenseSkill" }) };
                        retention["seedSaveJson"] = boundSeed.ToString(Formatting.None);
                    }
                    changed["retention"] = retention; WriteConfig(configPath, changed);
                    var parsed = InvokeJson("ParseRequest", Args(configPath, retentionSave), personal);
                    Require((bool)parsed["success"] == (scenario == "valid") && !Directory.Exists(retentionSave) && !Directory.Exists(retentionOutput),
                        "Retention policy must validate an explicit isolated ten-cycle 30-second warm-up protocol before ownership");
                });
            Test(report, "World retention entry refuses Editor activation without side effects", () =>
            {
                var type = typeof(PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceWorldRetentionProcess");
                Require(type != null, "World retention standalone entry missing");
                var rootObject = new GameObject("Owned retention activation fixture");
                try
                {
                    var owner = rootObject.AddComponent(type); bool rejected = false;
                    try { type.GetMethod("Initialize").Invoke(owner, new object[] { null }); }
                    catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                    Require(rejected, "Editor/null retention request activated a host or session");
                }
                finally { UnityEngine.Object.DestroyImmediate(rootObject); }
            });
            Test(report, "Movement-heading replay requires actual camera rotation to pass", () =>
            {
                var method = typeof(PerformanceTutorialProcess).GetMethod("FunctionalObservationAccepted", BindingFlags.Static | BindingFlags.NonPublic);
                var observation = new JObject { ["passed"] = true, ["observedMovement"] = true, ["observedFinalRelease"] = true,
                    ["controlsRestored"] = true, ["cameraMode"] = "movement-heading-v1" }.ToObject<PerformanceRouteRehearsal.Result>();
                Require(!(bool)method.Invoke(null, new object[] { observation, false, false }), "Heading mode passed without actual camera rotation");
                observation.observedCameraMovement = true;
                Require((bool)method.Invoke(null, new object[] { observation, false, false }), "Actual camera rotation was rejected");
            });
            Test(report, "Explicit route action requirements reject an unobserved jump", () =>
            {
                var method = typeof(PerformanceTutorialProcess).GetMethod("FunctionalObservationAccepted", BindingFlags.Static | BindingFlags.NonPublic);
                Require(method != null, "Explicit functional action acceptance gate is missing");
                var observation = new PerformanceRouteRehearsal.Result { passed = true, observedMovement = true, observedFinalRelease = true, controlsRestored = true };
                Func<bool, bool, bool> accepted = (jump, camera) => (bool)method.Invoke(null, new object[] { observation, jump, camera });
                Require(accepted(false, false) && !accepted(true, false) && !accepted(false, true), "Required action was inferred from position-only checkpoint success");
                observation.observedJump = true; Require(accepted(true, false), "An actual observed jump was rejected");
                observation.observedCameraMovement = true; Require(accepted(true, true), "Actual required controls were rejected");
                observation.controlsRestored = false; Require(!accepted(false, false), "Failed input restoration was accepted");
            });
            Test(report, "Retention final fingerprint IO failures are latched instead of escaping finalization", () =>
            {
                var type = typeof(PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceWorldRetentionProcess");
                var method = type?.GetMethod("CaptureFinalFingerprints", BindingFlags.Static | BindingFlags.NonPublic);
                Require(method != null, "Final fingerprint failure boundary missing");
                string folder = Path.Combine(root, "world-retention-final-fingerprint-fixture"); Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "save.json"); File.WriteAllText(file, "fixture");
                var errors = new List<string>();
                var readable = (Dictionary<string, string>)method.Invoke(null, new object[] { folder, errors });
                Require(readable != null && readable.Count == 1 && errors.Count == 0, "Readable files should fingerprint normally: " + JsonConvert.SerializeObject(new { readable, errors }));
                using (var locked = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    var result = method.Invoke(null, new object[] { folder, errors });
                    Require(result == null && errors.Count == 1 && errors[0].Contains("fingerprint"), "IO failure escaped or was accepted as unchanged personal files");
                }
            });
            report.limits.Add("Fixed-context transition fixtures use a separate preflight camera without disabling production cameras; initial ordered real SceneMenu camera-set binding is an independent guard.");
            report.limits.Add("Ordinary Release activation omission requires real Release compilation/process evidence; no Editor boolean substitutes for that check.");
            if (!report.actualSceneMenuBindingVerified) report.limits.Add("Actual SceneMenu PlayMode camera/roster binding remains pending; wrong-scene guard is complementary.");
            report.limits.Add("No 150-second stationary run or required exploration/combat/UI route baseline is established by this fixture.");
            try { if (Directory.Exists(root)) Directory.Delete(root, true); report.cleanedUp = !Directory.Exists(root); }
            catch (Exception error) { report.cases.Add(new Case { name = "Cleanup", detail = error.ToString() }); }
            report.completed = true; report.passed = report.cleanedUp && report.cases.All(c => c.passed);
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-process-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Debug.Log("Performance process fixture result: " + path);
            if (!report.passed) Debug.LogError("Performance process fixture FAILED; " + path);
        }
        static void WithFixedContext(string output, Action<PerformanceCaptureController, Camera> test, bool begin = true)
        {
            var originalScene = SceneManager.GetActiveScene();
            Scene scene = default;
            GameObject owned = null;
            PerformanceCaptureController controller = null;
            try
            {
                // Actual scene membership without modifying or saving the user's production scene.
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Require(SceneManager.GetActiveScene() == scene, "Disposable transition scene must become active.");
                owned = new GameObject("QA fixed context fixture");
                controller = owned.AddComponent<PerformanceCaptureController>();
                var camera = owned.AddComponent<Camera>();
                camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var second = new GameObject("QA second output camera");
                second.transform.SetParent(owned.transform);
                var secondCamera = second.AddComponent<Camera>();
                secondCamera.depth = 1;
                second.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                Require(scene.IsValid() && scene.isLoaded && owned.scene == scene && camera.gameObject.scene == scene &&
                    camera.enabled && camera.gameObject.activeInHierarchy && camera.targetTexture == null,
                    "Transition fixture must begin with an actual active-scene screen camera.");
                var callback = typeof(PerformanceCaptureController).GetProperty("FinalValidation", BindingFlags.Public | BindingFlags.Instance);
                Require(callback != null, "FinalValidation pre-export callback is missing.");
                var contextType = Harness().GetNestedType("FixedStationaryContext", BindingFlags.NonPublic);
                Require(contextType != null, "FixedStationaryContext preflight/postflight contract is missing.");
                Activator.CreateInstance(contextType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new object[] { controller, scene, new[] { camera, secondCamera } }, null);
                Require(controller.CameraIdProvider != null && controller.EnemyCountProvider != null && callback.GetValue(controller) != null, "Fixed context binds providers and final validation without starting a capture.");
                var config = new CaptureConfig { qaEnabled = true, routeId = "scene-menu-idle-v1", outputRoot = output,
                    commit = "transition-fixture", buildId = "Editor complementary transition fixture", camera = controller.CameraIdProvider(), enemyCount = controller.EnemyCountProvider(), renderScale = 1 };
                Require(!begin || controller.Begin(config), "Transition fixture begins the existing actual capture/export path.");
                test(controller, camera);
            }
            finally
            {
                if (controller != null) controller.End();
                if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
        static void Sample(PerformanceCaptureController controller, double elapsed)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var result = (CaptureResult)typeof(PerformanceCaptureController).GetField("result", flags).GetValue(controller);
            typeof(PerformanceCaptureController).GetMethod("SampleAt", flags).Invoke(controller, new object[] { result.startedTimestampSeconds + elapsed });
        }
        static JObject SafeSkillSeed() => new JObject { ["sceneIndex"] = 2, ["characterName"] = "QA seed fixture", ["currentHealth"] = 225, ["vitality"] = 15, ["endurance"] = 10,
            ["xPosition"] = 0, ["yPosition"] = 0, ["zPosition"] = 0, ["skillsSlot"] = new JArray(new JObject { ["instanceID"] = 0 }, new JObject { ["instanceID"] = 0 }, new JObject { ["instanceID"] = 0 }) };
        static JObject Config(string output) => JObject.FromObject(new
        {
            mode = "inventory", width = 1920, height = 1080, quality = QualitySettings.names[QualitySettings.GetQualityLevel()], vsync = 0, targetFrameRate = -1,
            capture = new CaptureConfig { qaEnabled = true, routeId = "scene-menu-idle-v1", outputRoot = output, commit = "fixture", buildId = "Editor inventory fixture", camera = "actual-scene-menu-camera", renderScale = 1 }
        });
        static string[] Args(string path, string root) => new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", root };
        static void WriteConfig(string path, JObject config) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, config.ToString()); }
        static Type Harness()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceProcessHarness")).FirstOrDefault(t => t != null);
            Require(type != null, "Performance process harness implementation missing."); return type;
        }
        static JObject InvokeJson(string method, string[] args, string personal) => JObject.Parse(JsonConvert.SerializeObject(Harness().GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { args, personal })));
        static void Test(Report report, string name, Action test) { var result = new Case { name = name }; try { test(); result.passed = true; result.detail = "passed"; } catch (Exception error) { result.detail = error.ToString(); } report.cases.Add(result); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
