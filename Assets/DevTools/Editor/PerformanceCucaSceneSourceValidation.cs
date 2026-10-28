using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCucaSceneSourceValidation
    {
        static Type SourceType => typeof(Performance.PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceCucaSceneSource") ?? throw new InvalidOperationException("Strict Cuca scene source missing");
        static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<IDisposable> proofs = new List<IDisposable>();
        [MenuItem("Karawara/Validation/TOBE/Validate Cuca Scene Sources")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Cuca source fixture requires EditMode");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, error = "" }); } catch (Exception e) { cases.Add(new { name, passed = false, error = e.ToString() }); } };
            foreach (string kind in new[] { "NativeGeometry", "DamageGeometry", "LeverCamera", "DormantCutscene" })
            {
                string selected = kind;
                test("Typed source accepts " + selected, () => With(selected, f => { Require(Valid(f.source), "Valid typed source rejected"); Require(Matches(Freeze(f.source)), "Fresh cached proof rejected"); }));
                test("Cached proof is allocation free for " + selected, () => With(selected, f => { var proof = Freeze(f.source); var predicate = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), proof, proof.GetType().GetMethod("Matches", PrivateInstance)); for (int i = 0; i < 32; i++) Require(predicate(), "Warm proof failed"); long before = GC.GetAllocatedBytesForCurrentThread(); bool all = true; for (int i = 0; i < 128; i++) all &= predicate(); long bytes = GC.GetAllocatedBytesForCurrentThread() - before; Require(all && bytes == 0, "Cached Matches allocated " + bytes + " bytes or rejected stable source"); }));
                test("Unknown child script rejected for " + selected, () => With(selected, f => { f.child.AddComponent<AnimationTrigger>(); Require(!Valid(f.source), "Unknown child script swallowed"); }));
                test("Post-freeze component replacement rejected for " + selected, () => With(selected, f => { var proof = Freeze(f.source); var original = f.child.AddComponent<BoxCollider>(); Require(!Matches(proof), "Added component accepted"); Object.DestroyImmediate(original); Require(!Matches(proof), "Native-component violation was not latched"); }));
            }
            test("Geometry needs no Rigidbody", () => With("NativeGeometry", f => Require(f.root.GetComponent<Rigidbody>() == null && Valid(f.source), "Native geometry incorrectly requires Rigidbody")));
            test("Endpoint Cuca proof coexists without acquiring callback ownership", () => With("NativeGeometry", f =>
            {
                var original = Freeze(f.source);
                var endpoint = SourceType.GetMethod("FreezeEndpoint", PrivateInstance) ?? throw new InvalidOperationException("Nonobserving endpoint proof missing");
                var retained = (IDisposable)endpoint.Invoke(f.source, null); proofs.Add(retained);
                Require(Matches(retained), "Endpoint proof lost original structural guards");
                ((IDisposable)original).Dispose();
                var replacement = Freeze(f.source);
                Require(Matches(replacement) && Matches(retained), "Endpoint proof retained exclusive source callbacks");
                f.renderer.sharedMaterial = f.NewMaterial();
                Require(!Matches(retained), "Detached endpoint proof hid material/reference changes");
            }));
            test("Actual endpoint roster context preserves guards across observer handoff", () => With("NativeGeometry", f =>
            {
                var args = new object[] { new[] { "-karawaraPerformanceRoster" }, new Component[] { f.source }, new[] { "world-visual" }, Array.Empty<GameObject>(), false };
                var type = typeof(Performance.PerformanceActorRosterContext);
                var endpointFactory = type.GetMethod("CreateEndpointOnly", BindingFlags.Static | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Endpoint roster factory missing");
                using (var full = Performance.PerformanceActorRosterContext.Create((string[])args[0], (Component[])args[1], (string[])args[2], (GameObject[])args[3], false))
                using (var endpoint = (Performance.PerformanceActorRosterContext)endpointFactory.Invoke(null, args))
                {
                    var result = new Performance.CaptureResult(); endpoint.Validate(result); Require(!result.conditionsChanged && !result.comparisonValid && !result.populationVerified, "Endpoint context rejected or granted certification");
                    full.Dispose();
                    using (var post = Performance.PerformanceActorRosterContext.Create((string[])args[0], (Component[])args[1], (string[])args[2], (GameObject[])args[3], false))
                    {
                        f.renderer.sharedMaterial = f.NewMaterial(); result = new Performance.CaptureResult(); endpoint.Validate(result);
                        Require(result.conditionsChanged, "Actual endpoint context lost retained material guard");
                    }
                }
            }));
            test("Native geometry owns exact paired MeshCollider", () => With("NativeGeometry", f => { var collider = f.child.AddComponent<MeshCollider>(); collider.sharedMesh = f.child.GetComponent<MeshFilter>().sharedMesh; Require(Valid(f.source) && Matches(Freeze(f.source)), "Exact native collision mesh/filter/renderer pair rejected"); }));
            test("Native geometry collision mesh and enabled state are frozen", () =>
            {
                With("NativeGeometry", f => { var collider = f.child.AddComponent<MeshCollider>(); collider.sharedMesh = f.child.GetComponent<MeshFilter>().sharedMesh; var proof = Freeze(f.source); collider.sharedMesh = f.NewMesh(); Require(!Matches(proof), "Native collision mesh replacement accepted"); });
                With("NativeGeometry", f => { var collider = f.child.AddComponent<MeshCollider>(); collider.sharedMesh = f.child.GetComponent<MeshFilter>().sharedMesh; var proof = Freeze(f.source); collider.enabled = !collider.enabled; Require(!Matches(proof), "Native collision enablement drift accepted"); });
            });
            test("Animator transforms may advance", () => With("NativeGeometry", f => { var proof = Freeze(f.source); f.child.transform.localPosition = new Vector3(1, 2, 3); f.child.transform.localRotation = Quaternion.Euler(0, 20, 0); Require(Matches(proof), "Ordinary animation pose invalidated ownership"); }));
            test("Collider enabled state is frozen", () => With("DamageGeometry", f => { var proof = Freeze(f.source); f.shape.enabled = !f.shape.enabled; Require(!Matches(proof), "Trap collider enabled transition silently permitted"); }));
            test("Collider geometry and trigger are frozen", () => With("DamageGeometry", f => { var proof = Freeze(f.source); f.shape.isTrigger = !f.shape.isTrigger; f.shape.size *= 2; Require(!Matches(proof), "Trap geometry change accepted"); }));
            test("Authored zero-damage solid trap is valid", () => With("DamageGeometry", f => { f.damage.physicalDamage = 0; f.shape.isTrigger = false; Require(Valid(f.source) && Matches(Freeze(f.source)), "Authored Espinhos3 variant rejected"); }));
            test("Damage driver must bind local collider", () => With("DamageGeometry", f => { typeof(DamageCollider).GetField("damageCollider", PrivateInstance).SetValue(f.damage, f.child.AddComponent<BoxCollider>()); Require(!Valid(f.source), "Foreign damage collider accepted"); }));
            test("Damage values are frozen", () => With("DamageGeometry", f => { var proof = Freeze(f.source); f.damage.physicalDamage++; Require(!Matches(proof), "Damage configuration changed"); }));
            test("Missing declared Animator rejected", () => With("NativeGeometry", f => { Set(f.source, "animators", new Animator[] { null }); Require(!Valid(f.source), "Null authored Animator accepted"); }));
            test("Material replacement rejected", () => With("NativeGeometry", f => { var proof = Freeze(f.source); f.renderer.sharedMaterial = f.NewMaterial(); Require(!Matches(proof), "Renderer material replacement accepted"); }));
            test("Mesh replacement rejected", () => With("NativeGeometry", f => { var proof = Freeze(f.source); f.child.GetComponent<MeshFilter>().sharedMesh = f.NewMesh(); Require(!Matches(proof), "Native mesh replacement accepted"); }));
            test("Lever camera can omit listener", () => With("LeverCamera", f => Require(f.root.GetComponent<AudioListener>() == null && Valid(f.source), "Camera1/2 listener absence rejected")));
            test("Lever camera can retain listener", () => With("LeverCamera", f => { Set(f.source, "listener", f.root.AddComponent<AudioListener>()); Require(Valid(f.source) && Matches(Freeze(f.source)), "Camera3 listener variant rejected"); }));
            test("Lever driver camera identity must match", () => With("LeverCamera", f => { f.lever.cam = f.child.AddComponent<Camera>(); Require(!Valid(f.source), "Foreign driver camera accepted"); }));
            test("Lever target activity is frozen", () => With("LeverCamera", f => { var proof = Freeze(f.source); f.target.SetActive(!f.target.activeSelf); Require(!Matches(proof), "Lever target activation silently permitted"); }));
            test("Lever target rejects newly added script", () => With("LeverCamera", f => { var proof = Freeze(f.source); f.target.AddComponent<AnimationTrigger>(); Require(!Matches(proof), "External target inventory addition accepted"); }));
            test("Lever target tolerates exact tool witness only", () => With("LeverCamera", f => { var proof = Freeze(f.source); f.target.AddComponent<Performance.PerformanceActorLifetimeWitness>(); Require(Valid(f.source) && Matches(proof), "Exact observer witness invalidated authored target"); }));
            test("Lever camera settings are frozen", () => With("LeverCamera", f => { var proof = Freeze(f.source); f.camera.fieldOfView++; Require(!Matches(proof), "Camera projection drift accepted"); }));
            test("Animator update mode is frozen", () => With("LeverCamera", f => { var proof = Freeze(f.source); f.animator.updateMode = AnimatorUpdateMode.UnscaledTime; Require(!Matches(proof), "Lever unscaled camera transition accepted"); }));
            test("Dormant graph has no root Animator requirement", () => With("DormantCutscene", f => Require(f.root.GetComponent<Animator>() == null && Valid(f.source), "Compound cutscene incorrectly requires root Animator")));
            test("Dormant graph activation rejected and latched", () => With("DormantCutscene", f => { var proof = Freeze(f.source); f.root.SetActive(true); Require(!Matches(proof), "Dormant graph activation accepted"); f.root.SetActive(false); Require(!Matches(proof), "Dormant activation violation was rebased"); }));
            test("Dormant source cannot start active", () => With("DormantCutscene", f => { f.root.SetActive(true); Require(!Valid(f.source), "Active cutscene mislabeled dormant"); }));
            test("Dormant camera array mutation rejected", () => With("DormantCutscene", f => { var proof = Freeze(f.source); f.fight.cam[0] = f.fight.cam[1]; Require(!Matches(proof), "In-place cutscene camera array mutation accepted"); }));
            test("Dormant driver renderer refs are frozen", () => With("DormantCutscene", f => { var proof = Freeze(f.source); f.fight.materialCUCA = Array.Empty<SkinnedMeshRenderer>(); Require(!Matches(proof), "Cutscene renderer references changed"); }));
            test("Dormant lifetime delay is frozen", () => With("DormantCutscene", f => { var proof = Freeze(f.source); typeof(DestroyAfterTime).GetField("timeToDestroy", PrivateInstance).SetValue(f.lifetime, 99f); Require(!Matches(proof), "Dormant destroy schedule configuration changed"); }));
            test("Dormant cameras must be referenced by reviewed drivers", () => With("DormantCutscene", f => { f.fight.cam = new[] { f.fight.cam[0] }; Require(!Valid(f.source), "Unowned cutscene camera admitted"); }));
            test("Dormant trigger must reference exact graph", () => With("DormantCutscene", f => { f.trigger._cutScene = f.target; Require(!Valid(f.source), "Unrelated trigger granted dormant proof"); }));
            test("Proof owns exact lifecycle subjects", () => With("DamageGeometry", f => { var proof = Freeze(f.source); var owns = proof.GetType().GetMethod("Owns", PrivateInstance); Require((bool)owns.Invoke(proof, new object[] { f.damage }) && !(bool)owns.Invoke(proof, new object[] { f.container.transform }), "Typed driver lifecycle ownership missing or global"); }));
            foreach (string kind in new[] { "NativeGeometry", "DamageGeometry", "LeverCamera", "DormantCutscene" })
            {
                string selected = kind;
                test("Actual roster accepts and releases " + selected, () => With(selected, f => { using (var context = Context(new[] { f.source }, Array.Empty<GameObject>())) Require(context.ReadLivingHostileCount() == 0, "Visual source became hostile"); Require(Matches(Freeze(f.source)), "Context disposal retained registered proof"); }));
            }
            test("Failed roster actor creation releases earlier proof", () => With("NativeGeometry", f => { RejectContext(() => Context(new[] { f.source, f.container.transform }, Array.Empty<GameObject>())); Require(Matches(Freeze(f.source)), "Failed subsequent actor retained first proof registration"); }));
            test("Failed roster effect validation releases earlier proof", () => With("NativeGeometry", f => { RejectContext(() => Context(new[] { f.source }, new[] { f.target })); Require(Matches(Freeze(f.source)), "Failed effect retained first proof registration"); }));
            test("Dormant reviewed native lights particles and colliders accepted", () => With("DormantCutscene", f => { f.AddDormantNative(); Require(Valid(f.source) && Matches(Freeze(f.source)), "Reviewed dormant native workload rejected"); }));
            test("Dormant native light configuration is frozen", () => With("DormantCutscene", f => { f.AddDormantNative(); var proof = Freeze(f.source); f.light.intensity++; Require(!Matches(proof), "Dormant light drift accepted"); }));
            test("Dormant particle state is frozen", () => With("DormantCutscene", f => { f.AddDormantNative(); var proof = Freeze(f.source); var emission = f.particles.emission; emission.enabled = !emission.enabled; Require(!Matches(proof), "Dormant particle configuration drift accepted"); }));
            test("Dormant MeshCollider mesh is frozen", () => With("DormantCutscene", f => { f.AddDormantNative(); var proof = Freeze(f.source); f.meshCollider.sharedMesh = f.NewMesh(); Require(!Matches(proof), "Dormant collision mesh drift accepted"); }));
            test("Dormant native polling remains allocation free", () => With("DormantCutscene", f => { f.AddDormantNative(); var proof = Freeze(f.source); var predicate = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), proof, proof.GetType().GetMethod("Matches", PrivateInstance)); for (int i = 0; i < 32; i++) Require(predicate(), "Native warm proof failed"); long before = GC.GetAllocatedBytesForCurrentThread(); bool all = true; for (int i = 0; i < 128; i++) all &= predicate(); long bytes = GC.GetAllocatedBytesForCurrentThread() - before; Require(all && bytes == 0, "Dormant native polling allocated " + bytes + " bytes or rejected stable native source"); }));
            test("Dormant curve particles remain stable and allocation free", () => With("DormantCutscene", f => { f.AddDormantNative(); var main = f.particles.main; main.startLifetime = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 2)); var proof = Freeze(f.source); var predicate = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), proof, proof.GetType().GetMethod("Matches", PrivateInstance)); for (int i = 0; i < 32; i++) Require(predicate(), "Curve warm proof failed"); long before = GC.GetAllocatedBytesForCurrentThread(); bool all = true; for (int i = 0; i < 128; i++) all &= predicate(); long bytes = GC.GetAllocatedBytesForCurrentThread() - before; Require(all && bytes == 0, "Dormant curve polling allocated " + bytes + " bytes or rejected stable source"); }));
            test("Production state notification rejects restored same-frame state", () => With("LeverCamera", f => { var proof = Freeze(f.source); SourceType.GetMethod("NotifyStateChanged").Invoke(null, new object[] { f.lever }); Require(!Matches(proof), "Exact driver notification did not invalidate proof"); }));
            test("Native collision survives Unity static batching", () => With("NativeGeometry", f => {
                var collider = f.child.AddComponent<MeshCollider>(); collider.sharedMesh = f.child.GetComponent<MeshFilter>().sharedMesh;
                var second = new GameObject("second static surface"); second.transform.SetParent(f.root.transform, false); second.transform.localPosition = Vector3.right * 3;
                second.AddComponent<MeshFilter>().sharedMesh = f.NewMesh(); second.AddComponent<MeshRenderer>().sharedMaterial = f.renderer.sharedMaterial;
                StaticBatchingUtility.Combine(f.root);
                Require(f.renderer.isPartOfStaticBatch, "Fixture failed to create Unity static batching");
                Require(Valid(f.source) && Matches(Freeze(f.source)), "Static batching broke independently retained collision mesh ownership");
            }));
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-cuca-scene-source-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Typed Cuca source/proof fixture only; no actual route, population, rendering or timing acceptance" }, Formatting.Indented));
            Debug.Log("Cuca scene source fixture: " + path);
        }
        static void With(string kind, Action<Fixture> body) { int start = proofs.Count; try { using (var fixture = new Fixture(kind)) body(fixture); } finally { for (int i = proofs.Count - 1; i >= start; i--) { proofs[i].Dispose(); proofs.RemoveAt(i); } } }
        static bool Valid(Component source) => (bool)SourceType.GetMethod("IsValidSource", PrivateInstance).Invoke(source, null);
        static object Freeze(Component source) { var proof = SourceType.GetMethod("Freeze", PrivateInstance).Invoke(source, null); if (proof is IDisposable lifetime) proofs.Add(lifetime); return proof; }
        static bool Matches(object proof) => (bool)proof.GetType().GetMethod("Matches", PrivateInstance).Invoke(proof, null);
        static void Set(Component source, string field, object value) => SourceType.GetField(field).SetValue(source, value);
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static Performance.PerformanceActorRosterContext Context(Component[] actors, GameObject[] effects) => Performance.PerformanceActorRosterContext.Create(new[] { "-karawaraPerformanceRoster" }, actors, Enumerable.Repeat("world-visual", actors.Length).ToArray(), effects, false);
        static void RejectContext(Func<Performance.PerformanceActorRosterContext> create) { try { using (var context = create()) { } } catch (ArgumentException) { return; } throw new InvalidOperationException("Malformed later roster input accepted"); }
        sealed class Fixture : IDisposable
        {
            internal readonly GameObject container, root, child, target;
            internal readonly Component source;
            internal Animator animator;
            internal BoxCollider shape;
            internal DamageCollider damage;
            internal AnimationTrigger lever;
            internal Camera camera;
            internal Renderer renderer;
            internal cutSceneFight fight;
            internal CutSceneTrigger trigger;
            internal DestroyAfterTime lifetime;
            internal Light light;
            internal ParticleSystem particles;
            internal MeshCollider meshCollider;
            readonly List<Object> resources = new List<Object>();
            internal Fixture(string kind)
            {
                // Resolve the production contract before creating native fixture objects.
                Type type = SourceType;
                container = new GameObject("Cuca typed source fixture");
                root = Node("authored-root", container.transform); child = Node("visual", root.transform); target = Node("external-target", container.transform);
                if (kind == "DormantCutscene") root.SetActive(false);
                source = root.AddComponent(type); Set(source, "kind", Enum.Parse(type.GetField("kind").FieldType, kind));
                if (kind == "NativeGeometry" || kind == "DamageGeometry")
                {
                    animator = AddAnimator(root); shape = root.AddComponent<BoxCollider>(); shape.isTrigger = kind == "DamageGeometry";
                    child.AddComponent<MeshFilter>().sharedMesh = NewMesh(); renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = NewMaterial();
                    Set(source, "animators", new[] { animator }); Set(source, "shape", shape);
                    if (kind == "DamageGeometry") { damage = root.AddComponent<DamageCollider>(); typeof(DamageCollider).GetField("damageCollider", PrivateInstance).SetValue(damage, shape); damage.physicalDamage = 60; Set(source, "damage", damage); }
                }
                else if (kind == "LeverCamera")
                {
                    Object.DestroyImmediate(child); child = Node("native-child", root.transform);
                    animator = AddAnimator(root); camera = root.AddComponent<Camera>(); camera.enabled = false;
                    var data = root.AddComponent<UniversalAdditionalCameraData>(); lever = root.AddComponent<AnimationTrigger>(); lever.animator = animator; lever.cam = camera; lever.target = target;
                    var targetShape = target.AddComponent<BoxCollider>(); target.SetActive(false);
                    Set(source, "animators", new[] { animator }); Set(source, "camera", camera); Set(source, "cameraData", data); Set(source, "lever", lever); Set(source, "targetShape", targetShape);
                }
                else
                {
                    var audio = root.AddComponent<AudioSource>(); var listener = root.AddComponent<AudioListener>();
                    var first = Node("camera-a", root.transform); var second = Node("camera-b", root.transform);
                    var cameraA = first.AddComponent<Camera>(); var cameraB = second.AddComponent<Camera>(); first.AddComponent<UniversalAdditionalCameraData>(); second.AddComponent<UniversalAdditionalCameraData>();
                    var a = AddAnimator(first); var b = AddAnimator(second); lifetime = second.AddComponent<DestroyAfterTime>();
                    var moved = Node("moving-character", root.transform); var stopped = Node("stopped-character", root.transform);
                    var characterAnimator = AddAnimator(moved); var skin = moved.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = NewMesh(); skin.sharedMaterial = NewMaterial(); renderer = skin;
                    var cab = Node("separate-visual", root.transform); var cabAnimator = AddAnimator(cab);
                    trigger = target.AddComponent<CutSceneTrigger>(); trigger._cutScene = root; trigger._player = Node("actual-player", container.transform);
                    fight = moved.AddComponent<cutSceneFight>(); fight.cam = new[] { cameraA, cameraB }; fight.clip = Array.Empty<AudioClip>(); fight.audio = audio; fight.materialErode = Array.Empty<SkinnedMeshRenderer>(); fight.materialCUCA = new[] { skin }; fight.CucaDeFato = cab; fight.TainaraStop = stopped; fight.TainaraMove = moved; fight.trigger = trigger;
                    Set(source, "animators", new[] { a, b, characterAnimator, cabAnimator }); Set(source, "cameras", new[] { cameraA, cameraB }); Set(source, "cutscenes", new[] { fight }); Set(source, "lifetimes", new[] { lifetime }); Set(source, "audio", audio); Set(source, "listener", listener); Set(source, "trigger", trigger);
                }
            }
            static GameObject Node(string name, Transform parent) { var node = new GameObject(name); node.transform.SetParent(parent, false); return node; }
            Animator AddAnimator(GameObject owner) { var value = owner.AddComponent<Animator>(); var controller = new AnimatorController(); resources.Add(controller); value.runtimeAnimatorController = controller; return value; }
            internal Mesh NewMesh() { var value = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } }; resources.Add(value); return value; }
            internal Material NewMaterial() { var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); if (shader == null) throw new InvalidOperationException("Fixture shader unavailable"); var value = new Material(shader); resources.Add(value); return value; }
            internal void AddDormantNative()
            {
                var geometry = Node("collision-geometry", root.transform); geometry.AddComponent<MeshFilter>().sharedMesh = NewMesh(); var surface = geometry.AddComponent<MeshRenderer>(); surface.sharedMaterial = NewMaterial(); meshCollider = geometry.AddComponent<MeshCollider>(); meshCollider.sharedMesh = geometry.GetComponent<MeshFilter>().sharedMesh; geometry.AddComponent<BoxCollider>().size = Vector3.one;
                var lightObject = Node("dormant-light", root.transform); light = lightObject.AddComponent<Light>(); lightObject.AddComponent<UniversalAdditionalLightData>();
                var particleObject = Node("dormant-particles", root.transform); particles = particleObject.AddComponent<ParticleSystem>(); particleObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = NewMaterial();
            }
            public void Dispose() { if (container != null) Object.DestroyImmediate(container); foreach (var resource in resources) if (resource != null) Object.DestroyImmediate(resource); }
        }
    }
}
