using System;
using System.IO;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCheckpointImageValidation
    {
        public static string Run()
        {
            var checks = new JArray();
            Action<string, Action> test = (name, body) => { try { body(); checks.Add(new JObject { ["name"] = name, ["passed"] = true }); } catch (Exception error) { checks.Add(new JObject { ["name"] = name, ["passed"] = false, ["error"] = error.GetBaseException().Message }); } };
            Func<Type> type = () => typeof(CaptureResult).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceCheckpointImages", true);
            Func<string, string, JToken, bool, bool> validate = (mode, arm, flag, profiler) =>
                (bool)type().GetMethod("ValidateRequest").Invoke(null, new object[] { mode, arm, flag, profiler });
            Action<Action> rejects = body => { bool rejected = false; try { body(); } catch (Exception e) { rejected = e.GetBaseException() is ArgumentException; } if (!rejected) throw new InvalidOperationException("Invalid diagnostic request was accepted"); };
            Action<bool> require = value => { if (!value) throw new InvalidOperationException("Assertion failed"); };
            test("default leaves existing captures unchanged", () => require(!validate("tutorial-certified-route", null, null, false)));
            test("explicit typed Tutorial diagnostic", () => require(validate("tutorial-certified-route", "full", new JValue(true), false)));
            test("explicit false remains disabled", () => require(!validate("tutorial-certified-route", null, new JValue(false), false)));
            test("light timing cannot take images", () => rejects(() => validate("tutorial-certified-route", "light-control", new JValue(true), false)));
            test("other route cannot silently consume images", () => rejects(() => validate("cuca-certified-route", null, new JValue(true), false)));
            test("profiler and image diagnostics stay distinct", () => rejects(() => validate("tutorial-certified-route", null, new JValue(true), true)));
            test("string true rejected", () => rejects(() => validate("tutorial-certified-route", null, new JValue("true"), false)));
            test("numeric flag rejected", () => rejects(() => validate("tutorial-certified-route", null, new JValue(1), false)));
            test("array flag rejected", () => rejects(() => validate("tutorial-certified-route", null, new JArray(true), false)));
            Func<int, double, int> due = (next, elapsed) => (int)type().GetMethod("DueIndex").Invoke(null, new object[] { next, elapsed });
            test("before checkpoint does nothing", () => require(due(0, 19.99) == -1));
            test("105 and135 checkpoint slots are explicit", () => require(due(3, 105.01) == 3 && due(4, 135.01) == 4));
            test("missed checkpoint is rejected", () => rejects(() => due(3, 105.3)));
            test("NaN time rejected", () => rejects(() => due(0, double.NaN)));
            test("negative index rejected", () => rejects(() => due(-1, 20)));
            test("completed schedule does nothing", () => require(due(6, 151) == -1));
            test("image verdict distinct from timing verdict", () => require(typeof(PerformanceCertifiedRouteProcess.EvidenceReport).GetProperty("checkpointImagesCompleted") != null));
            Func<int, string, PerformanceCertifiedRouteProcess.EvidenceReport> evidence = (count, scene) =>
            {
                var image = Activator.CreateInstance(type().GetNestedType("Report"));
                image.GetType().GetField("completed").SetValue(image, true);
                var requests = (System.Collections.IList)image.GetType().GetField("requests").GetValue(image);
                for (int i = 0; i < count; i++) requests.Add(Activator.CreateInstance(type().GetNestedType("Request")));
                var verdict = (PerformanceBoundedRoutePopulation.DomainVerdict)Activator.CreateInstance(typeof(PerformanceBoundedRoutePopulation.DomainVerdict), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[] { null }, null);
                var report = new PerformanceCertifiedRouteProcess.EvidenceReport { measurementArm = "checkpoint-images-diagnostic",
                    manifest = new PerformanceCertifiedRouteProcess.ManifestData { scene = scene },
                    capture = new CaptureResult { fullWindowCompleted = true, comparisonValid = false, conditionsChanged = true },
                    population = verdict, poses = new PerformanceRoutePoseProof.Report { completed = true, closurePassed = true },
                    unexpectedConditionsChanged = false, moved = true, jumped = true, cameraMoved = true, finalRelease = true, controlsRestored = true };
                report.GetType().GetField("checkpointImages").SetValue(report, image); return report;
            };
            Func<PerformanceCertifiedRouteProcess.EvidenceReport, bool> completed = report => (bool)report.GetType().GetProperty("checkpointImagesCompleted").GetValue(report, null);
            test("complete diagnostic remains timing ineligible", () => { var e = evidence(6, "Tutorial_Final"); require(completed(e) && !e.routePopulationQualified); });
            test("missing images cannot qualify", () => require(!completed(evidence(0, "Tutorial_Final"))));
            test("wrong scene cannot qualify", () => require(!completed(evidence(6, "Video1"))));
            test("unplanned changed conditions cannot qualify", () => { var e = evidence(6, "Tutorial_Final"); e.unexpectedConditionsChanged = true; require(!completed(e)); });
            test("failed pose proof cannot qualify", () => { var e = evidence(6, "Tutorial_Final"); e.poses.closurePassed = false; require(!completed(e)); });
            Func<bool, double, double, bool> files = (ready, requested, now) => (bool)type().GetMethod("FilesReadyWithinDeadline").Invoke(null, new object[] { ready, requested, now });
            test("ready headers within deadline complete", () => require(files(true, 100, 104.99)));
            test("ready headers first observed after deadline reject", () => rejects(() => files(true, 100, 105.01)));
            test("missing headers before deadline remain pending", () => require(!files(false, 100, 104)));
            test("file observation clock cannot reverse", () => rejects(() => files(true, 100, 99)));
            test("nonfinite file observation rejected", () => rejects(() => files(true, 100, double.NaN)));
            bool passed = true; foreach (var row in checks) passed &= (bool)row["passed"];
            string path = "Docs/Validation/TOBE/Execution/checkpoint-image-fixture-" + Guid.NewGuid().ToString("N") + ".json";
            using (var file = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(file))
                writer.Write(new JObject { ["passed"] = passed, ["checks"] = checks, ["g1bAccepted"] = false }.ToString());
            return path;
        }
    }
}
