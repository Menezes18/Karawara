using System;
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
    public static class PerformanceBossCreationValidation
    {
        [MenuItem("Karawara/Validation/TOBE/Validate Boss Creation History (Play)")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Owned empty PlayMode required.");
            var cases = new List<object>();
            foreach (string scenario in new[] { "missing-prefab", "exception-finally", "existing-owner", "bounded", "allocation" })
            {
                var root = new GameObject("Owned inactive boss history source"); root.SetActive(false);
                var prefab = new GameObject("Owned inactive boss history prefab"); prefab.SetActive(false);
                PerformanceAiCreationHistory scope = null;
                try
                {
                    var source = root.AddComponent<BossSpawnerInput>();
                    scope = PerformanceAiCreationHistory.Begin(new[] { "-karawaraPerformanceRoster" });
                    if (scenario == "existing-owner")
                    {
                        source.bossPrefab = prefab;
                        typeof(BossSpawnerInput).GetField("instantiatedBoss", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(source, prefab);
                        source.SpawnBoss();
                        Require(scope.Capture().attempts.Length == 0, "Existing owned boss fabricated a new creation attempt");
                    }
                    else if (scenario == "exception-finally")
                    {
                        source.bossPrefab = prefab; // Null authored spawnPoint throws before Instantiate, through the real production method.
                        bool threw = false;
                        try { source.SpawnBoss(); } catch (NullReferenceException) { threw = true; }
                        var report = scope.Capture();
                        Require(threw && report.attempts.Length == 1 && report.attempts[0].finished && report.attempts[0].prefabId == prefab.GetInstanceID() && report.failedAttempts == 1 && !report.sourceFaulted,
                            "Production exception lost exact source/prefab/completion evidence");
                    }
                    else
                    {
                        int count = scenario == "bounded" ? 1025 : scenario == "allocation" ? 164 : 1;
                        long before = 0;
                        for (int i = 0; i < count; i++)
                        {
                            if (i == 64) before = GC.GetAllocatedBytesForCurrentThread();
                            source.SpawnBoss();
                        }
                        long allocated = scenario == "allocation" ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
                        var report = scope.Capture();
                        Require(report.attempts.Length == Math.Min(count, 1024) && !report.sourceFaulted && report.failedAttempts == report.attempts.Length,
                            "Missing boss prefab attempts were omitted or fabricated registration");
                        Require(report.attempts.All(a => a.spawnerId == source.GetInstanceID() && a.finished && !a.spawned && !a.registered), "Exact boss producer identity/completion missing");
                        Require(report.overflow == (scenario == "bounded") && report.events.Length <= 8192, "Boss history overflow not bounded/latched");
                        Require(allocated == 0, "Warmed boss callbacks allocated " + allocated + " managed bytes");
                    }
                    cases.Add(new { name = scenario, passed = true });
                }
                catch (Exception error) { cases.Add(new { name = scenario, passed = false, error = error.ToString() }); }
                finally { scope?.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(prefab); }
            }
            foreach (string scenario in new[] { "census-source", "choice-source", "ambiguous-choice-source" })
            {
                var owner = new GameObject("Owned inactive boss declaration"); owner.SetActive(false);
                var choice = new GameObject("Owned inactive boss choice"); choice.SetActive(false);
                var prefab = new GameObject("Owned inactive boss declaration prefab"); prefab.SetActive(false);
                try
                {
                    prefab.AddComponent<AISaciCharacterManager>();
                    var source = owner.AddComponent<BossSpawnerInput>(); source.bossPrefab = prefab; source.spawnPoint = owner.transform;
                    var npc = owner.AddComponent<NPCDialogue>(); npc.escolha1 = choice;
                    choice.AddComponent<MissionManager>(); choice.AddComponent<AtivarSaci>().Input = source;
                    if (scenario == "ambiguous-choice-source") choice.AddComponent<AtivarSaci>().Input = source;
                    if (scenario == "census-source")
                    {
                        var census = PerformanceActorCensus.Capture(new[] { "-karawaraPerformanceRoster" });
                        var row = census.report.spawnerAssociations.SingleOrDefault(s => s.spawnerId == source.GetInstanceID());
                        Require(row != null && row.prefabId == prefab.GetInstanceID() && row.createdObjectId == 0 && !row.linked && row.status == "not-created",
                            "Census omitted exact boss producer/prefab or fabricated a live link");
                    }
                    else
                    {
                        var rows = PerformanceMissionProducerInventory.Capture(new[] { npc });
                        Require(rows.Length == (scenario == "choice-source" ? 1 : 2) && rows.All(r => r.npcId == npc.GetInstanceID() && r.choiceIndex == 1 && r.spawnerId == source.GetInstanceID() &&
                            r.prefabId == prefab.GetInstanceID() && r.primaryType == typeof(AISaciCharacterManager).FullName && r.existingCreatedId == 0), "Actual AtivarSaci.Input choice source declaration missing");
                        Require(rows.All(r => string.IsNullOrEmpty(r.issue)) == (scenario == "choice-source"), "Ambiguous boss choice source not rejected");
                    }
                    cases.Add(new { name = scenario, passed = true });
                }
                catch (Exception error) { cases.Add(new { name = scenario, passed = false, error = error.ToString() }); }
                finally { Object.DestroyImmediate(choice); Object.DestroyImmediate(owner); Object.DestroyImmediate(prefab); }
            }
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-boss-creation-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                populationVerified = false, boundary = "Actual production no-op/exception/history owner callbacks only; not successful NGO spawn, encounter, timing or global creation acceptance." }, Formatting.Indented));
            Debug.Log("Boss creation fixture: " + path);
        }
        static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
