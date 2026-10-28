using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceUiCheckpointImageValidation
    {
        public static string Run()
        {
            var checks = new JArray();
            Action<string, Action> test = (name, body) => { try { body(); checks.Add(new JObject { ["name"] = name, ["passed"] = true }); } catch (Exception error) { checks.Add(new JObject { ["name"] = name, ["passed"] = false, ["error"] = error.GetBaseException().Message }); } };
            Action<bool> require = value => { if (!value) throw new InvalidOperationException("Assertion failed"); };
            test("UI full may request separate images", () => require(PerformanceCheckpointImages.ValidateRequest("tutorial-ui-certified-route", "full", new JValue(true), false)));
            test("UI default remains disabled", () => require(!PerformanceCheckpointImages.ValidateRequest("tutorial-ui-certified-route", null, null, false)));
            test("UI LIGHT rejects images", () => { bool rejected = false; try { PerformanceCheckpointImages.ValidateRequest("tutorial-ui-certified-route", "light-control", new JValue(true), false); } catch (ArgumentException) { rejected = true; } require(rejected); });
            Func<int, PerformanceUiRouteProcess.EvidenceReport> create = count =>
            {
                var images = new PerformanceCheckpointImages.Report { completed = true };
                for (int i = 0; i < count; i++) images.requests.Add(new PerformanceCheckpointImages.Request());
                var verdict = (PerformanceBoundedRoutePopulation.DomainVerdict)Activator.CreateInstance(typeof(PerformanceBoundedRoutePopulation.DomainVerdict), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { null }, null);
                var report = new PerformanceUiRouteProcess.EvidenceReport { measurementArm = "checkpoint-images-diagnostic",
                    manifest = new PerformanceUiRouteProcess.ManifestData { scene = "Tutorial_Final" },
                    capture = new CaptureResult { fullWindowCompleted = true, comparisonValid = false, conditionsChanged = true }, population = verdict,
                    preparationOpened = true, preparationClosed = true, stationaryCameraVerified = true, finalRelease = true, controlsRestored = true,
                    phaseTransitions = new PerformanceUiRouteProcess.TransitionRow[3], phaseObservedFrames = new[] { 1, 1, 1, 1 } };
                report.GetType().GetField("checkpointImages").SetValue(report, images);
                report.GetType().GetField("unexpectedConditionsChanged").SetValue(report, false); return report;
            };
            Func<PerformanceUiRouteProcess.EvidenceReport, bool> completed = r => (bool)r.GetType().GetProperty("checkpointImagesCompleted").GetValue(r, null);
            test("complete UI images remain timing ineligible", () => { var r = create(6); require(completed(r) && !r.routePopulationQualified); });
            test("incomplete image set rejected", () => require(!completed(create(5))));
            test("all four UI phases required", () => { var r = create(6); r.phaseObservedFrames[2] = 0; require(!completed(r)); });
            test("controls must be restored", () => { var r = create(6); r.controlsRestored = false; require(!completed(r)); });
            test("session timing verdict stays false", () =>
            {
                var session = Activator.CreateInstance(typeof(PerformanceUiRouteProcess), true);
                typeof(PerformanceUiRouteProcess).GetProperty("Evidence").SetValue(session, create(6), null);
                require((bool)session.GetType().GetProperty("Qualified").GetValue(session, null));
                require(!(bool)session.GetType().GetProperty("TimingQualified").GetValue(session, null));
            });
            bool passed = true; foreach (var check in checks) passed &= (bool)check["passed"];
            string path = "Docs/Validation/TOBE/Execution/ui-checkpoint-image-fixture-" + Guid.NewGuid().ToString("N") + ".json";
            using (var file = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(file))
                writer.Write(new JObject { ["passed"] = passed, ["checks"] = checks, ["g1bAccepted"] = false }.ToString());
            return path;
        }
    }
}
