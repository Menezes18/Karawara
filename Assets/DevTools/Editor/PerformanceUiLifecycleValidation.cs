using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceUiLifecycleValidation
    {
        static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance." + name)).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException(name + " missing");
        sealed class Pair : IDisposable
        {
            public GameObject root, imageRoot;
            public LoadScene owner;
            public MonoBehaviour boundOwner;
            public Animator animator;
            public Component witness;
            public IDisposable context;
            public Pair(bool orphan = false, string kind = "loading")
            {
                root = new GameObject("Owned UI lifecycle source", typeof(RectTransform)); root.SetActive(false);
                if (!orphan) root.AddComponent<Canvas>();
                if (kind == "loading") { owner = root.AddComponent<LoadScene>(); boundOwner = owner; }
                imageRoot = new GameObject("Declared loading image", typeof(RectTransform)); imageRoot.transform.SetParent(root.transform, false);
                var go = new GameObject("Animated loading child", typeof(RectTransform), typeof(Image), typeof(Animator)); go.transform.SetParent(imageRoot.transform, false); animator = go.GetComponent<Animator>();
                witness = go.AddComponent(Type("PerformanceUiSourceWitness")); witness.GetType().GetField("animator").SetValue(witness, animator); if (owner != null) owner.image = imageRoot;
                if (kind == "skill") { var skill = root.AddComponent<FadeImages>(); skill.enabled = false; boundOwner = skill; skill.galhoFadeCanvas = root.AddComponent<CanvasGroup>(); skill.leafFadeGalho = skill.galhoFadeCanvas; skill.painel = skill.galhoFadeCanvas; skill.fillImage = animator.GetComponent<Image>(); skill.fillIcon = skill.fillImage; }
                if (kind == "fade") { var fade = root.AddComponent<FadeController>(); boundOwner = fade; fade.animator = animator; animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Scripts/Utilitarios/FadeController.controller"); }
                if (kind == "damage") { var damage = root.AddComponent<System.MiniMap.MiniMapUI>(); boundOwner = damage; damage.hitAnimator = animator; animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Scripts/Minimap/Art/Animation/MiniMap - Damage.controller"); }
                root.SetActive(true);
            }
            public void Bind() => context = (IDisposable)Type("PerformanceUiSourceContext").GetMethod("Begin").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, new MonoBehaviour[] { boundOwner } });
            public void Call(string name) => Type("PerformanceUiSourceContext").GetMethod(name).Invoke(context, null);
            public bool Changed()
            {
                var result = new CaptureResult { conditions = new CaptureConfig { enemyCount = 17 } };
                Type("PerformanceUiSourceContext").GetMethod("Validate").Invoke(context, new object[] { result });
                Require(result.conditions.enemyCount == 17 && !result.populationVerified && !result.comparisonValid, "UI altered actors or granted certification");
                return result.conditionsChanged;
            }
            public void Dispose() { context?.Dispose(); if (root != null) Object.DestroyImmediate(root); }
        }
        static void Require(bool yes, string reason) { if (!yes) throw new InvalidOperationException(reason); }
        [MenuItem("Karawara/Validation/TOBE/Validate Other UI Lifecycle (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required");
            var cases = new List<object>();
            foreach (string change in new[] { "baseline", "orphan", "warming-visibility", "frozen-visibility", "loading-api-roundtrip", "witness-toggle", "reparent-roundtrip", "unknown-transient-source", "destroy-animator", "destroy-graphic", "owner-reference", "controller", "owner-enabled", "owner-roundtrip", "warming-destroy", "canvas-removal", "dispose", "freeze-twice", "duplicate-owner", "census-binding", "no-opt-in" })
            {
                try
                {
                    using (var pair = new Pair(change == "orphan"))
                    {
                        var type = Type("PerformanceUiSourceContext");
                        if (change == "no-opt-in") { Require(type.GetMethod("Begin").Invoke(null, new object[] { null, null }) == null, "No flag must be a no-op"); cases.Add(new { name = change, passed = true }); continue; }
                        if (change == "duplicate-owner")
                        {
                            bool rejected = false; try { ((IDisposable)type.GetMethod("Begin").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, new MonoBehaviour[] { pair.owner, pair.owner } })).Dispose(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; }
                            Require(rejected, "Duplicate source owner admitted"); cases.Add(new { name = change, passed = true }); continue;
                        }
                        pair.Bind();
                        if (change == "warming-visibility") { pair.owner.LoadGame(); pair.owner.desativado(); pair.owner.LoadGame(); Require(!pair.Changed(), "Expected warming visibility/API change rejected"); }
                        if (change == "warming-destroy") { Object.DestroyImmediate(pair.animator); Require(pair.Changed(), "Warming reference destruction hidden"); cases.Add(new { name = change, passed = true }); continue; }
                        pair.Call("Freeze");
                        if (change == "frozen-visibility") { pair.imageRoot.SetActive(false); pair.imageRoot.SetActive(true); }
                        if (change == "loading-api-roundtrip") { pair.owner.LoadGame(); pair.owner.desativado(); pair.owner.LoadGame(); }
                        if (change == "witness-toggle") { ((Behaviour)pair.witness).enabled = false; ((Behaviour)pair.witness).enabled = true; }
                        if (change == "reparent-roundtrip") { pair.animator.transform.SetParent(pair.root.transform); pair.animator.transform.SetParent(pair.imageRoot.transform); }
                        if (change == "unknown-transient-source") { var clone = Object.Instantiate(pair.animator.gameObject); Object.DestroyImmediate(clone); }
                        if (change == "destroy-animator") Object.DestroyImmediate(pair.animator);
                        if (change == "destroy-graphic") Object.DestroyImmediate(pair.animator.GetComponent<Image>());
                        if (change == "owner-reference") pair.owner.image = null;
                        if (change == "controller") pair.animator.enabled = !pair.animator.enabled;
                        if (change == "owner-roundtrip") { pair.owner.enabled = false; pair.owner.enabled = true; }
                        if (change == "owner-enabled") pair.owner.enabled = !pair.owner.enabled;
                        if (change == "canvas-removal") Object.DestroyImmediate(pair.root.GetComponent<Canvas>());
                        if (change == "dispose") { pair.context.Dispose(); Require((bool)type.GetProperty("IsDisposed").GetValue(pair.context), "Context retained callbacks"); pair.imageRoot.SetActive(false); pair.imageRoot.SetActive(true); cases.Add(new { name = change, passed = true }); continue; }
                        if (change == "freeze-twice") { bool rejected = false; try { pair.Call("Freeze"); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; } Require(rejected, "Rebase after freeze admitted"); }
                        if (change == "census-binding")
                        {
                            var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                            Require(!census.report.unclassifiedAnimators.Any(a => a.componentId == pair.animator.GetInstanceID()) && !census.report.creationPathsVerified, "Exact witnessed source was omitted or globally certified");
                        }
                        bool expected = !new[] { "baseline", "orphan", "warming-visibility", "freeze-twice", "census-binding" }.Contains(change);
                        Require(pair.Changed() == expected, "Source change not latched or stable source rejected");
                        if (!expected)
                        {
                            var poll = (Action)Delegate.CreateDelegate(typeof(Action), pair.context, type.GetMethod("Poll")); for (int i = 0; i < 64; i++) poll();
                            long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) poll(); Require(before == GC.GetAllocatedBytesForCurrentThread(), "Warmed polling allocated managed bytes");
                        }
                    }
                    cases.Add(new { name = change, passed = true });
                }
                catch (Exception error) { cases.Add(new { name = change, passed = false, detail = error.ToString() }); }
            }
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true }); } catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); } };
            foreach (string kind in new[] { "skill", "fade", "damage" })
                test(kind + " actual API", () =>
                {
                    using (var pair = new Pair(false, kind))
                    {
                        pair.Bind(); pair.Call("Freeze");
                        if (kind == "skill") ((FadeImages)pair.boundOwner).Reiniciar();
                        if (kind == "fade") ((FadeController)pair.boundOwner).FadeOut();
                        if (kind == "damage") ((System.MiniMap.MiniMapUI)pair.boundOwner).DoHitEffect();
                        Require(pair.Changed(), "Actual source API did not latch");
                        Require(!(bool)Type("PerformanceUiSourceContext").GetProperty("SourceFaulted").GetValue(pair.context), "Expected source API was misreported as internal fault");
                        var checkpoint = Newtonsoft.Json.Linq.JObject.FromObject(Type("PerformanceUiSourceContext").GetMethod("CaptureCheckpoint").Invoke(pair.context, new object[] { "actual-api" }));
                        int expected = kind == "skill" ? 7 : kind == "fade" ? 9 : 10;
                        Require(checkpoint["events"].Any(e => (int)e["kind"] == expected && (bool)e["frozen"]), "Checkpoint omitted actual API kind/phase");
                    }
                });
            test("process disposal", () =>
            {
                using (var pair = new Pair())
                {
                    pair.Bind(); var owner = new GameObject("Owned UI process fixture"); owner.SetActive(false);
                    string temp = Path.GetFullPath("Builds/TOBE-QA/RunData/ui-lifecycle-dispose-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
                    try
                    {
                        var process = owner.AddComponent<PerformanceTutorialProcess>();
                        Action<string, object> set = (name, value) => process.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(process, value);
                        set("initialized", true); set("personalRoot", Path.Combine(temp, "personal-unused")); set("personalBefore", new Dictionary<string, string>());
                        set("outputPath", Path.Combine(temp, "terminal.json")); set("request", new PerformanceProcessResponse { saveRoot = Path.Combine(temp, "save-unused") });
                        set("uiSourceContext", pair.context); owner.SetActive(true); process.enabled = false;
                        Require((bool)Type("PerformanceUiSourceContext").GetProperty("IsDisposed").GetValue(pair.context), "Disabled process retained UI callbacks");
                    }
                    finally { Object.DestroyImmediate(owner); }
                }
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-ui-lifecycle-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases }, Formatting.Indented)); Debug.Log("Other UI lifecycle: " + path);
        }
        public static void RunCombatDamageContract()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required");
            var cases = new List<object>();
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var combat = UnityEngine.SceneManagement.SceneManager.CreateScene("CucaLoad");
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(combat);
            try
            {
                foreach (string mode in new[] { "allowed", "repeated", "wrong-mode", "unbound", "late-bind", "rebind", "wrong-event", "foreign-owner", "reference", "controller", "disabled", "overflow" })
                {
                    try
                    {
                        using (var pair = new Pair(false, "damage"))
                        {
                            pair.Bind(); var type = Type("PerformanceUiSourceContext");
                            var bind = type.GetMethod("BindCucaCombatDamageFeedback") ?? throw new InvalidOperationException("Explicit combat UI contract missing");
                            Action<string> configure = value => bind.Invoke(pair.context, new object[] { value });
                            Action<Action> reject = action => { bool rejected = false; try { action(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException || e.InnerException is InvalidOperationException) { rejected = true; } Require(rejected, "Invalid contract binding admitted"); };
                            if (mode == "wrong-mode") { reject(() => configure("tutorial-certified-route")); cases.Add(new { name = mode, passed = true, error = "" }); continue; }
                            if (mode != "unbound" && mode != "late-bind") configure("cuca-route-rehearsal");
                            if (mode == "rebind") { reject(() => configure("cuca-route-rehearsal")); cases.Add(new { name = mode, passed = true, error = "" }); continue; }
                            pair.Call("Freeze");
                            if (mode == "late-bind") { reject(() => configure("cuca-route-rehearsal")); cases.Add(new { name = mode, passed = true, error = "" }); continue; }
                            int count = mode == "overflow" ? 257 : mode == "repeated" ? 7 : 1;
                            for (int i = 0; i < count; i++) ((System.MiniMap.MiniMapUI)pair.boundOwner).DoHitEffect();
                            if (mode == "wrong-event") PerformanceUiSourceContext.NotifyOwner(pair.boundOwner, PerformanceUiSourceEvent.HiddenRequest);
                            if (mode == "foreign-owner") PerformanceUiSourceContext.NotifyOwner(pair.owner, PerformanceUiSourceEvent.HitEffect);
                            if (mode == "reference") ((System.MiniMap.MiniMapUI)pair.boundOwner).hitAnimator = null;
                            if (mode == "controller") pair.animator.runtimeAnimatorController = null;
                            if (mode == "disabled") pair.animator.enabled = false;
                            Require(pair.Changed() == (mode != "allowed" && mode != "repeated"), "Combat contract hid an unexpected change or rejected authored feedback");
                            var checkpoint = Newtonsoft.Json.Linq.JObject.FromObject(type.GetMethod("CaptureCheckpoint").Invoke(pair.context, new object[] { "combat-feedback" }));
                            if (mode == "allowed" || mode == "repeated")
                            {
                                Require((long)checkpoint["combatHitEffectCount"] == count && (int)checkpoint["combatDamageOwnerId"] == pair.boundOwner.GetInstanceID(), "Exact owner/activity evidence missing");
                                Require(checkpoint["events"].Count(e => (int)e["kind"] == 10 && (bool)e["combatAllowed"] && (bool)e["frozen"]) == count, "Actual callback provenance lost");
                            }
                        }
                        cases.Add(new { name = mode, passed = true, error = "" });
                    }
                    catch (Exception error) { cases.Add(new { name = mode, passed = false, error = error.ToString() }); }
                }
            }
            finally { UnityEngine.SceneManagement.SceneManager.SetActiveScene(original); UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(combat); }
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-combat-ui-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.Count == 12 && cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases }, Formatting.Indented));
            Debug.Log("Combat UI contract: " + path);
        }
        public static void RunRealFade()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required");
            var pair = new Pair(false, "skill"); var skill = (FadeImages)pair.boundOwner; skill.fadeDuration = .05f; skill.fillDuration = .02f; pair.Bind(); skill.Reiniciar();
            double deadline = EditorApplication.timeSinceStartup + 10; EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                bool done = false, passed = false; string error = null; object checkpoint = null;
                try
                {
                    if (!Application.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Real FadeImages completion");
                    pair.Call("Poll"); if (skill.enabled) return;
                    Require(!pair.Changed(), "Real automatic fade completion invalidated warming phase");
                    checkpoint = Type("PerformanceUiSourceContext").GetMethod("CaptureCheckpoint").Invoke(pair.context, new object[] { "actual-fade-completed-warming" });
                    var data = Newtonsoft.Json.Linq.JObject.FromObject(checkpoint);
                    Require(data["events"].Any(e => (int)e["kind"] == 2 && !(bool)e["frozen"]), "Actual automatic OnDisable event absent");
                    pair.Call("Freeze"); skill.Reiniciar(); Require(pair.Changed(), "Real frozen skill restart was lost"); passed = true; done = true;
                }
                catch (Exception e) { done = true; error = e.ToString(); }
                finally
                {
                    if (done)
                    {
                        EditorApplication.update -= tick; pair.Dispose();
                        string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-ui-lifecycle-real-fade-" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed, error, checkpoint }, Formatting.Indented));
                    }
                }
            };
            EditorApplication.update += tick;
        }
    }
}
