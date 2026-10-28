using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceSkillRetentionValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Skill retention policy fixture requires EditMode.");
            var cases = new List<object>();
            string root = Path.GetFullPath("Builds/TOBE-QA/FixtureData/skill-retention-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string configPath = Path.Combine(root, "config.json"), save = Path.Combine(root, "saves"), output = Path.Combine(root, "output"), personal = Path.Combine(root, "personal");
            try
            {
                foreach (string scenario in new[] { "valid", "valid-twenty", "cycles", "nineteen-cycles", "twenty-one-cycles", "warmup-cycles", "warmup", "settle", "timeout", "missing-assets", "wrong-type", "duplicate-skill", "stale-native-id", "missing-bindings", "wrong-binding", "mixed-capture", "mixed-profiler", "mixed-world", "unknown-field", "unknown-skill-field", "shared-roots", "personal-output", "foreign-mode" })
                {
                    bool passed = false; string error = "";
                    try
                    {
                        var seed = JObject.Parse((string)JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/video1-heading-authored-skills-template.json"))["rehearsal"]["seedSaveJson"]);
                        string[] names = { "PetSpirit", "Escudo", "SkillJacare" };
                        string[] types = { "RPGKarawara.SkillTree.AttackSkill", "RPGKarawara.SkillTree.DefenseSkill", "RPGKarawara.SkillTree.SupportSkill" };
                        seed["qaSkillBindings"] = new JObject { ["skillsSlot"] = new JArray(names.Select((name, i) => new JObject { ["assetName"] = name, ["type"] = types[i] })) };
                        var protocol = new JObject { ["routeId"] = "video1-skill-retention-v1", ["outputRoot"] = output, ["seedSaveJson"] = seed.ToString(Formatting.None),
                            ["skillNames"] = JArray.FromObject(names), ["skillTypes"] = JArray.FromObject(types), ["cycles"] = 10, ["warmupCycles"] = 2, ["warmupSeconds"] = 30, ["settleSeconds"] = 3, ["cycleTimeoutSeconds"] = 90 };
                        var config = new JObject { ["mode"] = "skill-retention", ["width"] = 1920, ["height"] = 1080, ["quality"] = "High Fidelity", ["vsync"] = 0, ["targetFrameRate"] = -1, ["skillRetention"] = protocol };
                        switch (scenario)
                        {
                            case "cycles": protocol["cycles"] = 1; break;
                            case "valid-twenty": protocol["cycles"] = 20; break;
                            case "nineteen-cycles": protocol["cycles"] = 19; break;
                            case "twenty-one-cycles": protocol["cycles"] = 21; break;
                            case "warmup-cycles": protocol["warmupCycles"] = 0; break;
                            case "warmup": protocol["warmupSeconds"] = 0; break;
                            case "settle": protocol["settleSeconds"] = 0; break;
                            case "timeout": protocol["cycleTimeoutSeconds"] = 99999; break;
                            case "missing-assets": protocol["skillNames"] = new JArray(); break;
                            case "wrong-type": protocol["skillTypes"][0] = "Missing.Skill"; break;
                            case "duplicate-skill": protocol["skillNames"][1] = "PetSpirit"; break;
                            case "stale-native-id": seed["skillsSlot"][0]["instanceID"] = 19378; break;
                            case "missing-bindings": seed.Remove("qaSkillBindings"); break;
                            case "wrong-binding": seed["qaSkillBindings"]["skillsSlot"][0]["assetName"] = "ForeignSkill"; break;
                            case "mixed-capture": config["capture"] = new JObject(); break;
                            case "mixed-profiler": config["profilerAttribution"] = new JObject(); break;
                            case "mixed-world": config["retention"] = new JObject(); break;
                            case "unknown-field": config["retentionAccepted"] = true; break;
                            case "unknown-skill-field": protocol["retentionAccepted"] = true; break;
                            case "shared-roots": protocol["outputRoot"] = save; break;
                            case "personal-output": protocol["outputRoot"] = personal; break;
                            case "foreign-mode": config["mode"] = "video1-certified-route"; break;
                        }
                        protocol["seedSaveJson"] = seed.ToString(Formatting.None);
                        File.WriteAllText(configPath, config.ToString(Formatting.None));
                        var result = PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", configPath, "-karawaraSaveRoot", save }, personal);
                        passed = result.success == (scenario == "valid" || scenario == "valid-twenty") && !Directory.Exists(save) && !Directory.Exists(output) && result.result == null;
                        error = string.Join("; ", result.errors);
                    }
                    catch (Exception failure) { error = failure.ToString(); }
                    cases.Add(new { name = scenario, passed, detail = error });
                }
            }
            finally { if (File.Exists(configPath)) File.Delete(configPath); Directory.Delete(root); }
            foreach (bool overflow in new[] { false, true })
            {
                var container = new GameObject("Owned skill journal fixture"); container.SetActive(false);
                try
                {
                    var process = container.AddComponent<PerformanceSkillRetentionProcess>();
                    var child = new GameObject("Owned skill effect"); child.transform.SetParent(container.transform);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = process.GetType(); var add = type.GetMethod("AddEffect", flags);
                    int count = overflow ? 8193 : 1;
                    for (int i = 0; i < count; i++) add.Invoke(process, new object[] { child, false, 0, 1, 2 });
                    bool passed;
                    if (overflow) passed = (int)type.GetField("effectCount", flags).GetValue(process) == 8192 && (bool)type.GetField("overflow", flags).GetValue(process);
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(child); type.GetMethod("Sweep", flags).Invoke(process, null);
                        var record = ((Array)type.GetField("effects", flags).GetValue(process)).GetValue(0);
                        passed = ReferenceEquals(record.GetType().GetField("live").GetValue(record), null) && JObject.FromObject(record)["live"] == null;
                    }
                    cases.Add(new { name = overflow ? "Bounded journal latches overflow without growth" : "Destroyed effect reference cleared and excluded from DTO", passed, detail = "Owned numeric buffer fixture only" });
                }
                finally { UnityEngine.Object.DestroyImmediate(container); }
            }
            var provenanceContainer = new GameObject("Owned source provenance fixture"); provenanceContainer.SetActive(false);
            try
            {
                var process = provenanceContainer.AddComponent<PerformanceSkillRetentionProcess>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic; var type = process.GetType();
                var effectRoot = new GameObject("Separately created effect"); effectRoot.transform.SetParent(provenanceContainer.transform);
                type.GetMethod("AddEffect", flags).Invoke(process, new object[] { effectRoot, false, 0, 123, 456 });
                var record = JObject.FromObject(((Array)type.GetField("effects", flags).GetValue(process)).GetValue(0));
                cases.Add(new { name = "effectRoot difference preserves unknown actual prefab/creator", passed = record["creatorId"]?.Type == JTokenType.Null && record["prefabId"]?.Type == JTokenType.Null && (int?)record["activationSkillId"] == 123 && (int?)record["declaredSkillPrefabId"] == 456, detail = "Declared activation is separate from actual source provenance" });
                var shooter = effectRoot.AddComponent<FireballShooter>(); shooter.fireballPrefab = effectRoot;
                var register = type.GetMethod("RegisterShooter", flags);
                bool required = false, emitted = false, bothRequired = false;
                if (register != null)
                {
                    register.Invoke(process, new object[] { shooter });
                    required = (bool)type.GetField("projectileRequired", flags).GetValue(process) && !(bool)type.GetMethod("AllSourcesEmitted", flags).Invoke(process, null);
                    type.GetMethod("ProjectileCreated", flags).Invoke(process, new object[] { shooter, effectRoot, effectRoot });
                    emitted = (bool)type.GetMethod("AllSourcesEmitted", flags).Invoke(process, null);
                    var second = effectRoot.AddComponent<FireballShooter>(); second.fireballPrefab = effectRoot;
                    register.Invoke(process, new object[] { second });
                    bothRequired = !(bool)type.GetMethod("AllSourcesEmitted", flags).Invoke(process, null);
                }
                cases.Add(new { name = "Separately created projectile source requires its own emission", passed = required && emitted && bothRequired, detail = "Two live sources cannot share one observed emission" });
            }
            finally { UnityEngine.Object.DestroyImmediate(provenanceContainer); }
            ValidatePlateauProtocol(cases);
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/skill-retention-policy-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), passed = cases.All(row => (bool)row.GetType().GetProperty("passed").GetValue(row)), cases,
                boundary = "Explicit standalone policy only; no runtime activation, skill cycles, snapshot or retention acceptance." }, Formatting.Indented));
            Debug.Log("Skill retention policy fixture: " + path); return path;
        }
        static void ValidatePlateauProtocol(List<object> cases)
        {
            var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(PerformanceSkillRetentionProcess);
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "Controlled protocol policy only" }); } catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); } };
            Func<int, int, string> checkpoint = (completed, requested) =>
            {
                var method = type.GetMethod("SnapshotCheckpoint", flags);
                if (method == null) throw new InvalidOperationException("Controlled ten/twenty checkpoint scheduling is not implemented.");
                return (string)method.Invoke(null, new object[] { completed, requested });
            };
            test("Twenty cycles schedule baseline, after10 and after20 only", () =>
            {
                if (checkpoint(0, 20) != "skills-before-ten-cycles" || checkpoint(10, 20) != "skills-after-ten-cycles" || checkpoint(20, 20) != "skills-after-twenty-cycles" || checkpoint(9, 20) != null || checkpoint(11, 20) != null || checkpoint(19, 20) != null) throw new InvalidOperationException("Expected exact baseline/10/20 snapshot checkpoints.");
            });
            test("Default ten cycles keep exactly baseline and after10", () =>
            {
                if (checkpoint(0, 10) != "skills-before-ten-cycles" || checkpoint(10, 10) != "skills-after-ten-cycles" || checkpoint(20, 10) != null) throw new InvalidOperationException("Default ten-cycle checkpoint behavior changed.");
            });
            foreach (var sample in new[] {
                new { requested = 20, completed = 20, cycles = 22, snapshots = 3, succeeded = true, expected = true, name = "Twenty completion requires22cycles and3successful snapshots" },
                new { requested = 20, completed = 10, cycles = 12, snapshots = 2, succeeded = true, expected = false, name = "Twenty completion rejects early ten-cycle stop" },
                new { requested = 20, completed = 20, cycles = 22, snapshots = 2, succeeded = true, expected = false, name = "Twenty completion rejects missing intermediate snapshot" },
                new { requested = 20, completed = 20, cycles = 21, snapshots = 3, succeeded = true, expected = false, name = "Twenty completion rejects missing warmup cycle" },
                new { requested = 20, completed = 20, cycles = 22, snapshots = 3, succeeded = false, expected = false, name = "Twenty completion rejects failed snapshot" },
                new { requested = 10, completed = 10, cycles = 12, snapshots = 2, succeeded = true, expected = true, name = "Ten completion preserves12cycles and2snapshots" } })
                test(sample.name, () =>
                {
                    var method = type.GetMethod("CompletedProtocol", flags);
                    if (method == null) throw new InvalidOperationException("Requested-cycle completion policy is not implemented.");
                    var container = new GameObject("Owned plateau policy fixture"); container.SetActive(false);
                    try
                    {
                        var process = container.AddComponent<PerformanceSkillRetentionProcess>();
                        type.GetField("config", flags).SetValue(process, new SkillRetentionConfig { cycles = sample.requested, warmupCycles = 2 });
                        type.GetField("measuredCycles", flags).SetValue(process, sample.completed);
                        var cycleRows = (System.Collections.IList)type.GetField("cycles", flags).GetValue(process);
                        var snapshotRows = (System.Collections.IList)type.GetField("snapshots", flags).GetValue(process);
                        var cycleType = type.GetNestedType("Cycle", flags); var snapshotType = type.GetNestedType("Snapshot", flags);
                        for (int i = 0; i < sample.cycles; i++) { var row = Activator.CreateInstance(cycleType, true); cycleType.GetField("passed").SetValue(row, true); cycleRows.Add(row); }
                        for (int i = 0; i < sample.snapshots; i++) { var row = Activator.CreateInstance(snapshotType, true); snapshotType.GetField("completed").SetValue(row, true); snapshotType.GetField("succeeded").SetValue(row, sample.succeeded); snapshotRows.Add(row); }
                        if ((bool)method.Invoke(process, null) != sample.expected) throw new InvalidOperationException("Completion policy accepted wrong cycle/snapshot evidence.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(container); }
                });
        }
        public static string RunSources()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Live source lease fixture requires empty PlayMode.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            test("Unowned notifications are a no-op", () => PerformanceSkillRetentionSources.ProjectileCreated(null, null, null));
            test("Source lease disposal is owner-specific", () =>
            {
                int called = 0;
                var first = PerformanceSkillRetentionSources.Open((a, b, c) => called++); first.Dispose();
                using (var replacement = PerformanceSkillRetentionSources.Open((a, b, c) => called += 10)) { first.Dispose(); PerformanceSkillRetentionSources.ProjectileCreated(null, null, null); }
                PerformanceSkillRetentionSources.ProjectileCreated(null, null, null);
                if (called != 10) throw new InvalidOperationException("Old disposal removed the replacement or callbacks survived disposal.");
            });
            test("Duplicate source owner rejected", () =>
            {
                using (var scope = PerformanceSkillRetentionSources.Open((a, b, c) => { }))
                {
                    try { using (var duplicate = PerformanceSkillRetentionSources.Open((a, b, c) => { })) { } } catch (InvalidOperationException) { return; }
                    throw new InvalidOperationException("Duplicate source owner accepted.");
                }
            });
            test("Observer fault is latched without throwing into production", () =>
            {
                using (var scope = PerformanceSkillRetentionSources.Open((a, b, c) => throw new InvalidOperationException("Owned fixture fault")))
                { PerformanceSkillRetentionSources.ProjectileCreated(null, null, null); if (!scope.Faulted) throw new InvalidOperationException("Observer fault was silently accepted."); }
            });
            test("Owned skill keyboard survives a different current keyboard", () =>
            {
                var previous = UnityEngine.InputSystem.Keyboard.current;
                var owned = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Owned skill fixture keyboard");
                var foreign = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Foreign fixture keyboard");
                try
                {
                    foreign.MakeCurrent();
                    var type = typeof(PerformanceSkillRetentionSources); var open = type.GetMethod("Open");
                    var callback = new Action<FireballShooter, GameObject, GameObject>((a, b, c) => { });
                    using (var scope = (IDisposable)open.Invoke(null, open.GetParameters().Length == 2 ? new object[] { callback, owned } : new object[] { callback }))
                    {
                        var keyboard = type.GetProperty("SkillKeyboard");
                        if (keyboard == null || !ReferenceEquals(keyboard.GetValue(null, null), owned) || !ReferenceEquals(UnityEngine.InputSystem.Keyboard.current, foreign)) throw new InvalidOperationException("Direct production skill keys escaped the explicit diagnostic keyboard lease.");
                    }
                    if (!ReferenceEquals(type.GetProperty("SkillKeyboard").GetValue(null, null), foreign)) throw new InvalidOperationException("Disposed lease retained the diagnostic keyboard.");
                }
                finally
                {
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(owned); UnityEngine.InputSystem.InputSystem.RemoveDevice(foreign);
                    if (previous != null && previous.added) previous.MakeCurrent();
                }
            });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/skill-retention-source-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), passed = cases.All(row => (bool)row.GetType().GetProperty("passed").GetValue(row)), cases }, Formatting.Indented)); return path;
        }
    }
}
