using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceBoundedRoutePopulationValidation
    {
        const string Ai = "RPGKarawara.AICharacterManager", Player = "RPGKarawara.PlayerManager";
        [MenuItem("Karawara/Validation/TOBE/Validate Bounded Route Population")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Pure reconciliation fixture requires EditMode");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "passed" }); } catch (Exception e) { cases.Add(new { name, passed = false, detail = e.ToString() }); } };
            test("Stable typed phase accepted without global certification", () => Check(false, Sample(false), true));
            test("Census primary controller independent of enumeration order", () =>
            {
                var root = new GameObject("Bounded census fixture") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    root.SetActive(false);
                    var movement = root.AddComponent<NPCMovement>(); var dialogue = root.AddComponent<RPGKarawara.NPCATIVAR>();
                    var method = typeof(RPGKarawara.DevTools.Performance.PerformanceActorCensus).GetMethod("SelectPrimary", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    Require(method != null, "Deterministic primary selector missing");
                    foreach (var parts in new[] { new Component[] { movement, dialogue }, new Component[] { dialogue, movement } })
                        Require(ReferenceEquals(method.Invoke(null, new object[] { parts }), movement), "Primary changed with enumeration order");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            });
            foreach (string state in new[] { "active", "enabled", "spawned" })
                test("Frozen workload rejects initially false " + state, () =>
                {
                    var sample = Sample(false);
                    sample["observed"]["before"]["actors"][0][state] = false;
                    sample["observed"]["after"]["actors"][0][state] = false;
                    Check(false, sample, false);
                });
            test("Missing frozen activity rejected", () => { var sample = Sample(false); ((JObject)sample["expected"]["composition"][0]).Remove("active"); Check(false, sample, false); });
            foreach (string badType in new[] { "Missing.UnknownActor", Player })
                test("Hostile type cannot evade provenance: " + badType, () =>
                {
                    var sample = Sample(false);
                    foreach (string endpoint in new[] { "before", "after" })
                        foreach (var actor in sample["observed"][endpoint]["actors"].Where(a => (string)a["role"] == "hostile"))
                        { actor["primaryType"] = badType; actor["components"] = new JArray(badType); }
                    sample["expected"]["composition"][0]["primaryType"] = badType;
                    foreach (string endpoint in new[] { "aiBefore", "aiAfter" }) sample["observed"][endpoint] = History(0);
                    Check(false, sample, false);
                });
            test("Legitimate player-only phase accepts zero AI", () =>
            {
                var sample = Sample(false); var observed = sample["observed"];
                foreach (string endpoint in new[] { "before", "after" })
                { observed[endpoint]["actors"] = new JArray(observed[endpoint]["actors"].Last.DeepClone()); observed[endpoint]["spawnerAssociations"] = new JArray(); }
                observed["lifecycle"]["initiallyBoundSubjects"] = new JArray(observed["lifecycle"]["initiallyBoundSubjects"].Last.DeepClone());
                observed["aiBefore"] = History(0); observed["aiAfter"] = History(0);
                sample["expected"]["composition"] = new JArray(sample["expected"]["composition"].Last.DeepClone());
                Check(false, sample, true);
            });
            foreach (string mutation in new[] { "missing-history", "fault", "overflow", "unknown-transient", "same-count-replacement", "new-component-old-root", "unsupported", "wrong-window", "missing-owner", "duplicate-subject", "wrong-prefab", "missing-stage", "late-source", "death", "reference-violation" })
                test("Stable phase rejects " + mutation, () =>
                {
                    var sample = Sample(false); var observed = (JObject)sample["observed"];
                    switch (mutation)
                    {
                        case "missing-history": observed.Remove("lifecycle"); break;
                        case "fault": observed["lifecycle"]["sourceFaulted"] = true; break;
                        case "overflow": observed["lifecycle"]["overflow"] = true; observed["lifecycle"]["dropped"] = 1; break;
                        case "unknown-transient": AddEvent(observed, Event(9999, 19999, 0, false, 5, 15)); break;
                        case "same-count-replacement": observed["after"]["actors"][0]["objectId"] = 9999; break;
                        case "new-component-old-root": AddEvent(observed, Event(1000, 9999, 0, false, 0, 15)); break;
                        case "unsupported": observed["after"]["actors"][0]["supportedBinding"] = false; break;
                        case "wrong-window": observed["lifecycle"]["endedTime"] = 21; break;
                        case "missing-owner": ((JArray)observed["lifecycle"]["initiallyBoundSubjects"]).RemoveAt(0); break;
                        case "duplicate-subject": ((JArray)observed["lifecycle"]["initiallyBoundSubjects"]).Add(observed["lifecycle"]["initiallyBoundSubjects"][0].DeepClone()); break;
                        case "wrong-prefab": observed["after"]["spawnerAssociations"][0]["prefabId"] = 9999; break;
                        case "missing-stage": ((JArray)observed["aiAfter"]["events"]).RemoveAt(1); break;
                        case "late-source": observed["lifecycle"]["startedFrame"] = 11; break;
                        case "death": observed["after"]["actors"][0]["dead"] = true; observed["after"]["actors"][0]["currentHealth"] = 0; break;
                        case "reference-violation": observed["contextViolationMask"] = 2; break;
                    }
                    Check(false, sample, false);
                });
            foreach (int extra in new[] { 14, 17 }) test("Expected production generation24->" + (24 + extra), () => Check(true, Sample(true, extra), true));
            foreach (string mutation in new[] { "missing-creator", "extra-transient", "wrong-creator", "missing-birth", "replace-original", "wrong-window", "destroy-original", "forged-known", "duplicate-root", "rewrite-history", "missing-mask" })
                test("Mission transition rejects " + mutation, () =>
                {
                    var sample = Sample(true); var observed = (JObject)sample["observed"];
                    switch (mutation)
                    {
                        case "missing-creator": ((JArray)sample["expected"]["creations"]).RemoveAt(0); break;
                        case "extra-transient": AddEvent(observed, Event(9999, 19999, 0, false, 2, 16)); break;
                        case "wrong-creator": sample["expected"]["creations"][0]["spawnerId"] = 9999; break;
                        case "missing-birth": ((JArray)observed["lifecycle"]["events"]).RemoveAt(0); observed["lifecycle"]["notifications"] = observed["lifecycle"]["events"].Count(); break;
                        case "replace-original": observed["after"]["actors"][0]["objectId"] = 9999; break;
                        case "wrong-window": sample["expected"]["phase"]["untilTime"] = 18; break;
                        case "destroy-original": AddEvent(observed, Event(1000, 11000, 1000, true, 2, 16)); break;
                        case "forged-known": observed["lifecycle"]["events"][0]["initiallyBound"] = true; break;
                        case "duplicate-root": observed["after"]["actors"][25]["objectId"] = 1000; break;
                        case "rewrite-history": observed["aiAfter"]["attempts"][0]["prefabId"] = 9999; break;
                        case "missing-mask": observed.Remove("contextViolationMask"); break;
                    }
                    Check(true, sample, false);
                });
            var report = new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                boundary = "Pure DTO reconciliation tests only; no runtime producer binding, measured capture or global G1B acceptance" };
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-bounded-population-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew)) using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
            Debug.Log("Bounded population fixture: " + path);
            if (!report.passed) Debug.LogError("Bounded population fixture FAILED: " + path);
        }
        static void Check(bool transition, JObject sample, bool expected)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.Performance.PerformanceBoundedRoutePopulation")).FirstOrDefault(t => t != null);
            Require(type != null, "Bounded population reconciler missing");
            var method = type.GetMethod(transition ? "EvaluateTransition" : "EvaluatePhase"); Require(method != null, "Evidence evaluation entry missing");
            var args = method.GetParameters();
            var result = JObject.FromObject(method.Invoke(null, new[] { sample["expected"].ToObject(args[0].ParameterType), sample["observed"].ToObject(args[1].ParameterType) }));
            Require((bool)result["accepted"] == expected, "Unexpected verdict: " + result);
            Require((string)result["scope"] == "bounded-typed-actor-population", "Actor result claimed an unrelated/global scope");
            Require(result["populationVerified"] == null && result["g1bAccepted"] == null, "Pure domain result granted global acceptance");
        }
        static JObject Sample(bool transition, int extra = 14)
        {
            var before = Census(24, 10); var after = Census(transition ? 24 + extra : 24, 20);
            var journal = new JObject { ["startedFrame"] = 10, ["endedFrame"] = 20, ["startedTime"] = 10, ["endedTime"] = 20,
                ["notifications"] = 0, ["sourceFaulted"] = false, ["overflow"] = false, ["dropped"] = 0,
                ["events"] = new JArray(), ["initiallyBoundSubjects"] = new JArray(before["actors"].Select(a => new JObject { ["componentId"] = (int)a["objectId"] + 10000,
                    ["rootId"] = (int)a["objectId"], ["ownerRootId"] = (int)a["objectId"], ["componentType"] = (string)a["primaryType"] })) };
            var observed = new JObject { ["before"] = before, ["after"] = after, ["lifecycle"] = journal, ["aiBefore"] = History(24),
                ["aiAfter"] = History(transition ? 24 + extra : 24), ["contextViolationMask"] = transition ? 32 : 0 };
            var phase = new JObject { ["id"] = "fixture-phase", ["scene"] = "Video1", ["fromTime"] = 10, ["untilTime"] = 20,
                ["boundaryTolerance"] = .25, ["composition"] = Groups(24) };
            if (!transition) return new JObject { ["expected"] = phase, ["observed"] = observed };
            for (int i = 24; i < 24 + extra; i++)
            {
                double t = 15 + i * .01;
                AddEvent(observed, Event(1000 + i, 11000 + i, 0, false, 0, t + .0005));
                AddEvent(observed, Event(1000 + i, 11000 + i, 0, false, 3, t + .0025));
            }
            var expected = new JObject { ["phase"] = phase, ["resultingComposition"] = Groups(24 + extra),
                ["creations"] = new JArray(Enumerable.Range(24, extra).Select(i => new JObject { ["spawnerId"] = 2000 + i,
                    ["prefabId"] = 3000, ["primaryType"] = Ai, ["count"] = 1 })) };
            return new JObject { ["expected"] = expected, ["observed"] = observed };
        }
        static JArray Groups(int ai) => new JArray(new JObject { ["role"] = "hostile", ["primaryType"] = Ai, ["count"] = ai, ["active"] = true, ["enabled"] = true, ["spawned"] = true },
            new JObject { ["role"] = "local-player", ["primaryType"] = Player, ["count"] = 1, ["active"] = true, ["enabled"] = true, ["spawned"] = true });
        static JObject Census(int count, int frame)
        {
            var actors = new JArray(Enumerable.Range(0, count).Select(i => Actor(1000 + i, Ai, "hostile", (ulong)(100 + i)))); actors.Add(Actor(500, Player, "local-player", 1));
            return new JObject { ["activeScene"] = "Video1", ["frame"] = frame, ["actors"] = actors,
                ["spawnerAssociations"] = new JArray(Enumerable.Range(0, count).Select(i => new JObject { ["spawnerId"] = 2000 + i, ["prefabId"] = 3000,
                    ["createdObjectId"] = 1000 + i, ["actorObjectId"] = 1000 + i, ["localCharacterId"] = (ulong)(100 + i),
                    ["linked"] = true, ["uniqueOwner"] = true, ["spawned"] = true, ["supportedActor"] = true })) };
        }
        static JObject Actor(int id, string type, string role, ulong network) => new JObject { ["objectId"] = id, ["sceneHandle"] = 1, ["scene"] = "Video1",
            ["path"] = "fixture/" + id, ["primaryType"] = type, ["role"] = role, ["components"] = new JArray(type), ["active"] = true, ["enabled"] = true,
            ["supportedBinding"] = true, ["spawned"] = true, ["localCharacterId"] = network, ["currentHealth"] = 100, ["maximumHealth"] = 100, ["dead"] = false };
        static JObject History(int count)
        {
            var rows = new JArray(); var events = new JArray();
            for (int i = 0; i < count; i++)
            {
                rows.Add(new JObject { ["token"] = i + 1, ["spawnerId"] = 2000 + i, ["prefabId"] = 3000, ["createdId"] = 1000 + i,
                    ["sceneHandle"] = 1, ["localCharacterId"] = (ulong)(100 + i), ["lastStage"] = 4, ["spawned"] = true, ["registered"] = true,
                    ["finished"] = true, ["invalid"] = false, ["despawnRequested"] = false, ["despawned"] = false });
                for (int s = 0; s < 5; s++) events.Add(new JObject { ["token"] = i + 1, ["stage"] = s, ["frame"] = i < 24 ? 1 : 15, ["time"] = (i < 24 ? 1 : 15) + i * .01 + s * .001 });
            }
            return new JObject { ["startedFrame"] = 0, ["sourceFaulted"] = false, ["overflow"] = false, ["dropped"] = 0, ["failedAttempts"] = 0,
                ["preexistingAiIds"] = new JArray(), ["unmatchedAiIds"] = new JArray(), ["orphanLiveAttempts"] = new JArray(), ["attempts"] = rows, ["events"] = events,
                ["currentAiCount"] = count, ["matchedAiCount"] = count, ["currentAiHistoryMatched"] = true };
        }
        static JObject Event(int root, int component, int owner, bool known, int change, double time) => new JObject { ["rootId"] = root, ["componentId"] = component,
            ["ownerRootId"] = owner, ["initiallyBound"] = known, ["componentType"] = Ai, ["change"] = change, ["frame"] = 15, ["time"] = time };
        static void AddEvent(JObject observed, JObject row)
        {
            var rows = (JArray)observed["lifecycle"]["events"]; rows.Add(row); observed["lifecycle"]["notifications"] = rows.Count;
        }
        static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    }
}
