using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceLightCaptureValidation
    {
        public static string Run()
        {
            var cases = new JArray();
            Action<string, Action> test = (name, action) => { try { action(); cases.Add(new JObject { ["name"] = name, ["passed"] = true }); } catch (Exception e) { cases.Add(new JObject { ["name"] = name, ["passed"] = false, ["detail"] = e.ToString() }); } };
            test("Light mode is explicit and defaults false", () => { var field = Field(); Need(!(bool)field.GetValue(new CaptureConfig()), "Light control defaulted on"); });
            test("Light rejects any requested native counter", () => With((controller, config) => { Light(config); config.counterNames = new[] { "__absent_counter__" }; Need(!controller.Begin(config) && controller.OwnedRecorderCount == 0 && !controller.IsCapturing, "Light accepted native counter request"); }));
            test("Light never invokes throwing providers and owns no recorders", () => With((controller, config) => { Light(config); controller.EnemyCountProvider = () => throw new Exception("enemy provider invoked"); controller.CameraIdProvider = () => throw new Exception("camera provider invoked"); Need(controller.Begin(config) && controller.OwnedRecorderCount == 0, "Light Begin failed or owns recorders"); Sample(controller, Started(controller) + 1); Need(controller.IsCapturing, "Light invoked provider"); var result = controller.End(); Need(result.errors.Count == 0 && result.requestedMetrics.Count == 0 && result.counterInventory.Count == 0, "Light collected metrics or errors"); }));
            test("Normal mode still invokes provider", () => With((controller, config) => { int calls = 0; controller.EnemyCountProvider = () => { calls++; return 0; }; Need(controller.Begin(config), "Normal Begin failed"); Sample(controller, Started(controller) + 1); Need(calls == 1, "Normal provider skipped"); controller.End(); }));
            test("Postflight cannot promote light result even when callback throws", () => With((controller, config) => { Light(config); controller.FinalValidation = result => { result.populationVerified = result.cameraVerified = result.comparisonValid = true; throw new Exception("fixture finalization rejection"); }; Need(controller.Begin(config), "Light Begin failed"); var actual = controller.End(); Need(!actual.populationVerified && !actual.cameraVerified && !actual.comparisonValid && (bool)Field().GetValue(actual.conditions), "Light callback promoted result"); var exported = JObject.Parse(File.ReadAllText(actual.jsonPath)); Need(!(bool)exported["populationVerified"] && !(bool)exported["cameraVerified"] && !(bool)exported["comparisonValid"] && (bool)exported["conditions"]["lightControl"], "Light export promoted result"); }));
            test("Light exports common clock and sampler estimate and releases buffers", () => With((controller, config) => { Light(config); Need(controller.Begin(config), "Light Begin failed"); Sample(controller, Started(controller) + 1); var result = controller.End(); Need(result.externalClock?.ended != null && result.updateClock?.ticks.Length == 1 && result.captureBufferBytesEstimate == 120000L * 25L, "Missing common clock/QPC bytes"); Need(!controller.IsCapturing && controller.OwnedRecorderCount == 0, "Capture resources retained"); foreach (string field in new[] { "timestamps", "frameTimes", "focused", "counterValues", "updateClock" }) Need(typeof(PerformanceCaptureController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller) == null, "Buffer retained: " + field); }));
            test("Light full synthetic window remains diagnostic after successful postflight", () => With((controller, config) => { Light(config); bool ran = false; controller.FinalValidation = result => { ran = true; result.populationVerified = result.cameraVerified = result.comparisonValid = true; }; Need(controller.Begin(config), "Light Begin failed"); double start = Started(controller); for (int i = 1; i <= 2400; i++) Sample(controller, start + i * .0625); var result = controller.LastResult; Need(ran && result != null && result.fullWindowCompleted && !result.populationVerified && !result.cameraVerified && !result.comparisonValid, "Completed light capture promoted or postflight skipped"); }));
            var report = new JObject { ["cases"] = cases, ["passed"] = true, ["g1bAccepted"] = false, ["boundary"] = "Editor synthetic control contract; no standalone timing or route acceptance" };
            foreach (JObject row in cases) if (!(bool)row["passed"]) report["passed"] = false;
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/light-capture-contract-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(path, report.ToString()); return path;
        }
        static FieldInfo Field() { var field = typeof(CaptureConfig).GetField("lightControl"); Need(field != null, "CaptureConfig.lightControl is not implemented"); return field; }
        static void Light(CaptureConfig config) => Field().SetValue(config, true);
        static void Sample(PerformanceCaptureController controller, double now) => typeof(PerformanceCaptureController).GetMethod("SampleAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(controller, new object[] { now });
        static double Started(PerformanceCaptureController controller) => (double)typeof(PerformanceCaptureController).GetProperty("StartedTimestampSeconds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
        static void With(Action<PerformanceCaptureController, CaptureConfig> action)
        {
            var go = new GameObject("Light capture isolated fixture") { hideFlags = HideFlags.HideAndDontSave }; var controller = go.AddComponent<PerformanceCaptureController>();
            var config = new CaptureConfig { qaEnabled = true, routeId = "light-contract-fixture", outputRoot = Path.GetFullPath("Docs/Validation/TOBE/Execution/light-capture-fixture-" + Guid.NewGuid().ToString("N")), commit = "fixture", buildId = "Editor fixture", camera = "fixture", enemyCount = 0 };
            try { action(controller, config); } finally { controller.End(); UnityEngine.Object.DestroyImmediate(go); }
        }
        static void Need(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    }
}
