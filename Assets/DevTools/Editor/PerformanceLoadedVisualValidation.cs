using System;
using System.Collections;
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
    public static class PerformanceLoadedVisualValidation
    {
        static readonly string[] Args = { "-karawaraPerformanceRoster" };
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static bool Running { get; private set; }
        public static string LastReport { get; private set; }
        static Type ProofType() => typeof(PerformanceActorCensus).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceLoadedVisualProof") ?? throw new InvalidOperationException("Loaded visual proof missing");
        static bool Ready(WorldSaveGameManager owner) => (bool)ProofType().GetMethod("CanBind", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { owner });
        static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        static object Property(WorldSaveGameManager owner, string name) => typeof(WorldSaveGameManager).GetProperty(name)?.GetValue(owner) ?? throw new InvalidOperationException("Actual source property missing: " + name);
        static PerformanceActorRosterContext Context(GameObject root) => PerformanceActorRosterContext.Create(Args, new Component[] { root.GetComponent<Animator>() }, new[] { "world-visual" }, Array.Empty<GameObject>(), false);
        static void Invalid(PerformanceActorRosterContext context) { var result = new CaptureResult(); context.Validate(result); Require(result.conditionsChanged && result.errors.Count > 0 && !result.populationVerified, "Source mutation was not latched"); }
        public static void Run()
        {
            if (!Application.isPlaying || Running) throw new InvalidOperationException("One owned PlayMode load fixture required");
            var ownerRoot = new GameObject("Owned texture loader fixture"); ownerRoot.SetActive(false);
            var owner = ownerRoot.AddComponent<WorldSaveGameManager>(); owner.enabled = false;
            ownerRoot.SetActive(true); Running = true;
            owner.StartCoroutine(Execute(ownerRoot, owner));
        }
        static IEnumerator Execute(GameObject ownerRoot, WorldSaveGameManager owner)
        {
            var cases = new List<object>();
            Action<string, Action> test = (name, run) => { try { run(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            var prefab = new GameObject("Owned async prefab " + Guid.NewGuid().ToString("N")); prefab.SetActive(false);
            var animator = prefab.AddComponent<Animator>(); animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Data/Robsonexportandocoisas/Animais/Capivara_Animada.controller");
            var mesh = new Mesh(); mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }; mesh.triangles = new[] { 0, 1, 2 };
            prefab.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = prefab.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); renderer.sharedMaterial = material;
            var otherMaterial = new Material(material); var otherTexture = new Texture2D(1, 1);
            var data = ScriptableObject.CreateInstance<AssetData>(); data.prefab = prefab; data.texturePath = "Textures/MyTexture";
            var otherData = ScriptableObject.CreateInstance<AssetData>(); otherData.prefab = prefab; otherData.texturePath = data.texturePath;
            typeof(WorldSaveGameManager).GetField("assetData", Private).SetValue(owner, data);
            var operation = (IEnumerator)typeof(WorldSaveGameManager).GetMethod("InstantiatePrefabWithTextureAsync", Private).Invoke(owner, new object[] { prefab, data.texturePath });
            GameObject root = null;
            try
            {
                Require(operation.MoveNext(), "Actual loader must yield its real ResourceRequest");
                root = owner.QaLoadedWorldObject; Require(root != null && root != prefab, "Actual Instantiate source root missing");
                var current = root;
                test("Actual request is pending and cannot bind", () => Require(!Ready(owner), "Pending texture accepted"));
                test("Pending root is not swallowed by census", () => { var c = PerformanceActorCensus.Capture(Args).report; Require(!c.actors.Any(a => a.objectId == current.GetInstanceID() && a.supportedBinding) && c.unclassifiedAnimators.Any(a => a.objectId == current.GetInstanceID()), "Pending visual already exempted"); });
                test("Pending actual loader root cannot create a frozen context", () => { bool rejected = false; try { using (Context(current)) { } } catch (ArgumentException) { rejected = true; } Require(rejected, "Pending context accepted"); });
                yield return operation.Current;
                while (operation.MoveNext()) yield return operation.Current;
                var loadedTexture = Resources.Load<Texture2D>(data.texturePath);
                test("Actual application and source identity are exported", () => {
                    Require((bool)Property(owner, "QaWorldTextureApplied") && ReferenceEquals(Property(owner, "QaLoadedWorldAsset"), data) && ReferenceEquals(Property(owner, "QaLoadedWorldPrefab"), prefab) && ReferenceEquals(Property(owner, "QaLoadedWorldTexture"), loadedTexture), "Actual load/application identity differs");
                    Require(material.mainTexture == loadedTexture, "Actual production material application missing");
                });
                test("Renamed completed visual binds through exact retained source", () => { current.name = Guid.NewGuid().ToString(); Require(Ready(owner), "Completed source rejected"); var c = PerformanceActorCensus.Capture(Args).report; Require(c.actors.Any(a => a.objectId == current.GetInstanceID() && a.supportedBinding && a.role == "world-visual"), "Completed source absent from census"); });
                foreach (var change in new[] { "asset", "prefab", "path", "position", "controller", "mesh", "material", "texture", "renderer", "loader", "root" })
                {
                    string mutation = change;
                    test("Completed loaded visual latches " + mutation, () => {
                        var actualRenderer = current.GetComponent<MeshRenderer>(); var actualFilter = current.GetComponent<MeshFilter>(); var actualAnimator = current.GetComponent<Animator>();
                        var oldController = actualAnimator.runtimeAnimatorController; var oldPosition = data.position; var oldPath = data.texturePath;
                        try {
                            using (var context = Context(current)) using (var scope = PerformanceActorLifecycleScope.Begin(Args, context)) {
                                Require(context.ReadLivingHostileCount() == 0, "Initial source context changed");
                                if (mutation == "asset") typeof(WorldSaveGameManager).GetField("assetData", Private).SetValue(owner, otherData);
                                else if (mutation == "prefab") data.prefab = current;
                                else if (mutation == "path") data.texturePath = "Different/Resource";
                                else if (mutation == "position") data.position += Vector3.one;
                                else if (mutation == "controller") actualAnimator.runtimeAnimatorController = null;
                                else if (mutation == "mesh") actualFilter.sharedMesh = null;
                                else if (mutation == "material") actualRenderer.sharedMaterial = otherMaterial;
                                else if (mutation == "texture") material.mainTexture = otherTexture;
                                else if (mutation == "renderer") actualRenderer.enabled = false;
                                else if (mutation == "loader") { owner.enabled = true; owner.enabled = false; }
                                else { current.SetActive(true); current.SetActive(false); }
                                Invalid(context);
                                // Restore common public source fields while the violation remains latched.
                                data.prefab = prefab; data.texturePath = oldPath; data.position = oldPosition;
                                typeof(WorldSaveGameManager).GetField("assetData", Private).SetValue(owner, data);
                                actualAnimator.runtimeAnimatorController = oldController; actualFilter.sharedMesh = mesh; actualRenderer.sharedMaterial = material; material.mainTexture = loadedTexture; actualRenderer.enabled = true;
                                Invalid(context);
                            }
                        } finally { data.prefab = prefab; data.texturePath = oldPath; data.position = oldPosition; typeof(WorldSaveGameManager).GetField("assetData", Private).SetValue(owner, data); actualAnimator.runtimeAnimatorController = oldController; actualFilter.sharedMesh = mesh; actualRenderer.sharedMaterial = material; material.mainTexture = loadedTexture; actualRenderer.enabled = true; owner.enabled = false; current.SetActive(false); }
                    });
                }
                test("Loaded provenance poll allocates zero owned managed bytes after warm-up", () => { using (var context = Context(current)) { for (int i = 0; i < 200; i++) context.ReadLivingHostileCount(); long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 1000; i++) context.ReadLivingHostileCount(); Require(GC.GetAllocatedBytesForCurrentThread() == before, "Loaded proof poll allocated"); } });
            }
            finally
            {
                (operation as IDisposable)?.Dispose(); if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(prefab); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); Object.DestroyImmediate(otherMaterial); Object.DestroyImmediate(otherTexture); Object.DestroyImmediate(data); Object.DestroyImmediate(otherData);
                var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, populationVerified = false, routeAccepted = false, boundary = "Owned actual async request/application and frozen source checks only; no standalone or global G1B acceptance" };
                LastReport = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-loaded-visual-fixture-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(LastReport, JsonConvert.SerializeObject(report, Formatting.Indented)); Running = false; Debug.Log("Loaded visual fixture: " + LastReport); Object.Destroy(ownerRoot);
            }
        }
    }
}
