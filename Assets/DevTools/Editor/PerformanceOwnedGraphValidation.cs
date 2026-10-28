using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using RPGKarawara.SkillTree;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceOwnedGraphValidation
    {
        public static string RunProtocol()
        {
            var template = JObject.Parse(File.ReadAllText("Builds/TOBE-QA/RunData/skill-retention-8b8ba2ba3cd04760aceb1ee5efcd491e/config.json"));
            var root = Path.GetFullPath("Builds/TOBE-QA/FixtureData/owned-graph-protocol-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); var checks = new JArray();
            try
            {
                foreach (string name in new[] { "legacy", "owned-ten", "owned-twenty", "owned-wrong-route", "legacy-wrong-route", "integer-flag", "mixed-snapshot-control", "explicit-false", "owned-stale-v1" })
                {
                    var config = (JObject)template.DeepClone(); var p = (JObject)config["skillRetention"];
                    p["outputRoot"] = Path.Combine(root, "output"); p["cycles"] = name == "owned-twenty" ? 20 : 10;
                    if (name != "legacy") p["ownedDefenseGraph"] = name != "explicit-false" && name != "legacy-wrong-route";
                    if (name.StartsWith("owned-") || name == "legacy-wrong-route" || name == "mixed-snapshot-control") p["routeId"] = "video1-skill-owned-graph-retention-v2";
                    if (name == "owned-stale-v1") p["routeId"] = "video1-skill-owned-graph-retention-v1";
                    if (name == "owned-wrong-route") p["routeId"] = "video1-skill-retention-v1";
                    if (name == "integer-flag") p["ownedDefenseGraph"] = 1;
                    if (name == "mixed-snapshot-control") { p["snapshotControl"] = true; p["cycles"] = 0; p["routeId"] = "video1-skill-snapshot-control-v1"; }
                    var path = Path.Combine(root, "config.json"); File.WriteAllText(path, config.ToString());
                    var response = RPGKarawara.DevTools.Performance.PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", Path.Combine(root, "saves") }, Path.Combine(root, "personal"));
                    bool expected = name == "legacy" || name == "owned-ten" || name == "owned-twenty" || name == "explicit-false";
                    checks.Add(new JObject { ["name"] = name, ["passed"] = response.success == expected && !Directory.Exists(Path.Combine(root, "saves")) && !Directory.Exists(Path.Combine(root, "output")), ["errors"] = JArray.FromObject(response.errors) });
                }
            }
            finally { File.Delete(Path.Combine(root, "config.json")); Directory.Delete(root); }
            var output = Path.GetFullPath("Docs/Validation/TOBE/Execution/owned-graph-protocol-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(output, FileMode.CreateNew)) using (var writer = new StreamWriter(file)) writer.Write(new JObject { ["passed"] = checks.All(c => (bool)c["passed"]), ["checks"] = checks }.ToString());
            return output;
        }
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit-mode owned-resource fixture required.");
            var authored = AssetDatabase.LoadAssetAtPath<DefenseSkill>("Assets/Scripts/SkillTree/Data/Escudo.asset");
            if (authored == null || authored.vfxPrefab == null) throw new InvalidOperationException("Actual authored defense source required.");
            var sourcePrefab = authored.vfxPrefab;
            var originalBindings = sourcePrefab.GetComponentsInChildren<VisualEffect>(true).Select(v => v.visualEffectAsset).ToArray();
            if (originalBindings.Length == 0 || originalBindings.Any(v => v == null)) throw new InvalidOperationException("Populated authored VFX bindings required.");
            var checks = new JArray();
            Action<bool, string> require = (condition, reason) => { if (!condition) throw new InvalidOperationException(reason); };
            Func<Type> scopeType = () => typeof(RPGKarawara.DevTools.Performance.CaptureResult).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceOwnedDefenseGraph", true);
            Func<DefenseSkill, Transform, IDisposable> open = (skill, owner) => (IDisposable)scopeType().GetMethod("Open", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { skill, owner });
            Func<IDisposable, string, object> property = (scope, name) => scope.GetType().GetProperty(name).GetValue(scope, null);
            Action<string, Action<DefenseSkill, GameObject>> test = (name, action) =>
            {
                var skill = Object.Instantiate(authored); var owner = new GameObject("Owned graph fixture owner"); owner.SetActive(false);
                try { action(skill, owner); checks.Add(new JObject { ["name"] = name, ["passed"] = true }); }
                catch (Exception error) { checks.Add(new JObject { ["name"] = name, ["passed"] = false, ["error"] = error.ToString() }); }
                finally { if (skill != null) Object.DestroyImmediate(skill); if (owner != null) Object.DestroyImmediate(owner); }
            };
            test("source remains authored while only diagnostic skill receives an inactive prototype", (skill, owner) =>
            {
                using (var scope = open(skill, owner.transform))
                {
                    var prototype = (GameObject)property(scope, "Prototype");
                    require(prototype != null && skill.vfxPrefab == prototype && prototype != sourcePrefab, "Prototype not bound exclusively to diagnostic skill.");
                    require(!prototype.activeInHierarchy && prototype.activeSelf == sourcePrefab.activeSelf, "Prototype activation semantics changed.");
                    require(authored.vfxPrefab == sourcePrefab && originalBindings.SequenceEqual(sourcePrefab.GetComponentsInChildren<VisualEffect>(true).Select(v => v.visualEffectAsset)), "Authored source bindings changed.");
                    var copied = (VisualEffectAsset[])property(scope, "Graphs");
                    require(copied.Length == originalBindings.Distinct().Count() && copied.All(g => g != null && !originalBindings.Contains(g) && !EditorUtility.IsPersistent(g)), "Every unique source graph needs an owned runtime copy.");
                }
            });
            test("dispose restores prefab and destroys owned graphs and prototype", (skill, owner) =>
            {
                var scope = open(skill, owner.transform); var prototype = (GameObject)property(scope, "Prototype"); var graphs = (VisualEffectAsset[])property(scope, "Graphs");
                scope.Dispose(); require(skill.vfxPrefab == sourcePrefab && prototype == null && graphs.All(g => g == null), "Owned resources survived fixture disposal.");
            });
            test("dispose is idempotent and never destroys authored graph", (skill, owner) =>
            {
                var scope = open(skill, owner.transform); scope.Dispose(); scope.Dispose();
                require(skill.vfxPrefab == sourcePrefab && authored.vfxPrefab == sourcePrefab && originalBindings.All(g => g != null && EditorUtility.IsPersistent(g)), "Idempotent cleanup touched an authored object.");
            });
            test("missing prefab rejects before altering the skill", (skill, owner) =>
            {
                skill.vfxPrefab = null; bool rejected = false;
                try { using (open(skill, owner.transform)) { } } catch (TargetInvocationException error) { rejected = error.InnerException is ArgumentException; }
                require(rejected && skill.vfxPrefab == null, "Missing prefab not rejected safely.");
            });
            test("a second lease cannot treat a scene prototype as an authored prefab", (skill, owner) =>
            {
                using (var scope = open(skill, owner.transform))
                {
                    bool rejected = false; try { using (open(skill, owner.transform)) { } } catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                    require(rejected && skill.vfxPrefab == (GameObject)property(scope, "Prototype"), "Second scope altered the first lease.");
                }
            });
            test("foreign replacement is preserved and cleanup conflict recorded", (skill, owner) =>
            {
                var scope = open(skill, owner.transform); var foreign = new GameObject("Fixture foreign prefab"); foreign.SetActive(false);
                try { skill.vfxPrefab = foreign; scope.Dispose(); require(skill.vfxPrefab == foreign && (bool)property(scope, "CleanupConflict") && foreign != null, "Foreign owner's replacement overwritten/destroyed."); }
                finally { scope.Dispose(); Object.DestroyImmediate(foreign); }
            });
            test("control uses identical unbound prototype while borrowing and preserving source graphs", (skill, owner) =>
            {
                var method = scopeType().GetMethod("OpenControl", BindingFlags.Public | BindingFlags.Static);
                require(method != null, "Matched control observation scope missing.");
                using (var scope = (IDisposable)method.Invoke(null, new object[] { skill, owner.transform }))
                {
                    var prototype = (GameObject)property(scope, "Prototype");
                    require(skill.vfxPrefab == prototype && prototype != sourcePrefab && !prototype.activeInHierarchy && prototype.GetComponentsInChildren<VisualEffect>(true).All(v => v.visualEffectAsset == null), "Control must match treatment's unbound prototype.");
                    var cast = Object.Instantiate(prototype, owner.transform);
                    try
                    {
                        scope.GetType().GetMethod("BindCastGraphs").Invoke(scope, new object[] { cast });
                        require(cast.GetComponentsInChildren<VisualEffect>(true).All(v => originalBindings.Contains(v.visualEffectAsset)), "Control cast must borrow original source graph.");
                    }
                    finally { Object.DestroyImmediate(cast); }
                    scope.Dispose();
                    require(skill.vfxPrefab == sourcePrefab && originalBindings.All(g => g != null && EditorUtility.IsPersistent(g)), "Control destroyed borrowed resources.");
                }
            });
            test("binding can be restored immediately while a cast still owns the copied graph", (skill, owner) =>
            {
                var scope = open(skill, owner.transform); GameObject cast = null;
                try
                {
                    cast = Object.Instantiate((GameObject)property(scope, "Prototype"), owner.transform);
                    scope.GetType().GetMethod("BindCastGraphs").Invoke(scope, new object[] { cast });
                    var method = scope.GetType().GetMethod("RestoreBinding"); require(method != null, "Two-phase restore missing."); method.Invoke(scope, null);
                    require(skill.vfxPrefab == sourcePrefab && cast != null && ((VisualEffectAsset[])property(scope, "Graphs")).All(g => g != null), "Restoration destroyed a live cast's resources.");
                }
                finally { if (cast != null) Object.DestroyImmediate(cast); scope.Dispose(); }
            });
            test("prototype is graph-unbound and only the actual cast receives the owned graph", (skill, owner) =>
            {
                using (var scope = open(skill, owner.transform))
                {
                    var prototype = (GameObject)property(scope, "Prototype");
                    require(prototype.GetComponentsInChildren<VisualEffect>(true).All(v => v.visualEffectAsset == null), "Inactive prototype still binds an asset and may pin a native batch.");
                    var cast = Object.Instantiate(prototype, owner.transform);
                    try
                    {
                        var method = scope.GetType().GetMethod("BindCastGraphs"); require(method != null, "Actual-cast binding method missing."); method.Invoke(scope, new object[] { cast });
                        var graphs = (VisualEffectAsset[])property(scope, "Graphs");
                        require(cast.GetComponentsInChildren<VisualEffect>(true).All(v => graphs.Contains(v.visualEffectAsset)), "Actual cast missed owned graph.");
                    }
                    finally { Object.DestroyImmediate(cast); }
                }
            });
            var path = Path.GetFullPath("Docs/Validation/TOBE/Execution/owned-graph-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(file)) writer.Write(new JObject { ["passed"] = checks.All(c => (bool)c["passed"]), ["checks"] = checks, ["boundary"] = "Actual prefab/graph ownership and cleanup fixture only; no visual parity, standalone retention or G1B acceptance." }.ToString());
            return path;
        }
    }
}
