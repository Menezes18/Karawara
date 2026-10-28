using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCaptureValidation
    {
        static Report activeReport;
        [MenuItem("Karawara/Validation/TOBE/Validate Performance Capture")]
        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            activeReport = report;
            try
            {
                Require(!CaptureContract.IncludesFrame(29.999, 29.999), "Warm-up excluded.");
                Require(!CaptureContract.IncludesFrame(29.99, 30.01), "Frame crossing warm-up excluded.");
                Require(CaptureContract.IncludesFrame(30, 30.01), "Capture begins at 30 seconds.");
                Require(CaptureContract.IncludesFrame(149.99, 150), "Capture ends at 150 seconds.");
                Require(!CaptureContract.IncludesFrame(150, 150.01), "Frames after capture excluded.");
                var synthetic = Enumerable.Range(1, 151).Where(end => CaptureContract.IncludesFrame(end - 1, end)).ToArray();
                Require(synthetic.Length == 120 && synthetic.First() == 31 && synthetic.Last() == 150, "Artificial timestamps must contain exactly 120 capture seconds.");
                var series = new double[] { 10, 10, 10, 20, 30 };
                Require(CaptureContract.Percentile(series, 0.5) == 10, "Median must be 10.");
                Require(CaptureContract.Percentile(series, 0.95) == 30, "Nearest-rank p95 must be 30.");
                Require(CaptureContract.NewRunId() != CaptureContract.NewRunId(), "Run IDs unique.");
                var metric = JObject.Parse(CaptureContract.MetricJson("missing", "count", false, 0));
                Require(!(bool)metric["available"] && metric["value"].Type == JTokenType.Null, "Absent counter must be JSON null.");
                ValidateLifecycleAndOutput();
                report.passed = true;
                report.detail = "PASS: synthetic 30s+120s, nearest-rank statistics, IDs, unavailable=null, JSON/CSV and 10 Begin/End cycles.";
                Debug.Log(report.detail);
            }
            catch (Exception error)
            {
                report.detail = error.ToString();
                Debug.LogError("Performance capture validation FAILED: " + report.detail);
            }
            report.completed = true;
            activeReport = null;
            WriteExclusiveReport("performance-capture-fixture-result", report);
        }
        static void ValidateLifecycleAndOutput()
        {
            var root = Path.Combine(Path.GetTempPath(), "KarawaraPerformanceFixture-" + Guid.NewGuid().ToString("N"));
            var go = new GameObject("Performance capture temporary QA fixture") { hideFlags = HideFlags.HideAndDontSave };
            var controller = go.AddComponent<PerformanceCaptureController>();
            try
            {
                var config = new CaptureConfig { qaEnabled = true, routeId = "synthetic-fixture", runId = "same-requested-id",
                    outputRoot = root, commit = "fixture-only", buildId = "Editor fixture", camera = "fixture-no-gameplay", enemyCount = 0,
                    counterNames = new[] { "__Karawara_deliberately_absent_counter__" } };
                config.qaEnabled = false;
                Require(!controller.Begin(config), "Explicit QA flag required.");
                config.qaEnabled = true;
                config.captureSeconds = 119;
                Require(!controller.Begin(config), "120-second capture contract required.");
                config.captureSeconds = 120;
                string previousPath = null, previousContents = null;
                for (int cycle = 0; cycle < 10; cycle++)
                {
                    Require(controller.Begin(config), "Begin cycle " + cycle);
                    Require(!controller.Begin(config), "Concurrent Begin rejected.");
                    if (cycle > 0 && config.counterNames.Length > 1) Require(controller.OwnedRecorderCount > 0, "Actual enumerated counter owned during cycle " + cycle);
                    if (cycle > 0)
                    {
                        var owned = (System.Collections.IEnumerable)typeof(PerformanceCaptureController).GetField("recorders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                        foreach (var counter in owned)
                        {
                            var native = (Unity.Profiling.ProfilerRecorder)counter.GetType().GetField("recorder").GetValue(counter);
                            Require(native.IsRunning, "Owned native recorder actually running before sampling cycle " + cycle);
                        }
                    }
                    var result = controller.End();
                    Require(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Owned handles cleared after cycle " + cycle);
                    Require(!result.fullWindowCompleted && result.medianFrameTimeMs == null, "Immediate fixture End is incomplete, never a measured baseline.");
                    var json = JObject.Parse(File.ReadAllText(result.jsonPath));
                    Require(json["frames"] is JArray && (string)json["runId"] == result.runId, "Parseable JSON and matching ID.");
                    var absent = ((JArray)json["requestedMetrics"]).First(m => (string)m["name"] == "__Karawara_deliberately_absent_counter__");
                    Require(!(bool)absent["available"] && absent["value"].Type == JTokenType.Null, "Missing counter remains null in capture output.");
                    Require(File.ReadAllText(result.csvPath).StartsWith("timestampSeconds,frameTimeMs"), "CSV header present.");
                    Require(JArray.Parse(File.ReadAllText(result.inventoryPath)).Count > 0, "Real Unity inventory exported.");
                    if (cycle == 0)
                    {
                        // Select an actual enumerated descriptor, never invent a Unity counter name.
                        var descriptor = result.counterInventory.FirstOrDefault(d => d.flags.Contains("Counter") && result.counterInventory.Count(other => other.name == d.name) == 1);
                        Require(descriptor != null, "At least one real Unity counter enumerated for lifecycle coverage.");
                        config.counterNames = new[] { descriptor.name, "__Karawara_deliberately_absent_counter__" };
                    }
                    if (previousPath != null) Require(previousPath != result.jsonPath && File.ReadAllText(previousPath) == previousContents, "Prior capture never overwritten.");
                    previousPath = result.jsonPath;
                    previousContents = File.ReadAllText(previousPath);
                    Require(controller.End() == result, "End is idempotent.");
                }
                Require(Directory.GetFiles(root, "*.csv").Length == 10 && Directory.GetFiles(root, "*.counters.json").Length == 10, "Each run has exclusive files.");
                if (EditorApplication.isPlaying)
                {
                    Require(controller.Begin(config), "Begin for actual PlayMode disable cleanup.");
                    controller.enabled = false;
                    Require(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Actual PlayMode OnDisable releases handles.");
                    activeReport.playModeDisableVerified = true;
                    controller.enabled = true;
                    Require(controller.Begin(config), "Begin for actual PlayMode destruction cleanup.");
                    UnityEngine.Object.DestroyImmediate(go);
                    Require(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Actual PlayMode destruction releases handles.");
                    activeReport.playModeDestroyVerified = true;
                }
                else
                {
                    activeReport.pendingCases.Add("PlayMode OnDisable/OnDestroy cleanup: EditMode does not deliver ordinary MonoBehaviour lifecycle callbacks.");
                    Require(controller.Begin(config), "Begin for EditMode explicit stop cleanup.");
                    controller.End();
                    Require(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Explicit EditMode stop releases handles.");
                }
            }
            finally
            {
                controller.End();
                UnityEngine.Object.DestroyImmediate(go);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
        [MenuItem("Karawara/Validation/TOBE/Validate Performance Capture Review Regressions")]
        public static void RunReviewRegressions()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            activeReport = report;
            foreach (var regression in new Action[] { ValidateTerminalBoundaryRegression, ValidateIncompleteCoverageRegression, ValidateThrowingProviderRegression })
            {
                try { regression(); }
                catch (Exception error) { report.detail += regression.Method.Name + ": " + error + "\n"; }
            }
            report.completed = true;
            report.passed = report.cases.Count > 0 && report.cases.All(c => c.passed) && string.IsNullOrEmpty(report.detail);
            if (report.passed) report.detail = "PASS: injected-timestamp terminal frame and throwing-provider regressions. Synthetic contract evidence only.";
            activeReport = null;
            WriteExclusiveReport("performance-capture-review-fixture-result", report);
            if (report.passed) Debug.Log(report.detail); else Debug.LogError("Performance review regressions FAILED: " + report.detail);
        }
        static void ValidateTerminalBoundaryRegression()
        {
            foreach (double end in new[] { 150.005, 150.48 })
                WithRegressionController((controller, config) =>
                {
                    Require(controller.Begin(config), "Begin synthetic terminal boundary " + end);
                    SampleThrough14998(controller);
                    SampleElapsed(controller, end);
                    var result = controller.LastResult;
                    Require(result != null && !controller.IsCapturing && controller.OwnedRecorderCount == 0, "Terminal boundary ends and disposes capture " + end);
                    Require(result.frames.Count == 121, "Complete terminal straddling frame retained " + end);
                    double terminalMs = result.frames.Last().frameTimeMs;
                    Require(terminalMs >= (end - 149.98) * 1000 - 1, "Terminal frame retains entire duration " + end);
                    Require(result.frames.Last().timestampSeconds - result.startedTimestampSeconds >= 150, "Raw end timestamp exposes overrun " + end);
                    Require(result.fullWindowCompleted, "Continuous synthetic window completes " + end);
                    Require(Math.Abs(result.capturedCoverageSeconds - 120) < 0.000001, "Clipped window coverage is 120 seconds " + end);
                    Require(Math.Abs(result.terminalOverrunSeconds - (end - 150)) < 0.000001, "Terminal overrun explicitly reported " + end);
                    if (end > 150.4) Require(result.frames.Any(f => f.frameTimeMs >= 499), "Terminal 500ms hitch contributes raw statistics series.");
                });
        }
        static void ValidateThrowingProviderRegression()
        {
            foreach (bool throwCamera in new[] { false, true })
                WithRegressionController((controller, config) =>
                {
                    Require(controller.Begin(config), "Probe actual descriptors for throwing-provider regression.");
                    var inventory = controller.End().counterInventory;
                    config.counterNames = new[] { inventory.First(d => d.flags.Contains("Counter") && inventory.Count(other => other.name == d.name) == 1).name };
                    Require(controller.Begin(config) && controller.OwnedRecorderCount > 0, "Provider regression starts with actual owned recorder.");
                    SampleThrough14998(controller);
                    controller.EnemyCountProvider = () => { if (!throwCamera) throw new InvalidOperationException("Injected enemy provider failure"); return 0; };
                    controller.CameraIdProvider = () => { if (throwCamera) throw new InvalidOperationException("Injected camera provider failure"); return config.camera; };
                    Exception escaped = null;
                    try { SampleElapsed(controller, 150.48); }
                    catch (Exception error) { escaped = error; }
                    Require(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Throwing " + (throwCamera ? "camera" : "enemy") + " provider stops and disposes immediately.");
                    Require(escaped == null, "Provider exception contained by sampling owner.");
                    var result = controller.LastResult;
                    Require(result != null && !result.comparisonValid && !result.fullWindowCompleted, "Failure at elapsed >=150 never certifies complete/valid capture.");
                    Require(result.errors.Any(e => e.Contains("Injected")), "Provider failure recorded.");
                    var json = JObject.Parse(File.ReadAllText(result.jsonPath));
                    Require(!(bool)json["comparisonValid"] && ((JArray)json["errors"]).Count > 0, "Invalid provider result and error persisted.");
                });
        }
        static void WithRegressionController(Action<PerformanceCaptureController, CaptureConfig> run)
        {
            var root = Path.Combine(Path.GetTempPath(), "KarawaraPerformanceRegression-" + Guid.NewGuid().ToString("N"));
            var go = new GameObject("Synthetic performance review regression") { hideFlags = HideFlags.HideAndDontSave };
            var controller = go.AddComponent<PerformanceCaptureController>();
            var config = new CaptureConfig { qaEnabled = true, routeId = "synthetic-review-only", outputRoot = root,
                commit = "fixture-only", buildId = "Editor synthetic timestamp regression", camera = "synthetic-camera", enemyCount = 0 };
            controller.EnemyCountProvider = () => config.enemyCount;
            controller.CameraIdProvider = () => config.camera;
            try { run(controller, config); }
            finally { controller.End(); UnityEngine.Object.DestroyImmediate(go); if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        static void ValidateIncompleteCoverageRegression()
        {
            foreach (double boundaryEnd in new[] { 30.01, 149.0 })
                WithRegressionController((controller, config) =>
                {
                    Require(controller.Begin(config), "Begin warm-up boundary coverage " + boundaryEnd);
                    SampleElapsed(controller, 29.99);
                    SampleElapsed(controller, boundaryEnd);
                    SampleElapsed(controller, 150.02);
                    var result = controller.LastResult;
                    Require(result != null && controller.OwnedRecorderCount == 0, "Boundary coverage capture terminates and disposes.");
                    Require(result.frames.Count == 1, "Warm-up crossing interval excluded from frame series.");
                    Require(Math.Abs(result.excludedWarmupBoundaryEndSeconds.Value - boundaryEnd) < 0.000001, "Excluded boundary raw end retained.");
                    Require(result.fullWindowCompleted == (boundaryEnd < 31), "Only small excluded warm-up overlap permits full window.");
                    if (boundaryEnd > 31) Require(!result.comparisonValid, "Near-empty measured series never comparison-valid.");
                });
        }
        static void SampleElapsed(PerformanceCaptureController controller, double elapsed)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var result = (CaptureResult)typeof(PerformanceCaptureController).GetField("result", flags).GetValue(controller);
            typeof(PerformanceCaptureController).GetMethod("SampleAt", flags).Invoke(controller, new object[] { result.startedTimestampSeconds + elapsed });
        }
        static void SampleThrough14998(PerformanceCaptureController controller)
        {
            SampleElapsed(controller, 30);
            for (int end = 31; end <= 149; end++) SampleElapsed(controller, end);
            SampleElapsed(controller, 149.98);
        }
        static void Require(bool value, string message)
        {
            activeReport?.cases.Add(new Case { name = message, passed = value });
            if (!value) throw new InvalidOperationException(message);
        }
        static void WriteExclusiveReport(string prefix, Report report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Validation/TOBE/Execution/" + prefix + "-" + Guid.NewGuid().ToString("N") + ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Debug.Log("Performance fixture result: " + path);
        }
        [Serializable] class Case { public string name; public bool passed; }
        [Serializable] class Report { public string utc, detail; public bool passed, completed, playModeDisableVerified, playModeDestroyVerified; public List<Case> cases = new List<Case>(); public List<string> pendingCases = new List<string>(); }
    }
}








