using System;
using System.Collections;
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
    public static class PerformanceSnapshotControlValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode required.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "Policy fixture only" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            string root = Path.GetFullPath("Builds/TOBE-QA/FixtureData/snapshot-control-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "config.json");
            try
            {
                foreach (string scenario in new[] { "normal10", "normal20", "explicit-false", "control", "control-ten", "normal-zero", "missing-flag", "string-flag", "null-flag", "unknown-field", "missing-required", "control-normal-route", "false-control-route", "interval3", "interval390", "interval600", "interval-low", "interval-high", "interval-null", "interval-string", "interval-bool", "interval-normal" })
                    test("Parser " + scenario, () =>
                    {
                        string[] names = { "PetSpirit", "Escudo", "SkillJacare" }, types = { "RPGKarawara.SkillTree.AttackSkill", "RPGKarawara.SkillTree.DefenseSkill", "RPGKarawara.SkillTree.SupportSkill" };
                        var seed = JObject.Parse((string)JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/video1-heading-authored-skills-template.json"))["rehearsal"]["seedSaveJson"]);
                        seed["qaSkillBindings"] = new JObject { ["skillsSlot"] = new JArray(names.Select((n, i) => new JObject { ["assetName"] = n, ["type"] = types[i] })) };
                        var protocol = new JObject { ["routeId"] = "video1-skill-retention-v1", ["outputRoot"] = Path.Combine(root, "output"), ["seedSaveJson"] = seed.ToString(Formatting.None), ["skillNames"] = JArray.FromObject(names), ["skillTypes"] = JArray.FromObject(types), ["cycles"] = 10, ["warmupCycles"] = 2, ["warmupSeconds"] = 30, ["settleSeconds"] = 3, ["cycleTimeoutSeconds"] = 90 };
                        bool expected = scenario == "normal10" || scenario == "normal20" || scenario == "explicit-false" || scenario == "control" || scenario == "interval3" || scenario == "interval390" || scenario == "interval600";
                        if (scenario == "normal20") protocol["cycles"] = 20;
                        if (scenario == "explicit-false") protocol["snapshotControl"] = false;
                        if (new[] { "control", "control-ten", "missing-flag", "string-flag", "null-flag", "unknown-field", "missing-required", "false-control-route" }.Contains(scenario)) { protocol["snapshotControl"] = true; protocol["routeId"] = "video1-skill-snapshot-control-v1"; protocol["cycles"] = 0; }
                        if (scenario.StartsWith("interval") && scenario != "interval-normal") { protocol["snapshotControl"] = true; protocol["routeId"] = "video1-skill-snapshot-control-v1"; protocol["cycles"] = 0; protocol["controlIntervalSeconds"] = 3; }
                        if (scenario == "interval-normal") protocol["controlIntervalSeconds"] = 3;
                        if (scenario == "interval390") protocol["controlIntervalSeconds"] = 390;
                        if (scenario == "interval600") protocol["controlIntervalSeconds"] = 600;
                        if (scenario == "interval-low") protocol["controlIntervalSeconds"] = 2;
                        if (scenario == "interval-high") protocol["controlIntervalSeconds"] = 601;
                        if (scenario == "interval-null") protocol["controlIntervalSeconds"] = JValue.CreateNull();
                        if (scenario == "interval-string") protocol["controlIntervalSeconds"] = "390";
                        if (scenario == "interval-bool") protocol["controlIntervalSeconds"] = true;
                        if (scenario == "control-ten") protocol["cycles"] = 10;
                        if (scenario == "normal-zero") protocol["cycles"] = 0;
                        if (scenario == "missing-flag") protocol.Remove("snapshotControl");
                        if (scenario == "string-flag") protocol["snapshotControl"] = "true";
                        if (scenario == "null-flag") protocol["snapshotControl"] = JValue.CreateNull();
                        if (scenario == "unknown-field") protocol["accepted"] = true;
                        if (scenario == "missing-required") protocol.Remove("warmupSeconds");
                        if (scenario == "control-normal-route") protocol["snapshotControl"] = true;
                        if (scenario == "false-control-route") protocol["snapshotControl"] = false;
                        File.WriteAllText(path, new JObject { ["mode"] = "skill-retention", ["width"] = 1920, ["height"] = 1080, ["quality"] = "High Fidelity", ["vsync"] = 0, ["targetFrameRate"] = -1, ["skillRetention"] = protocol }.ToString());
                        var result = PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", Path.Combine(root, "saves") }, Path.Combine(root, "personal"));
                        if (result.success != expected) throw new InvalidOperationException("Parser expected " + expected + ": " + string.Join(";", result.errors));
                    });
            }
            finally { File.Delete(path); Directory.Delete(root); }
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
            var type = typeof(PerformanceSkillRetentionProcess);
            test("Three distinct control checkpoints only", () =>
            {
                var method = type.GetMethod("SnapshotControlCheckpoint", flags); if (method == null) throw new InvalidOperationException("Missing control checkpoint contract.");
                string[] labels = Enumerable.Range(0, 3).Select(i => (string)method.Invoke(null, new object[] { i })).ToArray();
                if (labels.Any(string.IsNullOrEmpty) || labels.Distinct().Count() != 3 || (string)method.Invoke(null, new object[] { 3 }) != null) throw new InvalidOperationException("Invalid control checkpoint scheduling.");
            });
            foreach (string scenario in new[] { "valid", "one-snapshot", "failed-snapshot", "new-effect", "measured-cycle", "extra-cycle", "missing-warmup", "duplicate-path" })
                test("Control completion " + scenario, () =>
                {
                    var container = new GameObject("Owned snapshot control fixture"); container.SetActive(false);
                    try
                    {
                        var process = container.AddComponent<PerformanceSkillRetentionProcess>(); var config = new SkillRetentionConfig { cycles = 0, warmupCycles = 2 };
                        var control = typeof(SkillRetentionConfig).GetField("snapshotControl"); if (control == null) throw new InvalidOperationException("Missing explicit control mode."); control.SetValue(config, true); type.GetField("config", flags).SetValue(process, config);
                        type.GetField("controlBaselineEffectCount", flags).SetValue(process, 2); type.GetField("effectCount", flags).SetValue(process, scenario == "new-effect" ? 3 : 2);
                        type.GetField("measuredCycles", flags).SetValue(process, scenario == "measured-cycle" ? 1 : 0);
                        var cycleRows = (IList)type.GetField("cycles", flags).GetValue(process); var cycleType = type.GetNestedType("Cycle", flags);
                        int cycleCount = scenario == "extra-cycle" ? 3 : scenario == "missing-warmup" ? 1 : 2;
                        for (int i = 0; i < cycleCount; i++) { var row = Activator.CreateInstance(cycleType, true); cycleType.GetField("passed").SetValue(row, true); cycleType.GetField("warmup").SetValue(row, true); cycleRows.Add(row); }
                        var snapshotRows = (IList)type.GetField("snapshots", flags).GetValue(process); var snapshotType = type.GetNestedType("Snapshot", flags);
                        for (int i = 0; i < (scenario == "one-snapshot" ? 1 : 3); i++) { var row = Activator.CreateInstance(snapshotType, true); snapshotType.GetField("completed").SetValue(row, true); snapshotType.GetField("succeeded").SetValue(row, scenario != "failed-snapshot"); snapshotType.GetField("path").SetValue(row, scenario == "duplicate-path" ? "duplicate" : "snapshot-" + i); snapshotType.GetField("requestedRealtimeSeconds").SetValue(row, 10d + i * 5); snapshotType.GetField("completedRealtimeSeconds").SetValue(row, 12d + i * 5); snapshotRows.Add(row); }
                        bool result = (bool)type.GetMethod("CompletedProtocol", flags).Invoke(process, null); if (result != (scenario == "valid")) throw new InvalidOperationException("Wrong control completion result " + result);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(container); }
                });
            foreach (string scenario in new[] { "valid-390", "early-390", "backwards-callback", "nonfinite-clock", "overlapping-snapshot" })
                test("Control interval timing " + scenario, () =>
                {
                    var container = new GameObject("Owned snapshot timing fixture"); container.SetActive(false);
                    try
                    {
                        var config = new SkillRetentionConfig { cycles = 0, warmupCycles = 2 };
                        var interval = typeof(SkillRetentionConfig).GetField("controlIntervalSeconds");
                        if (interval == null) throw new InvalidOperationException("Missing explicit snapshot control interval contract.");
                        interval.SetValue(config, 390d); typeof(SkillRetentionConfig).GetField("snapshotControl").SetValue(config, true);
                        var process = container.AddComponent<PerformanceSkillRetentionProcess>(); type.GetField("config", flags).SetValue(process, config);
                        type.GetField("controlBaselineEffectCount", flags).SetValue(process, 2); type.GetField("effectCount", flags).SetValue(process, 2);
                        var cycleRows = (IList)type.GetField("cycles", flags).GetValue(process); var cycleType = type.GetNestedType("Cycle", flags);
                        for (int i = 0; i < 2; i++) { var row = Activator.CreateInstance(cycleType, true); cycleType.GetField("passed").SetValue(row, true); cycleType.GetField("warmup").SetValue(row, true); cycleRows.Add(row); }
                        var snapshotRows = (IList)type.GetField("snapshots", flags).GetValue(process); var snapshotType = type.GetNestedType("Snapshot", flags);
                        for (int i = 0; i < 3; i++)
                        {
                            double requested = 10 + i * 392, completed = requested + 2;
                            if (i == 1 && scenario == "early-390") requested -= .01;
                            if (i == 1 && scenario == "backwards-callback") completed = requested - 1;
                            if (i == 1 && scenario == "nonfinite-clock") requested = double.NaN;
                            if (i == 1 && scenario == "overlapping-snapshot") requested = 11;
                            var row = Activator.CreateInstance(snapshotType, true); snapshotType.GetField("completed").SetValue(row, true); snapshotType.GetField("succeeded").SetValue(row, true); snapshotType.GetField("path").SetValue(row, "snapshot-" + i);
                            snapshotType.GetField("requestedRealtimeSeconds").SetValue(row, requested); snapshotType.GetField("completedRealtimeSeconds").SetValue(row, completed); snapshotRows.Add(row);
                        }
                        bool result = (bool)type.GetMethod("CompletedProtocol", flags).Invoke(process, null);
                        if (result != (scenario == "valid-390")) throw new InvalidOperationException("Wrong interval timing completion " + result);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(container); }
                });
            string report = Path.GetFullPath("Docs/Validation/TOBE/Execution/snapshot-control-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(report, JsonConvert.SerializeObject(new { passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Parser/scheduling/completion fixture only; actual snapshot-only Player control pending." }, Formatting.Indented)); return report;
        }
    }
}
