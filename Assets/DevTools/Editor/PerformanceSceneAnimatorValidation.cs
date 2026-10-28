using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RPGKarawara.DevTools.Performance;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceSceneAnimatorValidation
    {
        static readonly string[] Args = { "-karawaraPerformanceRoster" };
        static Type SourceType() => typeof(PerformanceActorCensus).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceSceneAnimatorSource") ?? throw new InvalidOperationException("Explicit scene Animator source is missing");
        static void Set(Component source, string field, object value) => source.GetType().GetField(field).SetValue(source, value);
        static bool Valid(Component source) => (bool)source.GetType().GetMethod("IsValidSource", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(source, null);
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static PerformanceActorRosterContext Context(Component source) => PerformanceActorRosterContext.Create(Args, new[] { source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false);
        static void Invalid(PerformanceActorRosterContext context)
        {
            var result = new CaptureResult(); context.Validate(result);
            Require(result.conditionsChanged && result.errors.Count > 0 && !result.populationVerified, "Changed source was not latched");
        }
        static void WithNative(Action<GameObject, Component> body)
        {
            var root = new GameObject("Owned native visual fixture"); root.SetActive(false);
            var mesh = new Mesh(); mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
            try
            {
                var visual = root.AddComponent<Animator>(); var filter = root.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>(); var rigid = root.AddComponent<Rigidbody>(); rigid.isKinematic = true;
                var collider = root.AddComponent<BoxCollider>(); var source = root.AddComponent(SourceType());
                Set(source, "kind", Enum.ToObject(SourceType().GetNestedType("Kind"), 1));
                Set(source, "animators", new[] { visual }); Set(source, "presentation", Array.Empty<MonoBehaviour>());
                Set(source, "meshFilter", filter); Set(source, "meshRenderer", renderer); Set(source, "body", rigid); Set(source, "shape", collider);
                root.SetActive(true); body(root, source);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
        }
        static void WithNpc(Action<GameObject, Component, UIProximity, NPCDialogue> body)
        {
            var root = new GameObject("Owned NPC presentation fixture"); root.SetActive(false);
            try
            {
                var animator = root.AddComponent<Animator>(); root.AddComponent<SkinnedMeshRenderer>();
                var child = new GameObject("Owned child visual"); child.transform.SetParent(root.transform); var childAnimator = child.AddComponent<Animator>();
                var actor = new GameObject("Separate dialogue actor"); actor.transform.SetParent(root.transform); var npc = actor.AddComponent<NPCDialogue>();
                var ui = new GameObject("Owned prompt", typeof(RectTransform)); ui.transform.SetParent(root.transform); var text = ui.AddComponent<TMPro.TextMeshProUGUI>();
                var driver = root.AddComponent<UIProximity>(); driver.uiElement = ui; driver.uiProximityText = text;
                var source = root.AddComponent(SourceType()); Set(source, "kind", Enum.ToObject(SourceType().GetNestedType("Kind"), 0));
                Set(source, "animators", new[] { animator, childAnimator }); Set(source, "presentation", new MonoBehaviour[] { text }); Set(source, "dialogue", npc); Set(source, "proximity", driver);
                root.SetActive(true); body(root, source, driver, npc);
            }
            finally { Object.DestroyImmediate(root); }
        }
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned runtime fixture requires PlayMode");
            var cases = new List<object>();
            Action<string, Action> test = (name, run) => { try { run(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            test("Exact native visual source enters census without a name exemption", () => WithNative((root, source) => {
                root.name = Guid.NewGuid().ToString(); Require(Valid(source), "Native source rejected");
                var census = PerformanceActorCensus.Capture(Args).report;
                Require(census.actors.Any(a => a.objectId == root.GetInstanceID() && a.supportedBinding && a.role == "world-visual"), "Source absent from supported census");
                Require(!census.unclassifiedAnimators.Any(a => a.objectId == root.GetInstanceID()), "Native Animator remains unknown");
                using (var context = Context(source)) Require(context.ReadLivingHostileCount() == 0, "Visual is not a hostile");
            }));
            test("Foreign or duplicate animator declaration rejected", () => WithNative((root, source) => {
                var visual = root.GetComponent<Animator>(); Set(source, "animators", new[] { visual, visual }); Require(!Valid(source), "Duplicate accepted");
                var foreign = new GameObject("Foreign"); try { Set(source, "animators", new[] { foreign.AddComponent<Animator>() }); Require(!Valid(source), "Foreign accepted"); } finally { Object.DestroyImmediate(foreign); }
            }));
            test("Undeclared descendant Animator is not swallowed", () => WithNative((root, source) => {
                var child = new GameObject("Unknown visual"); child.transform.SetParent(root.transform); child.AddComponent<Animator>();
                Require(!Valid(source), "Unknown descendant accepted"); Require(PerformanceActorCensus.Capture(Args).report.unclassifiedAnimators.Any(a => a.objectId == root.GetInstanceID()), "Malformed source swallowed its root Animator");
            }));
            test("Unknown script cannot be declared as presentation", () => WithNative((root, source) => {
                var other = root.AddComponent<UIProximity>(); Set(source, "presentation", new MonoBehaviour[] { other }); Require(!Valid(source), "Native gameplay script accepted");
            }));
            test("Missing-script declaration must match actual records", () => WithNative((root, source) => { Set(source, "missingScriptCount", 3); Require(!Valid(source), "Invented missing records accepted"); }));
            test("Changed mesh reference stays invalid after restoration", () => WithNative((root, source) => {
                using (var context = Context(source)) { var filter = root.GetComponent<MeshFilter>(); var old = filter.sharedMesh; filter.sharedMesh = null; Invalid(context); filter.sharedMesh = old; Invalid(context); }
            }));
            test("Changed native physics flags invalidate without changing pose", () => WithNative((root, source) => {
                using (var context = Context(source)) { root.GetComponent<Rigidbody>().isKinematic = false; Invalid(context); }
            }));
            test("Changed collider or renderer enabled state invalidates", () => WithNative((root, source) => {
                using (var context = Context(source)) { root.GetComponent<BoxCollider>().enabled = false; Invalid(context); }
            }));
            test("Changing serialized source declarations invalidates", () => WithNative((root, source) => {
                using (var context = Context(source)) { Set(source, "animators", Array.Empty<Animator>()); Invalid(context); }
            }));
            test("Actual source component toggle is latched between polls", () => WithNative((root, source) => {
                using (var context = Context(source)) using (var scope = PerformanceActorLifecycleScope.Begin(Args, context)) { ((Behaviour)source).enabled = false; ((Behaviour)source).enabled = true; Invalid(context); }
            }));
            test("Native proof sampling does not allocate after warm-up", () => WithNative((root, source) => {
                using (var context = Context(source)) { for (int i = 0; i < 200; i++) context.ReadLivingHostileCount(); long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount(); Require(GC.GetAllocatedBytesForCurrentThread() == before, "Source poll allocated managed bytes"); }
            }));
            test("NPC presentation and delegated dialogue have distinct lifecycle owners", () => WithNpc((root, source, driver, npc) => {
                Require(Valid(source), "NPC source rejected");
                var census = PerformanceActorCensus.Capture(Args).report;
                Require(census.actors.Count(a => (a.objectId == root.GetInstanceID() || a.objectId == npc.gameObject.GetInstanceID()) && a.supportedBinding) == 2, "Visual and dialogue not independently bound");
                using (var context = PerformanceActorRosterContext.Create(Args, new Component[] { source, npc }, new[] { "world-visual", "ambient-npc" }, Array.Empty<GameObject>(), false))
                using (var scope = PerformanceActorLifecycleScope.Begin(Args, context)) Require(context.ReadLivingHostileCount() == 0 && !scope.NotificationFaulted, "Ambiguous delegated owner");
            }));
            test("Undeclared NPC presentation script rejected", () => WithNpc((root, source, driver, npc) => {
                Set(source, "presentation", Array.Empty<MonoBehaviour>()); Require(!Valid(source), "Undeclared text accepted");
            }));
            test("Duplicate presentation reference rejected", () => WithNpc((root, source, driver, npc) => {
                Set(source, "presentation", new MonoBehaviour[] { driver.uiProximityText, driver.uiProximityText }); Require(!Valid(source), "Duplicate presentation accepted");
            }));
            test("Hidden additional actor cannot be swallowed by NPC visual", () => WithNpc((root, source, driver, npc) => {
                var hidden = new GameObject("Other actor"); hidden.transform.SetParent(root.transform); hidden.AddComponent<NPCDialogue>(); Require(!Valid(source), "Unknown actor accepted");
            }));
            test("Proximity disable/enable callback is latched between polls", () => WithNpc((root, source, driver, npc) => {
                using (var context = Context(source)) using (var scope = PerformanceActorLifecycleScope.Begin(Args, context)) { driver.enabled = false; driver.enabled = true; Invalid(context); }
            }));
            test("Proximity reference and radius changes invalidate", () => WithNpc((root, source, driver, npc) => {
                using (var context = Context(source)) { driver.detectionRadius += 1; Invalid(context); }
            }));
            test("Expected presentation visibility changes do not claim UI lifecycle closure", () => WithNpc((root, source, driver, npc) => {
                using (var context = Context(source)) { driver.uiElement.SetActive(false); Require(context.ReadLivingHostileCount() == 0, "UI visibility treated as actor removal"); var result = new CaptureResult(); context.Validate(result); Require(!result.populationVerified, "Visual binding granted global certification"); }
            }));
            foreach (var change in new[] { "mesh", "material", "shader", "texture", "renderer" })
                test("NPC renderer continuity rejects " + change, () => WithNpc((root, source, driver, npc) => {
                    var renderer = root.GetComponent<SkinnedMeshRenderer>(); var mesh = new Mesh(); var otherMesh = new Mesh();
                    var shader = Shader.Find("Universal Render Pipeline/Lit"); Require(shader != null, "Authored project shader missing");
                    var material = new Material(shader); var otherMaterial = new Material(shader); var texture = new Texture2D(1, 1);
                    try {
                        renderer.sharedMesh = mesh; renderer.sharedMaterial = material;
                        using (var context = Context(source)) {
                            if (change == "mesh") renderer.sharedMesh = otherMesh;
                            else if (change == "material") renderer.sharedMaterial = otherMaterial;
                            else if (change == "shader") { var otherShader = Shader.Find("Universal Render Pipeline/Unlit"); Require(otherShader != null && otherShader != shader, "Distinct project shader missing"); material.shader = otherShader; }
                            else if (change == "texture") material.mainTexture = texture;
                            else renderer.enabled = false;
                            Invalid(context);
                        }
                    } finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(otherMesh); Object.DestroyImmediate(material); Object.DestroyImmediate(otherMaterial); Object.DestroyImmediate(texture); }
                }));
            test("Actual nullable proximity text binding remains explicit", () => WithNpc((root, source, driver, npc) => { driver.uiProximityText = null; Require(Valid(source), "Production nullable text rejected"); using (var context = Context(source)) Require(context.ReadLivingHostileCount() == 0, "Nullable text changed source"); }));
            foreach (var transient in new[] { false, true })
                test("Renderer-only child active state is latched; transient=" + transient, () => WithNpc((root, source, driver, npc) => {
                    var child = new GameObject("Mesh-only child"); child.transform.SetParent(root.transform); child.AddComponent<SkinnedMeshRenderer>();
                    using (var context = Context(source)) using (var scope = PerformanceActorLifecycleScope.Begin(Args, context)) {
                        child.SetActive(false); if (transient) child.SetActive(true); Invalid(context); child.SetActive(true); Invalid(context);
                    }
                }));
            test("NPC renderer proof poll allocates no managed bytes after warm-up", () => WithNpc((root, source, driver, npc) => {
                using (var context = Context(source)) { for (int i = 0; i < 200; i++) context.ReadLivingHostileCount(); long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount(); Require(GC.GetAllocatedBytesForCurrentThread() == before, "NPC source poll allocated"); }
            }));
            foreach (var change in new[] { "none", "collider", "active" })
                test("Declared native mesh/hitbox child continuity: " + change, () => WithNative((root, source) => {
                    var child = new GameObject("Owned native child"); child.transform.SetParent(root.transform);
                    child.AddComponent<MeshFilter>().sharedMesh = root.GetComponent<MeshFilter>().sharedMesh; child.AddComponent<MeshRenderer>(); var shape = child.AddComponent<BoxCollider>();
                    Require(!Valid(source), "Undeclared native child accepted"); Set(source, "nativeParts", child.GetComponents<Component>()); Require(Valid(source), "Declared native child rejected");
                    using (var context = Context(source)) using (var scope = PerformanceActorLifecycleScope.Begin(Args, context)) {
                        if (change == "none") Require(context.ReadLivingHostileCount() == 0, "Unchanged native child rejected");
                        else if (change == "collider") { shape.isTrigger = true; Invalid(context); }
                        else { child.SetActive(false); child.SetActive(true); Invalid(context); }
                    }
                }));
            var report = new { utc = DateTime.UtcNow.ToString("O"), passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, populationVerified = false, routeAccepted = false, boundary = "Owned visual bindings only; actual Tutorial, UI ownership, missing content and route acceptance remain separate" };
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-scene-animator-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented)); Debug.Log("Scene Animator fixture: " + path);
        }
    }
}
