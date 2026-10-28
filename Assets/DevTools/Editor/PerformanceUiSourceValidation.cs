using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    // Compiles before the proposed inventory exists so the first run demonstrates RED.
    public static class PerformanceUiSourceValidation
    {
        static Type Inventory => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceUiSourceInventory")).FirstOrDefault(t => t != null) ?? throw new InvalidOperationException("UI source inventory missing");
        static Newtonsoft.Json.Linq.JArray Capture(params MonoBehaviour[] owners) => Newtonsoft.Json.Linq.JArray.FromObject(Inventory.GetMethod("Capture").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster" }, owners }));
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        [MenuItem("Karawara/Validation/TOBE/Validate Other UI Sources (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode scene required");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            foreach (var mode in new[] { "loading-orphan", "loading-two", "loading-empty", "loading-foreign", "loading-duplicate", "loading-no-image", "loading-reference-swap", "loading-destroyed", "skill-two", "skill-destroyed-branch", "skill-destroyed-secondary", "skill-outside-branch", "skill-missing-reference", "skill-foreign-reference", "damage-exact", "damage-ancestor-canvas", "damage-foreign", "fade-exact", "fade-foreign", "fade-missing-canvas", "no-flag", "duplicate-flag", "census-no-exemption" })
                test(mode, () =>
                {
                    var root = new GameObject("Owned UI source fixture", typeof(RectTransform)); root.SetActive(false);
                    GameObject foreign = null;
                    try
                    {
                        MonoBehaviour owner;
                        var canvas = root.AddComponent<Canvas>();
                        Func<Transform, Animator> animated = parent => { var go = new GameObject("Same duplicate name", typeof(RectTransform), typeof(Image), typeof(Animator)); go.SetActive(false); go.transform.SetParent(parent, false); return go.GetComponent<Animator>(); };
                        var animator = animated(root.transform);
                        if (mode.StartsWith("loading") || mode.Contains("flag") || mode == "census-no-exemption")
                        {
                            var source = root.AddComponent<LoadScene>(); source.enabled = false; source.image = root; owner = source;
                            if (mode == "loading-orphan") Object.DestroyImmediate(canvas);
                            if (mode == "loading-two") animated(root.transform);
                            if (mode == "loading-empty" || mode == "loading-destroyed") Object.DestroyImmediate(animator.gameObject);
                            if (mode == "loading-foreign") { foreign = new GameObject("Foreign target"); source.image = foreign; }
                            if (mode == "loading-no-image") Object.DestroyImmediate(animator.GetComponent<Image>());
                            if (mode == "loading-duplicate") { Require(Capture(source, source).Count == 1 && !(bool)Capture(source, source)[0]["referenceValidated"], "Ambiguous declarations were accepted"); return; }
                            if (mode == "loading-reference-swap") { var old = Capture(source); source.image = null; Require(old.Count == 1 && Capture(source).Count == 0, "Changed loading owner reference was reused"); return; }
                        }
                        else if (mode.StartsWith("skill"))
                        {
                            var source = root.AddComponent<FadeImages>(); source.enabled = false; owner = source;
                            source.galhoFadeCanvas = root.AddComponent<CanvasGroup>(); source.leafFadeGalho = new GameObject("Leaf", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>(); source.leafFadeGalho.transform.SetParent(root.transform, false);
                            source.painel = source.leafFadeGalho; source.fillImage = animator.GetComponent<Image>(); source.fillIcon = source.fillImage;
                            if (mode == "skill-two") animated(root.transform);
                            if (mode == "skill-destroyed-branch") Object.DestroyImmediate(source.galhoFadeCanvas);
                            if (mode == "skill-destroyed-secondary") Object.DestroyImmediate(source.leafFadeGalho);
                            if (mode == "skill-outside-branch") { source.galhoFadeCanvas = source.leafFadeGalho; }
                            if (mode == "skill-missing-reference") source.fillIcon = null;
                            if (mode == "skill-foreign-reference") { foreign = new GameObject("Foreign image", typeof(RectTransform), typeof(Image)); source.fillIcon = foreign.GetComponent<Image>(); }
                        }
                        else if (mode.StartsWith("damage"))
                        {
                            var source = root.AddComponent<System.MiniMap.MiniMapUI>(); source.enabled = false; owner = source; source.hitAnimator = animator;
                            if (mode == "damage-ancestor-canvas") { Object.DestroyImmediate(canvas); foreign = new GameObject("Owned outer canvas", typeof(RectTransform), typeof(Canvas)); foreign.SetActive(false); root.transform.SetParent(foreign.transform, false); }
                            if (mode == "damage-foreign") { foreign = new GameObject("Foreign Animator", typeof(Animator)); source.hitAnimator = foreign.GetComponent<Animator>(); }
                        }
                        else
                        {
                            var source = root.AddComponent<FadeController>(); source.enabled = false; owner = source; source.animator = animator;
                            if (mode == "fade-foreign") { foreign = new GameObject("Foreign Animator", typeof(Animator)); source.animator = foreign.GetComponent<Animator>(); }
                            if (mode == "fade-missing-canvas") Object.DestroyImmediate(canvas);
                        }
                        if (mode == "no-flag") { Require(((Array)Inventory.GetMethod("Capture").Invoke(null, new object[] { null, new[] { owner } })).Length == 0, "Inventory activated without opt-in"); return; }
                        if (mode == "duplicate-flag") { bool rejected = false; try { Inventory.GetMethod("Capture").Invoke(null, new object[] { new[] { "-karawaraPerformanceRoster", "-karawaraPerformanceRoster" }, new[] { owner } }); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { rejected = true; } Require(rejected, "Duplicate activation accepted"); return; }
                        var rows = Capture(owner);
                        if (mode == "census-no-exemption")
                        {
                            var census = RPGKarawara.DevTools.Performance.PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                            Require(census.report.unclassifiedAnimators.Any(a => a.componentId == animator.GetInstanceID()) && !census.report.creationPathsVerified, "Source inspection incorrectly exempted an uninstrumented UI animator");
                            var uiRows = Newtonsoft.Json.Linq.JObject.FromObject(census.report)["uiSources"];
                            Require(uiRows != null && uiRows.Any(a => (int)a["animatorId"] == animator.GetInstanceID()), "Census omitted source inspection"); return;
                        }
                        bool empty = mode == "loading-empty" || mode == "loading-destroyed" || mode == "loading-foreign" || mode == "skill-outside-branch" || mode == "skill-destroyed-branch";
                        Require(rows.Count == (empty ? 0 : mode.EndsWith("two") ? 2 : 1), "Incorrect exact branch/reference association");
                        if (empty) return;
                        bool valid = !mode.Contains("foreign") && !mode.Contains("missing") && mode != "loading-no-image" && mode != "skill-destroyed-secondary";
                        Require(rows.All(r => (bool)r["referenceValidated"] == valid && !(bool)r["lifecycleCertified"] && !(bool)r["renderingCertified"]), "Invalid source admitted or certification granted");
                        if (mode == "loading-orphan") Require((int)rows[0]["canvasId"] == 0 && (string)rows[0]["renderingStatus"] == "no-canvas", "Missing Canvas was hidden");
                        Require(rows.Select(r => (int)r["animatorId"]).Distinct().Count() == rows.Count, "Duplicate path lost instance identity");
                    }
                    finally { Object.DestroyImmediate(root); if (foreign != null) Object.DestroyImmediate(foreign); }
                });
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-ui-source-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases }, Formatting.Indented));
            Debug.Log("UI source fixture: " + path);
        }
    }
}
