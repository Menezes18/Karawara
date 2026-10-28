using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceAuxiliaryValidation
    {
        static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance." + name)).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException(name + " missing");
        sealed class Pair : IDisposable
        {
            public GameObject root;
            public Animator animator;
            public Component source, marker;
            public IDisposable context;
            public Pair(string kind)
            {
                root = new GameObject("Owned auxiliary source"); root.SetActive(false); animator = root.AddComponent<Animator>();
                if (kind == "particle") source = root.AddComponent<ParticleSystem>();
                else if (kind == "spawner") { source = root.AddComponent<AICharacterSpawner>(); ((Behaviour)source).enabled = false; }
                else { root.AddComponent<Camera>(); root.AddComponent<AudioListener>(); root.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); source = root.AddComponent<DestroyAfterTime>(); ((Behaviour)source).enabled = false; }
                marker = root.AddComponent(Type("PerformanceAuxiliarySource")); marker.GetType().GetField("visual").SetValue(marker, animator); marker.GetType().GetField("source").SetValue(marker, source); root.SetActive(true);
            }
            public void Bind() { var markers = Array.CreateInstance(marker.GetType(), 1); markers.SetValue(marker, 0); context = (IDisposable)Type("PerformanceAuxiliaryContext").GetMethod("Begin").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, markers }); }
            public void Call(string method) => Type("PerformanceAuxiliaryContext").GetMethod(method).Invoke(context, null);
            public bool Changed()
            {
                var result = new CaptureResult { conditions = new CaptureConfig { enemyCount = 17 } }; Type("PerformanceAuxiliaryContext").GetMethod("Validate").Invoke(context, new object[] { result });
                Require(!result.populationVerified && !result.comparisonValid && result.conditions.enemyCount == 17, "Auxiliary workload changed enemy count or certification"); return result.conditionsChanged;
            }
            public void Dispose() { context?.Dispose(); if (root != null) Object.DestroyImmediate(root); }
        }
        static void Require(bool yes, string message) { if (!yes) throw new InvalidOperationException(message); }
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required"); var cases = new List<object>();
            foreach (string kind in new[] { "particle", "spawner", "camera" }) foreach (string change in new[] { "baseline", "warming", "active-roundtrip", "marker-roundtrip", "reparent-roundtrip", "unknown-clone", "destroy-source", "owner-reference", "native-state", "source-api", "census", "dispose" })
            {
                try
                {
                    using (var pair = new Pair(kind))
                    {
                        pair.Bind(); if (change == "warming") { pair.root.SetActive(false); pair.root.SetActive(true); Require(!pair.Changed(), "Expected warmup visibility invalidated source"); } pair.Call("Freeze");
                        if (change == "active-roundtrip") { pair.root.SetActive(false); pair.root.SetActive(true); }
                        if (change == "marker-roundtrip") { ((Behaviour)pair.marker).enabled = false; ((Behaviour)pair.marker).enabled = true; }
                        if (change == "reparent-roundtrip") { var parent = new GameObject("Temporary parent"); pair.root.transform.SetParent(parent.transform); pair.root.transform.SetParent(null); Object.DestroyImmediate(parent); }
                        if (change == "unknown-clone") { var clone = Object.Instantiate(pair.root); Object.DestroyImmediate(clone); }
                        if (change == "destroy-source") Object.DestroyImmediate(pair.source);
                        if (change == "owner-reference") pair.marker.GetType().GetField("source").SetValue(pair.marker, null);
                        if (change == "native-state") { if (kind == "particle") ((ParticleSystem)pair.source).Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); else if (kind == "camera") pair.root.GetComponent<Camera>().enabled = false; else ((Behaviour)pair.source).enabled = true; }
                        if (change == "source-api")
                        {
                            if (kind == "particle") ((ParticleSystem)pair.source).Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                            if (kind == "spawner") ((AICharacterSpawner)pair.source).AttemptToSpawnCharacter();
                            if (kind == "camera") pair.source.GetType().GetMethod("Destroyer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pair.source, null);
                        }
                        if (change == "census") { var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }); Require(!census.report.unclassifiedAnimators.Any(r => r.componentId == pair.animator.GetInstanceID()) && !census.report.actors.Any(r => r.objectId == pair.root.GetInstanceID()), "Auxiliary source omitted or counted as actor"); }
                        if (change == "dispose") { pair.context.Dispose(); Require((bool)Type("PerformanceAuxiliaryContext").GetProperty("IsDisposed").GetValue(pair.context), "Context not disposed"); pair.root.SetActive(false); pair.root.SetActive(true); }
                        else
                        {
                            bool expected = !new[] { "baseline", "warming", "census" }.Contains(change); Require(pair.Changed() == expected, "Source transition/reference not latched or baseline rejected");
                            if (!expected) { var poll = (Action)Delegate.CreateDelegate(typeof(Action), pair.context, Type("PerformanceAuxiliaryContext").GetMethod("Poll")); for (int i = 0; i < 64; i++) poll(); long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) poll(); Require(before == GC.GetAllocatedBytesForCurrentThread(), "Warm polling allocated"); }
                        }
                    }
                    cases.Add(new { kind, change, passed = true });
                }
                catch (Exception error) { cases.Add(new { kind, change, passed = false, detail = error.ToString() }); }
            }
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { kind = "contract", change = name, passed = true }); } catch (Exception error) { cases.Add(new { kind = "contract", change = name, passed = false, detail = error.ToString() }); } };
            test("no-opt-in", () => Require(Type("PerformanceAuxiliaryContext").GetMethod("Begin").Invoke(null, new object[] { null, null }) == null, "Unflagged Begin inspected invalid arguments"));
            test("duplicate-source-rejected-without-owner-leak", () =>
            {
                using (var pair = new Pair("particle"))
                {
                    var values = Array.CreateInstance(pair.marker.GetType(), 2); values.SetValue(pair.marker, 0); values.SetValue(pair.marker, 1); bool rejected = false;
                    try { ((IDisposable)Type("PerformanceAuxiliaryContext").GetMethod("Begin").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, values })).Dispose(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                    Require(rejected, "Duplicate source accepted"); pair.Bind(); pair.Call("Freeze"); Require(!pair.Changed(), "Failed Begin leaked ownership");
                }
            });
            test("unknown-script-rejected", () =>
            {
                using (var pair = new Pair("particle"))
                {
                    pair.root.AddComponent<LoadScene>(); bool rejected = false; try { pair.Bind(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; } Require(rejected, "Unreviewed script silently admitted");
                }
            });
            test("destroyed-required-camera-component", () => { using (var pair = new Pair("camera")) { pair.Bind(); Object.DestroyImmediate(pair.root.GetComponent<AudioListener>()); Require(pair.Changed(), "Real listener destruction hidden before freeze"); } });
            foreach (string kind in new[] { "particle", "spawner", "camera" }) test(kind + " mixed-native-role-rejected", () =>
            {
                using (var pair = new Pair(kind))
                {
                    if (kind == "camera") pair.root.AddComponent<ParticleSystem>(); else pair.root.AddComponent<Camera>();
                    bool rejected = false; try { pair.Bind(); pair.Call("Freeze"); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                    Require(rejected, "Unreviewed mixed camera/particle role was admitted");
                }
            });
            test("process-disposal", () =>
            {
                using (var pair = new Pair("particle"))
                {
                    pair.Bind(); var owner = new GameObject("Owned auxiliary process fixture"); owner.SetActive(false); string temp = Path.GetFullPath("Builds/TOBE-QA/RunData/auxiliary-dispose-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
                    try
                    {
                        var process = owner.AddComponent<PerformanceTutorialProcess>(); Action<string, object> set = (name, value) => process.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(process, value);
                        set("initialized", true); set("personalRoot", Path.Combine(temp, "personal-unused")); set("personalBefore", new Dictionary<string, string>()); set("outputPath", Path.Combine(temp, "terminal.json")); set("request", new PerformanceProcessResponse { saveRoot = Path.Combine(temp, "save-unused") }); set("auxiliaryContext", pair.context); owner.SetActive(true); process.enabled = false;
                        Require((bool)Type("PerformanceAuxiliaryContext").GetProperty("IsDisposed").GetValue(pair.context), "Interrupted process retained auxiliary source ownership");
                    }
                    finally { Object.DestroyImmediate(owner); }
                }
            });
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-auxiliary-fixture-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases }, Formatting.Indented));
        }
        public static void RunActualDestruction()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required"); var pair = new Pair("camera"); pair.source.GetType().GetField("timeToDestroy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pair.source, .03f); pair.Bind(); pair.Call("Freeze"); pair.source.GetType().GetMethod("Destroyer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pair.source, null);
            bool immediate = pair.Changed(); double deadline = EditorApplication.timeSinceStartup + 10; EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                bool done = false, passed = false; string error = null; object checkpoint = null;
                try
                {
                    if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Actual delayed camera destruction"); pair.Call("Poll"); if (pair.root != null) return;
                    Require(immediate && pair.Changed(), "Scheduling or actual destruction not latched"); Require(!(bool)Type("PerformanceAuxiliaryContext").GetProperty("SourceFaulted").GetValue(pair.context), "Normal destruction reported internal failure"); checkpoint = Type("PerformanceAuxiliaryContext").GetMethod("CaptureCheckpoint").Invoke(pair.context, new object[] { "actual-camera-destroyed" }); done = true; passed = true;
                }
                catch (Exception e) { done = true; error = e.ToString(); }
                finally
                {
                    if (done) { EditorApplication.update -= tick; pair.Dispose(); string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-auxiliary-real-destroy-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed, immediate, error, checkpoint }, Formatting.Indented)); }
                }
            }; EditorApplication.update += tick;
        }
    }
}
