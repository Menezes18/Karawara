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
    public static class PerformanceCucaProjectileRetentionValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode fixture required.");
            var cases = new List<object>();
            string root = Path.GetFullPath("Builds/TOBE-QA/FixtureData/cuca-projectile-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "config.json"), output = Path.Combine(root, "output"), save = Path.Combine(root, "saves"), personal = Path.Combine(root, "personal");
            try
            {
                foreach (string scenario in new[] { "valid", "nine", "eleven", "short", "unbounded", "warmup", "settle", "unknown", "mixed", "shared", "death-cheat", "wrong-scene" })
                {
                    var source = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/cuca-arena-proposal-inspection-2026-10-22.json"));
                    var protocol = new JObject { ["routeId"] = "cuca-natural-projectile-retention-v1", ["outputRoot"] = output,
                        ["seedSaveJson"] = source["rehearsal"]["seedSaveJson"], ["bursts"] = 10, ["warmupSeconds"] = 30, ["settleSeconds"] = 5, ["observationTimeoutSeconds"] = 360 };
                    var config = new JObject { ["mode"] = "cuca-projectile-retention", ["width"] = 1920, ["height"] = 1080, ["quality"] = "High Fidelity", ["vsync"] = 0, ["targetFrameRate"] = -1, ["cucaProjectileRetention"] = protocol };
                    switch (scenario)
                    {
                        case "nine": protocol["bursts"] = 9; break;
                        case "eleven": protocol["bursts"] = 11; break;
                        case "short": protocol["observationTimeoutSeconds"] = 20; break;
                        case "unbounded": protocol["observationTimeoutSeconds"] = 99999; break;
                        case "warmup": protocol["warmupSeconds"] = 0; break;
                        case "settle": protocol["settleSeconds"] = 0; break;
                        case "unknown": protocol["retentionAccepted"] = true; break;
                        case "mixed": config["capture"] = new JObject(); break;
                        case "shared": protocol["outputRoot"] = save; break;
                        case "death-cheat": protocol["reviveAfterDeath"] = true; break;
                        case "wrong-scene": var seed = JObject.Parse((string)protocol["seedSaveJson"]); seed["sceneIndex"] = 2; protocol["seedSaveJson"] = seed.ToString(Formatting.None); break;
                    }
                    File.WriteAllText(path, config.ToString(Formatting.None));
                    var response = PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", save }, personal);
                    cases.Add(new { name = scenario, passed = response.success == (scenario == "valid") && response.result == null && !Directory.Exists(output) && !Directory.Exists(save), detail = string.Join("; ", response.errors) });
                }
            }
            finally { File.Delete(path); Directory.Delete(root); }
            var type = typeof(PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceCucaProjectileRetentionProcess");
            foreach (string scenario in new[] { "actual-provenance", "foreign-owner", "foreign-prefab", "duplicate-clone", "ordinal-gap", "bounded-journal", "destroyed-reference-cleared", "natural-ten-complete", "incomplete-tenth" })
            {
                bool passed = false; string detail = "Missing explicit Cuca projectile retention coordinator.";
                var container = new GameObject("Owned projectile retention fixture"); container.SetActive(false);
                try
                {
                    if (type != null)
                    {
                        var process = container.AddComponent(type); var boss = container.AddComponent<CucaBoss>();
                        var prefab = new GameObject("Owned declared prefab"); prefab.transform.SetParent(container.transform); prefab.AddComponent<Rigidbody>(); prefab.AddComponent<DamageCollider>();
                        boss.magicProjectile = prefab; boss.magicSpawnPoint = container.transform;
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        type.GetMethod("BindBoss", flags).Invoke(process, new object[] { boss });
                        var record = type.GetMethod("ObserveProjectile", flags);
                        var clone = new GameObject("Owned separately created clone"); clone.transform.SetParent(container.transform);
                        var otherBoss = prefab.AddComponent<CucaBoss>();
                        record.Invoke(process, new object[] { scenario == "foreign-owner" ? otherBoss : boss, scenario == "foreign-prefab" ? clone : prefab, clone, scenario == "ordinal-gap" ? 2 : 1, 0 });
                        if (scenario == "duplicate-clone") record.Invoke(process, new object[] { boss, prefab, clone, 1, 1 });
                        if (scenario == "bounded-journal")
                        {
                            type.GetField("effectCount", flags).SetValue(process, 1024);
                            var next = new GameObject("Owned overflow attempt"); next.transform.SetParent(container.transform);
                            record.Invoke(process, new object[] { boss, prefab, next, 1, 1 });
                        }
                        if (scenario == "natural-ten-complete" || scenario == "incomplete-tenth")
                            for (int i = 1; i < (scenario == "incomplete-tenth" ? 29 : 30); i++)
                            {
                                var next = new GameObject("Owned observed clone " + i); next.transform.SetParent(container.transform);
                                record.Invoke(process, new object[] { boss, prefab, next, 1 + i / 3, i % 3 });
                            }
                        bool fault = (bool)type.GetField("sourceFault", flags).GetValue(process);
                        int count = (int)type.GetField("effectCount", flags).GetValue(process);
                        if (scenario == "actual-provenance")
                        {
                            var row = JObject.FromObject(((Array)type.GetField("effects", flags).GetValue(process)).GetValue(0));
                            passed = !fault && count == 1 && (int)row["ownerId"] == boss.GetInstanceID() && (int)row["prefabId"] == prefab.GetInstanceID() && row["live"] == null;
                        }
                        else if (scenario == "destroyed-reference-cleared")
                        {
                            UnityEngine.Object.DestroyImmediate(clone); type.GetMethod("Sweep", flags).Invoke(process, null);
                            var row = ((Array)type.GetField("effects", flags).GetValue(process)).GetValue(0);
                            passed = !fault && ReferenceEquals(row.GetType().GetField("live").GetValue(row), null);
                        }
                        else if (scenario == "natural-ten-complete" || scenario == "incomplete-tenth") passed = !fault && (int)type.GetMethod("CountCompleteBurstsAfter", flags).Invoke(process, new object[] { 0 }) == (scenario == "incomplete-tenth" ? 9 : 10);
                        else if (scenario == "bounded-journal") passed = (bool)type.GetField("overflow", flags).GetValue(process) && count == 1024;
                        else passed = fault && count <= 1024;
                        detail = "Owned synthetic roots only; no actual attack or retention proof.";
                    }
                }
                catch (Exception error) { detail = error.ToString(); }
                finally { UnityEngine.Object.DestroyImmediate(container); }
                cases.Add(new { name = scenario, passed, detail });
            }
            string report = Path.GetFullPath("Docs/Validation/TOBE/Execution/cuca-projectile-retention-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(report, JsonConvert.SerializeObject(new { passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases, boundary = "Parser and bounded source ownership fixture; no runtime, timing or retention acceptance." }, Formatting.Indented));
            return report;
        }
    }
}
