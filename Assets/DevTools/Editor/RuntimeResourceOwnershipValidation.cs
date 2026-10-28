using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class RuntimeResourceOwnershipValidation
    {
        sealed class Fixture : IDisposable
        {
            public string name;
            public Action verify;
            public readonly List<Object> owned = new List<Object>();
            public T Own<T>(T value) where T : Object { owned.Add(value); return value; }
            public void Dispose() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]); }
        }
        [MenuItem("Karawara/Validation/TOBE/Validate Runtime Resource Ownership (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required");
            var cases = new List<object>(); var pending = new List<Fixture>();
            Action<string, Action<Fixture>> add = (name, setup) =>
            {
                var fixture = new Fixture { name = name };
                try { setup(fixture); pending.Add(fixture); }
                catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); fixture.Dispose(); }
            };
            foreach (bool external in new[] { false, true })
                add("Corruption releases its material and preserves shared source; external=" + external, f =>
                {
                    var root = f.Own(new GameObject("Owned Corruption controller")); root.SetActive(false);
                    var group = f.Own(new GameObject("Owned dissolve group")); if (!external) group.transform.SetParent(root.transform);
                    var child = GameObject.CreatePrimitive(PrimitiveType.Cube); child.transform.SetParent(group.transform);
                    var renderer = child.GetComponent<Renderer>();
                    var shared = f.Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));
                    renderer.sharedMaterial = shared;
                    var source = root.AddComponent<Corruption>(); source.gameObjectsToDissolve = new List<GameObject> { group }; source.vfxEffects = new List<ParticleSystem>();
                    root.SetActive(true);
                    var method = typeof(Corruption).GetMethod("DissolveObjects", BindingFlags.Instance | BindingFlags.NonPublic);
                    method.Invoke(source, null); var clone = renderer.sharedMaterial; f.Own(clone);
                    Require(clone != shared, "Fixture did not create the actual source's material instance");
                    method.Invoke(source, null); Require(renderer.sharedMaterial == clone, "Repeated access replaced the existing material");
                    Object.DestroyImmediate(root);
                    f.verify = () => { Require(clone == null, "Destroyed Corruption retained its owned native material"); Require(shared != null, "Shared source material was destroyed");
                        if (external) Require(renderer != null && renderer.sharedMaterial == shared, "External renderer did not regain the original material"); };
                });
            foreach (bool repeated in new[] { false, true })
                add("AI state clones release and restore templates; repeated=" + repeated, f =>
                {
                    var root = f.Own(new GameObject("Owned inactive AI state lease")); root.SetActive(false);
                    var ai = root.AddComponent<AICharacterManager>();
                    ai.idle = f.Own(ScriptableObject.CreateInstance<IdleState>());
                    ai.pursueTarget = f.Own(ScriptableObject.CreateInstance<PursueTargetState>());
                    ai.combatStance = f.Own(ScriptableObject.CreateInstance<CombatStanceState>());
                    ai.attack = f.Own(ScriptableObject.CreateInstance<AttackState>());
                    var templates = new AIState[] { ai.idle, ai.pursueTarget, ai.combatStance, ai.attack };
                    var acquire = typeof(AICharacterManager).GetMethod("AcquireRuntimeStates", BindingFlags.Instance | BindingFlags.NonPublic);
                    var release = typeof(AICharacterManager).GetMethod("ReleaseRuntimeStates", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require(acquire != null && release != null, "AI state ownership boundary missing");
                    var clones = new List<AIState>();
                    for (int cycle = 0; cycle < (repeated ? 10 : 1); cycle++)
                    {
                        acquire.Invoke(ai, null);
                        var current = new AIState[] { ai.idle, ai.pursueTarget, ai.combatStance, ai.attack };
                        for (int i = 0; i < 4; i++) { Require(current[i] != null && current[i] != templates[i], "Runtime state did not clone its template"); clones.Add(f.Own(current[i])); }
                        release.Invoke(ai, null); release.Invoke(ai, null);
                        Require(ai.idle == templates[0] && ai.pursueTarget == templates[1] && ai.combatStance == templates[2] && ai.attack == templates[3], "Despawn did not restore reusable templates");
                    }
                    f.verify = () => { Require(clones.All(c => c == null), "Owned AI state clones survived release"); Require(templates.All(t => t != null), "Shared AI templates were destroyed"); };
                });
            add("Additional boss state clones share the AI ownership lifetime", f =>
            {
                var root = f.Own(new GameObject("Owned inactive additional boss state")); root.SetActive(false);
                var ai = root.AddComponent<AICharacterManager>();
                var source = f.Own(ScriptableObject.CreateInstance<BossSleepState>());
                var create = typeof(AICharacterManager).GetMethod("CreateRuntimeState", BindingFlags.Instance | BindingFlags.NonPublic);
                var release = typeof(AICharacterManager).GetMethod("ReleaseRuntimeStates", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(create != null && release != null, "Additional state ownership boundary missing");
                var clone = f.Own((BossSleepState)create.MakeGenericMethod(typeof(BossSleepState)).Invoke(ai, new object[] { source }));
                Require(clone != source, "Additional boss state was not cloned"); release.Invoke(ai, null);
                f.verify = () => { Require(clone == null && source != null, "Additional boss state lifetime is incorrect"); };
            });
            int after = Time.frameCount + 2; double deadline = EditorApplication.timeSinceStartup + 10;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                bool expired = !Application.isPlaying || EditorApplication.timeSinceStartup > deadline;
                if (!expired && Time.frameCount < after) return;
                EditorApplication.update -= tick;
                foreach (var fixture in pending)
                {
                    try { Require(!expired, "Delayed destruction fixture expired"); fixture.verify(); cases.Add(new { name = fixture.name, passed = true, detail = "passed" }); }
                    catch (Exception e) { cases.Add(new { name = fixture.name, passed = false, detail = e.ToString() }); }
                    finally { fixture.Dispose(); }
                }
                string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/runtime-resource-ownership-fixture-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true,
                    passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                    boundary = "Actual owned source material access and deferred destruction; inactive AI state boundaries only; standalone/memory/appearance verification separate" }, Formatting.Indented));
                Debug.Log("Runtime ownership fixture: " + path);
            };
            EditorApplication.update += tick;
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
