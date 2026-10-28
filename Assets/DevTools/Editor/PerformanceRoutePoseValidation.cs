using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceRoutePoseValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate Frozen Route Poses")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode scalar pose fixture required.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true }); } catch (Exception error) { cases.Add(new { name, passed = false, error = error.ToString() }); } };
            test("Declaration alone does not grant observed completion", () => Require(!(bool)Evidence(Create(Sample()))["completed"], "Unobserved completion granted"));
            test("Observe all mandatory phases and actual measured closure", () =>
            {
                var owner = Create(Sample()); Feed(owner); Invoke(owner, "Complete"); var evidence = Evidence(owner);
                Require((bool)evidence["completed"] && (bool)evidence["closurePassed"] && !(bool)evidence["faulted"] && (int)evidence["observedCount"] == 6, "Complete measured pose proof unavailable");
            });
            foreach (string mutation in new[] { "missing-anchor", "missing-end", "position-bound", "camera-bound", "quaternion", "order", "nonfinite" })
                test("Reject malformed frozen pose contract: " + mutation, () =>
                {
                    var sample = Sample();
                    switch (mutation)
                    {
                        case "missing-anchor": sample = sample.Where(p => p.elapsed != 30).ToArray(); break;
                        case "missing-end": sample = sample.Take(5).ToArray(); break;
                        case "position-bound": sample[0].positionTolerance = 2.1f; break;
                        case "camera-bound": sample[0].cameraAngleTolerance = 10.1f; break;
                        case "quaternion": sample[0].cameraRotation[3] = 2; break;
                        case "order": sample[1].elapsed = 0; break;
                        case "nonfinite": sample[0].position[0] = float.NaN; break;
                    }
                    Reject(() => Create(sample));
                });
            test("Freeze caller pose arrays before observation", () =>
            {
                var sample = Sample(); var owner = Create(sample); sample[0].position[0] = 999;
                Feed(owner); Invoke(owner, "Complete"); Require((bool)Evidence(owner)["completed"], "Caller mutation replaced frozen expectations");
            });
            test("Late checkpoint is latched and cannot be cleared", () =>
            {
                var owner = Create(Sample()); Reject(() => Observe(owner, .3, Vector3.zero, Quaternion.identity));
                Reject(() => Observe(owner, 30, Vector3.zero, Quaternion.identity)); Require((bool)Evidence(owner)["faulted"], "Lost failure latch");
            });
            test("Per-checkpoint errors do not substitute actual anchor closure", () =>
            {
                var sample = Sample(); sample[5].position[0] = 1.9f; var owner = Create(sample);
                foreach (var point in sample.Take(5)) Observe(owner, point.elapsed, point.elapsed == 30 ? new Vector3(-1.9f, 0, 0) : Vector3.zero, Quaternion.identity);
                Reject(() => Observe(owner, 150, new Vector3(3.8f, 0, 0), Quaternion.identity));
                Require(!(bool)Evidence(owner)["closurePassed"], "Independent actual anchor closure was omitted");
            });
            test("Incomplete timeline cannot complete", () => { var owner = Create(Sample()); Observe(owner, 0, Vector3.zero, Quaternion.identity); Reject(() => Invoke(owner, "Complete")); });
            test("Warmed observe callback allocates no managed bytes", () =>
            {
                var owner = Create(Sample()); var method = owner.GetType().GetMethod("Observe");
                var observe = (Action<double, Vector3, Quaternion, int>)Delegate.CreateDelegate(typeof(Action<double, Vector3, Quaternion, int>), owner, method);
                for (int i = 0; i < 200; i++) observe(i * .0001, Vector3.zero, Quaternion.identity, i);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 200; i < 1200; i++) observe(i * .0001, Vector3.zero, Quaternion.identity, i);
                Require(GC.GetAllocatedBytesForCurrentThread() == before, "Pose observation allocated managed bytes");
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-route-pose-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                boundary = "Scalar pose/closure/ownership/allocation fixture only; not actual Player route, rendering or timing acceptance." }, Formatting.Indented));
            Debug.Log("Route pose fixture: " + path);
        }
        static RehearsalPoseCheckpoint[] Sample() => new double[] { 0, 30, 60, 90, 120, 150 }.Select(at => new RehearsalPoseCheckpoint { elapsed = at, position = new float[3], cameraRotation = new float[] { 0, 0, 0, 1 }, positionTolerance = 2, cameraAngleTolerance = 10 }).ToArray();
        static object Create(RehearsalPoseCheckpoint[] sample)
        {
            var type = typeof(PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceRoutePoseProof") ?? throw new InvalidOperationException("Frozen route pose proof missing");
            return type.GetMethod("Create").Invoke(null, new object[] { sample, .25 });
        }
        static void Feed(object owner) { foreach (double at in new double[] { 0, 30, 60, 90, 120, 150 }) Observe(owner, at, Vector3.zero, Quaternion.identity); }
        static void Observe(object owner, double at, Vector3 position, Quaternion rotation) => Invoke(owner, "Observe", at, position, rotation, (int)(at * 100));
        static void Invoke(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name).Invoke(owner, arguments);
        static JObject Evidence(object owner) => JObject.FromObject(owner.GetType().GetProperty("Evidence").GetValue(owner));
        static void Reject(Action body)
        {
            try { body(); } catch (TargetInvocationException error) when (error.InnerException is ArgumentException || error.InnerException is InvalidOperationException) { return; }
            throw new InvalidOperationException("Invalid/incomplete pose proof was accepted");
        }
        static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
