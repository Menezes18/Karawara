using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using System.MiniMap;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using RPGKarawara.DevTools.Performance;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceMinimapValidation
    {
        static Type ContextType() => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceMinimapContext")).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("Minimap source context missing");
        static Type WitnessType() => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceMinimapIconWitness")).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("Icon prefab source witness missing");
        sealed class Pair : IDisposable
        {
            public GameObject canvas, creatorRoot, root;
            public MiniMapEntity creator;
            public IconBaseMiniMapIcon icon;
            public Component witness;
            public Animator animator;
            public IDisposable context;
            public Pair()
            {
                var witnessType = WitnessType();
                canvas = new GameObject("Owned UI fixture canvas", typeof(RectTransform), typeof(Canvas)); canvas.SetActive(false);
                creatorRoot = new GameObject("Owned disabled UI creator"); creatorRoot.SetActive(false);
                creator = creatorRoot.AddComponent<MiniMapEntity>(); creator.enabled = false; creator.DestroyWithObject = false; creator.Target = creator.transform;
                root = new GameObject("Owned UI icon", typeof(RectTransform)); root.SetActive(false); root.transform.SetParent(canvas.transform, false);
                var image = root.AddComponent<Image>(); var group = root.AddComponent<CanvasGroup>(); animator = root.AddComponent<Animator>();
                icon = root.AddComponent<IconBaseMiniMapIcon>(); icon.TargetGraphic = image; icon.m_CanvasGroup = group;
                witness = root.AddComponent(witnessType); witnessType.GetField("icon").SetValue(witness, icon);
                SetInstance(creator, icon); typeof(MiniMapEntity).GetField("cacheItem", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(creator, root);
                icon.SetUp(creator); creatorRoot.SetActive(true); root.SetActive(true); canvas.SetActive(true);
            }
            public void Bind() => context = (IDisposable)ContextType().GetMethod("Begin").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, new[] { creator } });
            public void Call(string name) => ContextType().GetMethod(name).Invoke(context, null);
            public void Freeze() => Call("Freeze");
            public bool Changed()
            {
                var result = new RPGKarawara.DevTools.Performance.CaptureResult { conditions = new RPGKarawara.DevTools.Performance.CaptureConfig { enemyCount = 17 } };
                ContextType().GetMethod("Validate").Invoke(context, new object[] { result });
                Require(result.conditions.enemyCount == 17 && !result.populationVerified && !result.comparisonValid, "UI source changed enemy count or granted global certification");
                return result.conditionsChanged;
            }
            public void Dispose() { context?.Dispose(); context = null; if (creatorRoot != null) Object.DestroyImmediate(creatorRoot); if (canvas != null) Object.DestroyImmediate(canvas); }
        }
        static void SetInstance(MiniMapEntity creator, IconBaseMiniMapIcon icon) => typeof(MiniMapEntity).GetField("<IconInstance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(creator, icon);

        [MenuItem("Karawara/Validation/TOBE/Validate Minimap Sources (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned PlayMode UI fixture required");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); } };
            foreach (string change in new[] { "baseline", "warming-visibility", "frozen-visibility", "creator-hide-show", "witness-toggle", "reparent-roundtrip", "unknown-transient-icon", "destroy-icon", "target", "graphic", "group", "animator", "owner-reference", "dispose" })
                test(change, () =>
                {
                    ContextType();
                    using (var pair = new Pair())
                    {
                        pair.Bind();
                        if (change == "warming-visibility") { pair.icon.SetActive(false); pair.icon.SetActive(true); Require(!pair.Changed(), "Warming visibility must be observable without a frozen-state violation"); }
                        pair.Freeze();
                        if (change == "frozen-visibility") { pair.icon.SetActive(false); pair.icon.SetActive(true); }
                        if (change == "creator-hide-show") { pair.creator.HideItem(); pair.creator.ShowItem(); }
                        if (change == "witness-toggle") { ((Behaviour)pair.witness).enabled = false; ((Behaviour)pair.witness).enabled = true; }
                        if (change == "reparent-roundtrip") { pair.root.transform.SetParent(pair.creatorRoot.transform); pair.root.transform.SetParent(pair.canvas.transform); }
                        if (change == "unknown-transient-icon") { var unknown = Object.Instantiate(pair.root); Object.DestroyImmediate(unknown); }
                        if (change == "destroy-icon") Object.DestroyImmediate(pair.root);
                        if (change == "target") pair.creator.Target = pair.canvas.transform;
                        if (change == "graphic") pair.icon.TargetGraphic = null;
                        if (change == "group") pair.icon.m_CanvasGroup = null;
                        if (change == "animator") pair.animator.enabled = !pair.animator.enabled;
                        if (change == "owner-reference") SetInstance(pair.creator, null);
                        bool changed = change != "baseline" && change != "warming-visibility" && change != "dispose";
                        Require(pair.Changed() == changed, "UI transition/reference state was lost or unrelated state was invalidated");
                        if (!changed)
                        {
                            for (int i = 0; i < 64; i++) pair.Call("Poll");
                            // Cache the callable once; reflection itself allocates and is not the production poll.
                            var poll = (Action)Delegate.CreateDelegate(typeof(Action), pair.context, ContextType().GetMethod("Poll"));
                            long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) poll();
                            Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed UI polling allocated managed bytes");
                        }
                        if (change == "dispose") { pair.context.Dispose(); Require((bool)ContextType().GetProperty("IsDisposed").GetValue(pair.context), "UI ownership retained after disposal"); pair.icon.SetActive(false); pair.icon.SetActive(true); }
                    }
                });
            foreach (string change in new[] { "baseline", "minimap", "other-ui", "auxiliary", "partial-begin-failure" })
                test("Separate post-setup UI/auxiliary phase: " + change, () =>
                {
                    using (var pair = new Pair())
                    {
                        var otherRoot = new GameObject("Owned phase loading", typeof(RectTransform)); otherRoot.SetActive(false);
                        var auxRoot = new GameObject("Owned phase particle"); auxRoot.SetActive(false);
                        PerformanceUiSourceContext other = null; PerformanceAuxiliaryContext auxiliary = null; IDisposable phase = null;
                        try
                        {
                            var loader = otherRoot.AddComponent<LoadScene>(); loader.enabled = false; loader.image = otherRoot;
                            var graphic = new GameObject("Owned phase image", typeof(RectTransform), typeof(Image), typeof(Animator)); graphic.transform.SetParent(otherRoot.transform, false);
                            graphic.AddComponent<PerformanceUiSourceWitness>().animator = graphic.GetComponent<Animator>(); otherRoot.SetActive(true);
                            var animator = auxRoot.AddComponent<Animator>(); var particle = auxRoot.AddComponent<ParticleSystem>();
                            var marker = auxRoot.AddComponent<PerformanceAuxiliarySource>(); marker.visual = animator; marker.source = particle; auxRoot.SetActive(true);
                            pair.Bind(); pair.Freeze();
                            other = PerformanceUiSourceContext.Begin(new[] { "-karawaraPerformanceRoster" }, new MonoBehaviour[] { loader }); other.Freeze();
                            auxiliary = PerformanceAuxiliaryContext.Begin(new[] { "-karawaraPerformanceRoster" }, new[] { marker }); auxiliary.Freeze();
                            pair.root.SetActive(false); pair.root.SetActive(true);
                            otherRoot.SetActive(false); otherRoot.SetActive(true); auxRoot.SetActive(false); auxRoot.SetActive(true);
                            var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                            var type = typeof(PerformanceActorCensus).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceUiAuxiliaryPhase");
                            Require(type != null, "Separate UI/auxiliary phase owner missing");
                            phase = (IDisposable)Activator.CreateInstance(type);
                            if (change == "partial-begin-failure")
                            {
                                typeof(PerformanceActorCensus.Snapshot).GetField("uiSourceOwners", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(census, new MonoBehaviour[] { null });
                                bool rejected = false;
                                try { type.GetMethod("Begin").Invoke(phase, new object[] { census, pair.context, other, auxiliary, 5.0 }); }
                                catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                                Require(rejected, "Malformed replacement owner was accepted");
                                phase.Dispose();
                                using (PerformanceMinimapContext.Begin(new[] { "-karawaraPerformanceRoster" }, new[] { pair.creator })) { }
                                using (PerformanceUiSourceContext.Begin(new[] { "-karawaraPerformanceRoster" }, new MonoBehaviour[] { loader })) { }
                                using (PerformanceAuxiliaryContext.Begin(new[] { "-karawaraPerformanceRoster" }, new[] { marker })) { }
                            }
                            else
                            {
                                type.GetMethod("Begin").Invoke(phase, new object[] { census, pair.context, other, auxiliary, 5.0 });
                                var poll = (Action)Delegate.CreateDelegate(typeof(Action), phase, type.GetMethod("Poll"));
                                for (int i = 0; i < 64; i++) poll();
                                long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) poll();
                                Require(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed combined phase polling allocated managed bytes");
                                if (change == "minimap") { pair.root.SetActive(false); pair.root.SetActive(true); }
                                if (change == "other-ui") { otherRoot.SetActive(false); otherRoot.SetActive(true); }
                                if (change == "auxiliary") { auxRoot.SetActive(false); auxRoot.SetActive(true); }
                                type.GetMethod("Complete").Invoke(phase, new object[] { 10.0 });
                            }
                            var evidence = Newtonsoft.Json.Linq.JObject.FromObject(type.GetProperty("Evidence").GetValue(phase));
                            foreach (string name in new[] { "minimap", "ui", "auxiliary" })
                            {
                                Require((bool)evidence["previous" + char.ToUpperInvariant(name[0]) + name.Substring(1) + "Validation"]["conditionsChanged"], "Original rejection erased: " + name);
                                if (change != "partial-begin-failure")
                                {
                                    var validation = evidence[name + "Validation"];
                                    Require((bool)validation["conditionsChanged"] == (change == name || change == "other-ui" && name == "ui"), "Separate phase callback ownership lost: " + name);
                                    Require(!(bool)validation["populationVerified"] && !(bool)validation["comparisonValid"], "Phase granted global certification");
                                }
                            }
                        }
                        finally { phase?.Dispose(); other?.Dispose(); auxiliary?.Dispose(); Object.DestroyImmediate(otherRoot); Object.DestroyImmediate(auxRoot); }
                    }
                });
            test("Minimap binding change near settling boundary restarts the full wait", () =>
            {
                using (var pair = new Pair())
                {
                    var target = new GameObject("Owned replacement target"); target.SetActive(false);
                    var owner = new GameObject("Owned inactive settling observer"); owner.SetActive(false);
                    try
                    {
                        Func<string> signature = () => Newtonsoft.Json.Linq.JArray.FromObject(PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" }).report.uiIcons).ToString(Formatting.None);
                        string before = signature(); pair.creator.Target = target.transform; string after = signature();
                        Require(before != after, "Exact retained minimap target was omitted from settling state");
                        var process = owner.AddComponent<PerformanceTutorialProcess>();
                        var gate = typeof(PerformanceTutorialProcess).GetMethod("MissionSourcesSettled", BindingFlags.Instance | BindingFlags.NonPublic);
                        Require(gate != null, "Explicit source settling gate missing");
                        Func<string, double, bool> settled = (key, elapsed) => (bool)gate.Invoke(process, new object[] { key, elapsed });
                        Require(!settled(before, 0) && !settled(after, 4.5) && !settled(after, 5) && settled(after, 9.5), "Replacement did not restart the complete five-second interval");
                    }
                    finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(target); }
                }
            });
            test("duplicate creator rejected", () =>
            {
                ContextType(); using (var pair = new Pair())
                {
                    bool rejected = false;
                    try { var context = (IDisposable)ContextType().GetMethod("Begin").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, new[] { pair.creator, pair.creator } }); context.Dispose(); }
                    catch (TargetInvocationException error) when (error.InnerException is ArgumentException) { rejected = true; }
                    Require(rejected, "Duplicate creator/icon identities were accepted");
                }
            });
            test("removed warming Animator prevents Freeze", () =>
            {
                ContextType(); using (var pair = new Pair())
                {
                    pair.Bind(); Object.DestroyImmediate(pair.animator); bool rejected = false;
                    try { pair.Freeze(); } catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
                    Require(rejected, "Freeze accepted an Animator reference removed during settling");
                }
            });
            test("removed warming Canvas prevents Freeze", () =>
            {
                ContextType(); using (var pair = new Pair())
                {
                    pair.Bind(); Object.DestroyImmediate(pair.canvas.GetComponent<Canvas>()); bool rejected = false;
                    try { pair.Freeze(); } catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
                    Require(rejected && pair.Changed(), "A missing Canvas was not rejected as a latched reference violation");
                }
            });
            test("census binds exact UI source without actor-root exemption", () =>
            {
                ContextType(); using (var pair = new Pair())
                {
                    var census = RPGKarawara.DevTools.Performance.PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                    var field = census.report.GetType().GetField("uiIcons"); Require(field != null, "Census UI source report missing");
                    var rows = ((System.Collections.IEnumerable)field.GetValue(census.report)).Cast<object>().ToArray();
                    Require(rows.Length == 1 && (bool)rows[0].GetType().GetField("supportedBinding").GetValue(rows[0]), "Exact UI creator was not bound");
                    Require(!census.report.unclassifiedAnimators.Any(w => w.componentId == pair.animator.GetInstanceID()), "Bound icon Animator remains unclassified");
                    Require(!census.report.actors.Any(a => a.objectId == pair.root.GetInstanceID() || a.objectId == pair.creatorRoot.GetInstanceID()) && !census.report.creationPathsVerified, "UI was miscounted as an actor or certified creation closure");
                }
            });
            test("disabled process releases UI source owner", () =>
            {
                var field = typeof(RPGKarawara.DevTools.Performance.PerformanceTutorialProcess).GetField("uiContext", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(field != null, "Standalone owner does not own UI context");
                string temp = Path.Combine(Path.GetTempPath(), "Karawara-ui-owner-" + Guid.NewGuid().ToString("N"));
                using (var pair = new Pair())
                {
                    pair.Bind(); var owner = new GameObject("Owned UI process teardown"); owner.SetActive(false);
                    try
                    {
                        var process = owner.AddComponent<RPGKarawara.DevTools.Performance.PerformanceTutorialProcess>();
                        Action<string, object> set = (name, value) => process.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(process, value);
                        set("initialized", true); set("personalRoot", Path.Combine(temp, "personal-unused")); set("personalBefore", new Dictionary<string, string>());
                        set("outputPath", Path.Combine(temp, "terminal.json")); set("request", new RPGKarawara.DevTools.Performance.PerformanceProcessResponse { saveRoot = Path.Combine(temp, "saves-unused") });
                        field.SetValue(process, pair.context); owner.SetActive(true); process.enabled = false;
                        Require((bool)ContextType().GetProperty("IsDisposed").GetValue(pair.context), "Disabled process retained UI source ownership");
                        var report = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(temp, "terminal.json")));
                        Require((bool)report["completed"] && !(bool)report["passed"], "Interrupted UI owner did not export failure");
                    }
                    finally { Object.DestroyImmediate(owner); if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                }
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-minimap-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            Action finish = () => File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true,
                passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, populationVerified = false,
                boundary = "Owned UI callbacks and real coroutine/destruction timing; not production route or complete UI/native component closure" }, Formatting.Indented));
            Pair fading = null;
            try { ContextType(); fading = new Pair(); fading.Bind(); fading.icon.SpawnedDelayed(0); }
            catch (Exception error) { fading?.Dispose(); cases.Add(new { name = "real fade/delayed destroy", passed = false, detail = error.ToString() }); finish(); Debug.Log("Minimap fixture: " + path); return; }
            double deadline = EditorApplication.timeSinceStartup + 8; bool destroying = false;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                try
                {
                    if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("Actual UI coroutine/Destroy timeout or interrupted PlayMode");
                    fading.Call("Poll");
                    if (!destroying && fading.animator.enabled && fading.icon.m_CanvasGroup.alpha >= 0.99f)
                    {
                        Require(!fading.Changed(), "Real fade during warming invalidated UI source");
                        cases.Add(new { name = "real fade coroutine", passed = true, detail = "Actual SpawnedDelayed coroutine enabled Animator and completed alpha" });
                        fading.Freeze(); fading.icon.DestroyIn = 0.05f; fading.icon.DestroyIcon(false, null); destroying = true;
                    }
                    else if (destroying && fading.root == null)
                    {
                        Require(fading.Changed(), "Actual delayed Destroy was not latched");
                        Require(!(bool)ContextType().GetProperty("SourceFaulted").GetValue(fading.context), "Normal icon destruction was misreported as an internal source fault");
                        cases.Add(new { name = "actual delayed icon destruction", passed = true, detail = "Actual DestroyIcon delayed removal invalidated frozen UI" });
                        test("census after delayed icon destruction with live creator", () =>
                        {
                            Require(fading.creator != null && fading.creator.IconInstance == null, "Actual retained destroyed-icon state required");
                            var census = RPGKarawara.DevTools.Performance.PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                            Require(census.report.uiIcons.Length == 0, "Destroyed icon remained a live UI binding");
                        });
                        EditorApplication.update -= tick; fading.Dispose(); finish();
                    }
                }
                catch (Exception error) { EditorApplication.update -= tick; fading.Dispose(); cases.Add(new { name = "real fade/delayed destroy", passed = false, detail = error.ToString() }); finish(); }
            };
            EditorApplication.update += tick; Debug.Log("Minimap fixture running: " + path);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
