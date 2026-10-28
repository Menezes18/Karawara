using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceCucaCertifiedRouteValidation
    {
        static Type Contract => typeof(Performance.PerformanceProcessHarness).Assembly.GetType("RPGKarawara.DevTools.Performance.PerformanceCucaCertifiedRouteProcess") ?? throw new InvalidOperationException("Dedicated Cuca certified coordinator missing");
        static readonly string Build = new string('a', 64), Guid = new string('1', 32);
        const string Timeline = "{\"schemaVersion\":1,\"cameraMode\":\"movement-heading-v1\",\"cameraTurnDegreesPerSecond\":30,\"frames\":[{\"timeSeconds\":0,\"keys\":[\"W\"]},{\"timeSeconds\":149,\"keys\":[]}]}";
        const string Seed = "{\"sceneIndex\":3,\"characterName\":\"Cuca QA\",\"currentHealth\":100,\"vitality\":10,\"endurance\":10,\"xPosition\":0,\"yPosition\":0,\"zPosition\":0}";
        [MenuItem("Karawara/Validation/TOBE/Validate Cuca Certified Contract")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Cuca manifest/counter fixture requires EditMode");
            var rows = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); rows.Add(new { name, passed = true, error = "" }); } catch (Exception e) { rows.Add(new { name, passed = false, error = e.ToString() }); } };
            test("Dedicated Cuca manifest accepts frozen encounter", () => Parse(Manifest()));
            test("Cuca manifest accepts its exact typed scene source", () =>
            {
                var m = Manifest(); ((JArray)m["composition"]).Add(JObject.FromObject(new { role = "world-visual", primaryType = typeof(Performance.PerformanceCucaSceneSource).FullName, count = 8, active = true, enabled = true, spawned = false })); Parse(m);
            });
            test("Cuca scene source cannot claim a hostile role", () =>
            {
                var m = Manifest(); ((JArray)m["composition"]).Add(JObject.FromObject(new { role = "hostile", primaryType = typeof(Performance.PerformanceCucaSceneSource).FullName, count = 8, active = true, enabled = true, spawned = false })); Rejected(() => Parse(m));
            });
            test("Frozen Cuca manifest snapshots do not alias", () => { var frozen = Parse(Manifest()); var property = frozen.GetType().GetProperty("Snapshot"); var first = property.GetValue(frozen); var checkpoints = (Array)first.GetType().GetField("poseCheckpoints").GetValue(first); ((float[])checkpoints.GetValue(0).GetType().GetField("position").GetValue(checkpoints.GetValue(0)))[0] = 999; var second = property.GetValue(frozen); var copy = (Array)second.GetType().GetField("poseCheckpoints").GetValue(second); Require(((float[])copy.GetValue(0).GetType().GetField("position").GetValue(copy.GetValue(0)))[0] == 0, "Frozen checkpoint arrays alias caller"); });
            foreach (string mutation in new[] { "route", "scene", "build", "guid", "distribution", "timeline", "seed", "missing-field", "unknown-field", "boss-path", "missing-boss", "duplicate-counter", "missing-pose", "widen-pose", "camera-mode", "camera-rate", "timing", "settings" })
            {
                string selected = mutation;
                test("Cuca manifest rejects " + selected, () =>
                {
                    var manifest = Manifest();
                    switch (selected)
                    {
                        case "route": manifest["routeId"] = "tutorial-exploration-v1"; break;
                        case "scene": manifest["scene"] = "Tutorial_Final"; break;
                        case "build": manifest["buildAssemblySha256"] = new string('b', 64); break;
                        case "guid": manifest["playerBuildGuid"] = new string('2', 32); break;
                        case "distribution": manifest["distributionSha256"] = new string('b', 64); break;
                        case "timeline": manifest["timelineSha256"] = new string('b', 64); break;
                        case "seed": manifest["seedSha256"] = new string('b', 64); break;
                        case "missing-field": manifest.Remove("bossPath"); break;
                        case "unknown-field": manifest["g1bAccepted"] = true; break;
                        case "boss-path": manifest["bossPath"] = ""; break;
                        case "missing-boss": ((JArray)manifest["composition"]).RemoveAt(0); break;
                        case "duplicate-counter": manifest["counterNames"] = new JArray("Main Thread", "Main Thread"); break;
                        case "missing-pose": ((JArray)manifest["poseCheckpoints"]).RemoveAt(2); break;
                        case "widen-pose": manifest["poseCheckpoints"][2]["positionTolerance"] = 3; break;
                        case "camera-mode": manifest["cameraMode"] = "stationary"; break;
                        case "camera-rate": manifest["cameraTurnDegreesPerSecond"] = 60; break;
                        case "timing": manifest["warmupSeconds"] = 0; break;
                        case "settings": manifest["renderScale"] = .5; break;
                    }
                    Rejected(() => Parse(manifest));
                });
            }
            test("Cuca scene3 seed required with matching hash", () => { string bad = Seed.Replace("\"sceneIndex\":3", "\"sceneIndex\":1"); var manifest = Manifest(); manifest["seedSha256"] = Hash(bad); Rejected(() => Parse(manifest, Timeline, bad)); });
            test("Counter proof accepts observed complete bursts", () => Counters(Before(), After(), true));
            test("Counter proof accepts last burst straddling end", () => { var after = After(); after["createdProjectiles"] = 4; Counters(Before(), after, true); });
            test("Counter proof accepts two carried-in projectiles", () => { var before = Before(); before["projectileAttackCount"] = 1; before["createdProjectiles"] = 1; Counters(before, After(), true); });
            test("Counter proof accepts one carried-in projectile", () => { var before = Before(); before["projectileAttackCount"] = 1; before["createdProjectiles"] = 2; Counters(before, After(), true); });
            test("Counter proof rejects impossible preflight overproduction", () => { var before = Before(); before["projectileAttackCount"] = 1; before["createdProjectiles"] = 4; var after = After(); after["projectileAttackCount"] = 3; after["createdProjectiles"] = 10; Counters(before, after, false); });
            test("Counter proof rejects impossible carried-in backlog", () => { var before = Before(); before["projectileAttackCount"] = 3; before["createdProjectiles"] = 1; var after = After(); after["projectileAttackCount"] = 4; after["createdProjectiles"] = 4; Counters(before, after, false); });
            test("Counter proof rejects impossible postflight backlog", () => { var after = After(); after["projectileAttackCount"] = 3; Counters(Before(), after, false); });
            test("Boss binding rejects actual AttackTimer cooldown mutation", BossCooldownMutation);
            foreach (string mutation in new[] { "not-active-before", "not-active-after", "no-projectile-attacks", "too-few-projectiles", "too-many-projectiles", "no-laser-attacks", "no-laser-activations", "negative", "overflow", "decreased", "late-activation" })
            {
                string selected = mutation;
                test("Counter proof rejects " + selected, () =>
                {
                    var before = Before(); var after = After();
                    switch (selected)
                    {
                        case "not-active-before": before["active"] = false; break;
                        case "not-active-after": after["active"] = false; break;
                        case "no-projectile-attacks": after["projectileAttackCount"] = 0; break;
                        case "too-few-projectiles": after["createdProjectiles"] = 2; break;
                        case "too-many-projectiles": after["createdProjectiles"] = 7; break;
                        case "no-laser-attacks": after["laserAttackCount"] = 0; break;
                        case "no-laser-activations": after["laserActivationCount"] = 0; break;
                        case "negative": before["retreatCount"] = -1; break;
                        case "overflow": after["projectileAttackCount"] = int.MaxValue; after["createdProjectiles"] = int.MaxValue; break;
                        case "decreased": before["retreatCount"] = 2; after["retreatCount"] = 1; break;
                        case "late-activation": after["activationCount"] = 2; break;
                    }
                    Counters(before, after, false);
                });
            }
            test("Cuca reconciliation accepts actual stable phase shape", () => Population(Observation(), true));
            foreach (string mutation in new[] { "missing", "unknown-animator", "replacement", "wrong-window", "unknown-event", "unsupported", "boss-not-living" })
            {
                string selected = mutation;
                test("Cuca phase rejects " + selected, () =>
                {
                    var observed = Observation();
                    switch (selected)
                    {
                        case "missing": observed.Remove("lifecycle"); break;
                        case "unknown-animator": observed["after"]["unclassifiedAnimators"] = new JArray(new JObject { ["objectId"] = 99 }); break;
                        case "replacement": observed["after"]["actors"][0]["objectId"] = 99; break;
                        case "wrong-window": observed["lifecycle"]["endedTime"] = 159; break;
                        case "unknown-event": observed["lifecycle"]["events"] = new JArray(new JObject { ["componentId"] = 99, ["rootId"] = 99, ["ownerRootId"] = 0, ["componentType"] = typeof(CucaBoss).FullName, ["change"] = 0, ["frame"] = 20, ["time"] = 30, ["initiallyBound"] = false }); observed["lifecycle"]["notifications"] = 1; break;
                        case "unsupported": observed["after"]["actors"][0]["supportedBinding"] = false; break;
                        case "boss-not-living": observed["after"]["actors"][0]["currentHealth"] = 30; break;
                    }
                    Population(observed, false);
                });
            }
            test("Missing actual standalone owners rejected", () => { try { Contract.GetMethod("Begin").Invoke(null, new object[5]); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { return; } throw new InvalidOperationException("Missing actual owners accepted"); });
            test("Partial coordinator disposal is idempotent", () => { var session = Activator.CreateInstance(Contract, true); ((IDisposable)session).Dispose(); ((IDisposable)session).Dispose(); Require(!(bool)Contract.GetProperty("IsRunning").GetValue(session) && (int)Contract.GetProperty("OwnedRecorderCount").GetValue(session) == 0, "Partial coordinator retained ownership"); });
            LightCases(test);
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-cuca-certified-fixture-" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = rows.All(r => (bool)r.GetType().GetProperty("passed").GetValue(r)), cases = rows, boundary = "Cuca manifest/counter/raw-phase contract only; no actual encounter, route, timing or G1B acceptance" }, Formatting.Indented));
            Debug.Log("Cuca certified fixture: " + path);
        }
        static object InvokeLight(string name, params object[] args)
        {
            var method = Contract.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Explicit Cuca light-control policy is not implemented: " + name);
            try { return method.Invoke(null, args); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static JObject LightEvidence() => new JObject {
            ["measurementArm"] = "light-control", ["manifest"] = Manifest(),
            ["capture"] = JObject.FromObject(new { fullWindowCompleted = true, conditionsChanged = false, comparisonValid = false, populationVerified = false, cameraVerified = false, errors = new string[0], conditions = new { lightControl = true } }),
            ["poses"] = JObject.FromObject(new { completed = true, closurePassed = true, faulted = false }),
            ["encounter"] = JObject.FromObject(new { accepted = true, reasons = new string[0] }),
            ["moved"] = true, ["jumped"] = true, ["cameraMoved"] = true, ["finalRelease"] = true, ["controlsRestored"] = true,
            ["lightObserversReleased"] = true, ["lightStructureUnchanged"] = true, ["lightPrePostGuardPassed"] = true,
            ["lightPreparationStartedElapsed"] = 28.0, ["lightPreparationEndedElapsed"] = 28.05,
            ["lightObserversReleaseStartedElapsed"] = 28.06, ["lightObserversReleaseEndedElapsed"] = 28.1,
            ["guardedFrames"] = 100, ["errors"] = new JArray() };
        static object LightDto(JObject value) => value.ToObject(Contract.GetNestedType("EvidenceReport"));
        static void LightCases(Action<string, Action> test)
        {
            test("Explicit Cuca light arm is isolated from full/default and other families", () => {
                Require((bool)InvokeLight("UseLightControl", "cuca-certified-route", "light-control", "CucaLoad"), "Explicit Cuca light rejected.");
                Require(!(bool)InvokeLight("UseLightControl", "cuca-certified-route", null, "CucaLoad") && !(bool)InvokeLight("UseLightControl", "cuca-certified-route", "full", "CucaLoad"), "Default/full Cuca changed.");
                foreach (var values in new[] { new[] { "cuca-certified-route", "unknown", "CucaLoad" }, new[] { "cuca-route-rehearsal", "light-control", "CucaLoad" }, new[] { "cuca-certified-route", "light-control", "Tutorial_Final" } })
                { bool rejected = false; try { InvokeLight("UseLightControl", values.Cast<object>().ToArray()); } catch (ArgumentException) { rejected = true; } Require(rejected, "Unknown/mixed light route accepted."); }
            });
            test("Cuca light functional completion never promotes timing or population", () => {
                var dto = LightDto(LightEvidence()); Require((bool)InvokeLight("LightCompletion", dto), "Complete Cuca light proof rejected.");
                var session = Activator.CreateInstance(Contract, true); Contract.GetProperty("Evidence").SetValue(session, dto);
                Require((bool)Contract.GetProperty("Qualified").GetValue(session) && !(bool)Contract.GetProperty("TimingQualified").GetValue(session) && !(bool)Contract.GetProperty("PopulationVerified").GetValue(session), "Diagnostic and timing/population qualification conflated.");
                Require(!(bool)dto.GetType().GetProperty("routePopulationQualified").GetValue(dto), "Light endpoint evidence promoted population.");
            });
            foreach (string mutation in new[] { "window", "pose", "encounter", "release", "restore", "observers", "structure", "guard", "late-preparation", "nonfinite-preparation", "late-release", "same-frame-release", "release-before-preparation", "capture-promotion", "population-promotion", "camera-promotion", "not-light-capture", "conditions", "errors", "capture-errors" })
            {
                string selected = mutation;
                test("Cuca light rejects missing or malformed " + selected, () => {
                    var row = LightEvidence();
                    switch (selected)
                    {
                        case "window": row["capture"]["fullWindowCompleted"] = false; break;
                        case "pose": row["poses"]["closurePassed"] = false; break;
                        case "encounter": row["encounter"]["accepted"] = false; break;
                        case "release": row["finalRelease"] = false; break;
                        case "restore": row["controlsRestored"] = false; break;
                        case "observers": row["lightObserversReleased"] = false; break;
                        case "structure": row["lightStructureUnchanged"] = false; break;
                        case "guard": row["lightPrePostGuardPassed"] = false; break;
                        case "late-preparation": row["lightPreparationEndedElapsed"] = 30; break;
                        case "nonfinite-preparation": row["lightPreparationStartedElapsed"] = double.NaN; break;
                        case "late-release": row["lightObserversReleaseEndedElapsed"] = 30; break;
                        case "same-frame-release": row["lightObserversReleaseEndedElapsed"] = row["lightObserversReleaseStartedElapsed"]; break;
                        case "release-before-preparation": row["lightObserversReleaseStartedElapsed"] = 28; break;
                        case "capture-promotion": row["capture"]["comparisonValid"] = true; break;
                        case "population-promotion": row["capture"]["populationVerified"] = true; break;
                        case "camera-promotion": row["capture"]["cameraVerified"] = true; break;
                        case "not-light-capture": row["capture"]["conditions"]["lightControl"] = false; break;
                        case "conditions": row["capture"]["conditionsChanged"] = true; break;
                        case "errors": row["errors"] = new JArray("fault"); break;
                        case "capture-errors": row["capture"]["errors"] = new JArray("fault"); break;
                    }
                    Require(!(bool)InvokeLight("LightCompletion", LightDto(row)), "Malformed Cuca light proof accepted.");
                });
            }
        }
        static JObject Manifest() => JObject.FromObject(new { schemaVersion = 1, routeId = "cuca-encounter-v1", scene = "CucaLoad", sourceRevision = new string('a', 40), buildAssemblySha256 = Build, playerBuildGuid = Guid, distributionSha256 = Build, timelineSha256 = Hash(Timeline), seedSha256 = Hash(Seed), quality = "High Fidelity", width = 1920, height = 1080, vsync = 0, targetFrameRate = -1, warmupSeconds = 30, captureSeconds = 120, renderScale = 1, maxLatenessSeconds = .25, cameraMode = "movement-heading-v1", cameraTurnDegreesPerSecond = 30, cameraPaths = new[] { "PlayerCamera/Camera", "MiniMap/Camera" }, counterNames = new[] { "Main Thread" }, bossPath = "Boss", composition = new[] { new { role = "hostile", primaryType = typeof(CucaBoss).FullName, count = 1, active = true, enabled = true, spawned = false }, new { role = "local-player", primaryType = typeof(PlayerManager).FullName, count = 1, active = true, enabled = true, spawned = true } }, poseCheckpoints = new double[] { 0, 30, 60, 90, 120, 150 }.Select(t => new { elapsed = t, position = new float[] { 0, 0, 0 }, cameraRotation = new float[] { 0, 0, 0, 1 }, positionTolerance = 2, cameraAngleTolerance = 10 }).ToArray() });
        static object Parse(JObject manifest, string timeline = Timeline, string seed = Seed) => Contract.GetMethod("ParseManifest").Invoke(null, new object[] { manifest.ToString(Formatting.None), timeline, seed, Build, Guid, Build });
        static JObject Before() => JObject.FromObject(new { active = true, activationCount = 1, projectileAttackCount = 0, createdProjectiles = 0, laserAttackCount = 0, laserActivationCount = 0, retreatCount = 0 });
        static JObject After() => JObject.FromObject(new { active = true, activationCount = 1, projectileAttackCount = 2, createdProjectiles = 6, laserAttackCount = 1, laserActivationCount = 1, retreatCount = 0 });
        static void BossCooldownMutation()
        {
            var root = new GameObject("CucaCertifiedBindingFixture");
            AnimatorController controller = null;
            try
            {
                var playerObject = new GameObject("Player"); playerObject.transform.SetParent(root.transform);
                var player = playerObject.AddComponent<PlayerManager>();
                var bossObject = new GameObject("Boss"); bossObject.transform.SetParent(root.transform);
                var boss = bossObject.AddComponent<CucaBoss>();
                var health = bossObject.AddComponent<HealthManager>(); health.maxHealth = health.currentHealth = 100;
                var animator = bossObject.AddComponent<Animator>(); controller = new AnimatorController(); animator.runtimeAnimatorController = controller;
                bossObject.AddComponent<NavMeshAgent>();
                boss.player = playerObject;
                boss.magicProjectile = new GameObject("Projectile"); boss.magicProjectile.transform.SetParent(root.transform);
                boss.magicSpawnPoint = boss.laserOrigin = bossObject.transform;
                boss.VfxLaser = new GameObject("Vfx"); boss.VfxLaser.transform.SetParent(root.transform);
                boss.lineRenderer = bossObject.AddComponent<LineRenderer>();
                typeof(CucaBoss).GetField("isActive", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(boss, true);
                var type = Contract.GetNestedType("BossBinding", BindingFlags.NonPublic);
                var binding = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { boss, player }, null);
                var matches = type.GetMethod("Matches", BindingFlags.Instance | BindingFlags.NonPublic);
                Require((bool)matches.Invoke(binding, null), "Healthy fixture failed its initial frozen driver proof");
                boss.attackCooldown += 1;
                Require(!(bool)matches.Invoke(binding, null), "Actual AttackTimer attackCooldown mutation escaped frozen driver proof");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (controller != null) UnityEngine.Object.DestroyImmediate(controller);
            }
        }
        static void Counters(JObject before, JObject after, bool expected)
        {
            var method = Contract.GetMethod("ValidateCounters"); var type = method.GetParameters()[0].ParameterType;
            var verdict = JObject.FromObject(method.Invoke(null, new[] { before.ToObject(type), after.ToObject(type) }));
            Require((bool)verdict["accepted"] == expected, "Counter verdict mismatch: " + verdict);
        }
        static void Population(JObject observation, bool expected)
        {
            var method = Contract.GetMethod("ReconcilePopulation"); var value = observation.ToObject(method.GetParameters()[2].ParameterType);
            var verdict = JObject.FromObject(method.Invoke(null, new[] { Parse(Manifest()), (object)10d, value }));
            Require((bool)verdict["accepted"] == expected, "Cuca phase verdict mismatch: " + verdict);
        }
        static JObject Observation()
        {
            var boss = JObject.FromObject(new { objectId = 10, sceneHandle = 3, scene = "CucaLoad", path = "Boss", primaryType = typeof(CucaBoss).FullName, role = "hostile", components = new[] { typeof(CucaBoss).FullName, typeof(HealthManager).FullName }, active = true, enabled = true, spawned = false, supportedBinding = true, currentHealth = 100, maximumHealth = 100 });
            var player = JObject.FromObject(new { objectId = 11, sceneHandle = 3, scene = "CucaLoad", path = "Player", primaryType = typeof(PlayerManager).FullName, role = "local-player", components = new[] { typeof(PlayerManager).FullName }, active = true, enabled = true, spawned = true, supportedBinding = true, currentHealth = 100, maximumHealth = 100 });
            var before = new JObject { ["activeScene"] = "CucaLoad", ["frame"] = 10, ["actors"] = new JArray(boss, player), ["spawnerAssociations"] = new JArray(), ["unclassifiedAnimators"] = new JArray() }; var after = (JObject)before.DeepClone(); after["frame"] = 160;
            var history = JObject.FromObject(new { startedFrame = 0, sourceFaulted = false, overflow = false, dropped = 0, failedAttempts = 0, preexistingAiIds = new int[0], unmatchedAiIds = new int[0], orphanLiveAttempts = new int[0], attempts = new object[0], events = new object[0], currentAiCount = 0, matchedAiCount = 0 });
            var subjects = new JArray(new JObject { ["componentId"] = 20, ["rootId"] = 10, ["ownerRootId"] = 10, ["componentType"] = typeof(CucaBoss).FullName }, new JObject { ["componentId"] = 21, ["rootId"] = 11, ["ownerRootId"] = 11, ["componentType"] = typeof(PlayerManager).FullName });
            return new JObject { ["before"] = before, ["after"] = after, ["aiBefore"] = history, ["aiAfter"] = history.DeepClone(), ["contextViolationMask"] = 0, ["lifecycle"] = JObject.FromObject(new { startedFrame = 10, endedFrame = 160, startedTime = 10, endedTime = 160, sourceFaulted = false, overflow = false, dropped = 0, notifications = 0, events = new object[0], initiallyBoundSubjects = subjects }) };
        }
        static string Hash(string value) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
        static void Rejected(Action body) { try { body(); } catch (TargetInvocationException e) when (e.InnerException is ArgumentException) { return; } throw new InvalidOperationException("Invalid manifest accepted"); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
