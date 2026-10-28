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
    public static class PerformanceLightRouteValidation
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static object Invoke(string method, params object[] args)
        {
            var entry = typeof(PerformanceCertifiedRouteProcess).GetMethod(method, Flags);
            Require(entry != null, "Light route policy is not implemented: " + method);
            try { return entry.Invoke(null, args); } catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
        static void Set(object value, string name, object data)
        {
            var field = value.GetType().GetField(name, Flags); Require(field != null, "Light route evidence field is not implemented: " + name); field.SetValue(value, data);
        }
        static PerformanceActorCensus.Report Census() => new PerformanceActorCensus.Report {
            activeScene = "Tutorial_Final", actors = new[] {
                new PerformanceActorCensus.Actor { objectId = 1, sceneHandle = 17, scene = "Tutorial_Final", path = "Player", primaryType = "PlayerManager", role = "local-player", components = new[] { "PlayerManager" }, active = true, enabled = true, spawned = true, supportedBinding = true, networkObjectId = 10, currentHealth = 100, maximumHealth = 100, dead = false, position = new float[] { 0, 0, 0 } },
                new PerformanceActorCensus.Actor { objectId = 2, sceneHandle = 17, scene = "Tutorial_Final", path = "Saci", primaryType = "AISaciCharacterManager", role = "hostile", components = new[] { "AISaciCharacterManager" }, active = true, enabled = true, spawned = true, supportedBinding = true, networkObjectId = 11, currentHealth = 100, maximumHealth = 100, dead = false, position = new float[] { 1, 0, 0 } } },
            unclassifiedAnimators = Array.Empty<PerformanceActorCensus.Workload>() };
        static PerformanceBoundedRoutePopulation.ActorGroup[] Composition() => new[] {
            new PerformanceBoundedRoutePopulation.ActorGroup { role = "local-player", primaryType = "PlayerManager", count = 1, active = true, enabled = true, spawned = true },
            new PerformanceBoundedRoutePopulation.ActorGroup { role = "hostile", primaryType = "AISaciCharacterManager", count = 1, active = true, enabled = true, spawned = true } };
        static PerformanceCertifiedRouteProcess.EvidenceReport Evidence()
        {
            var row = new PerformanceCertifiedRouteProcess.EvidenceReport {
                manifest = new PerformanceCertifiedRouteProcess.ManifestData { scene = "Tutorial_Final" },
                capture = new CaptureResult { fullWindowCompleted = true },
                poses = new PerformanceRoutePoseProof.Report { completed = true, closurePassed = true },
                moved = true, jumped = true, cameraMoved = true, finalRelease = true, controlsRestored = true };
            Set(row, "measurementArm", "light-control"); Set(row, "lightObserversReleased", true);
            Set(row, "lightObserversReleaseStartedElapsed", 28.01); Set(row, "lightObserversReleaseEndedElapsed", 28.02);
            Set(row, "lightStructureUnchanged", true); Set(row, "lightPrePostGuardPassed", true);
            Set(row, "lightPreparationStartedElapsed", 28.0); Set(row, "lightPreparationEndedElapsed", 28.01);
            return row;
        }
        static bool ProcessParse(string arm)
        {
            string root = Path.Combine(Path.GetTempPath(), "KarawaraLightFixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string path = Path.Combine(root, "config.json");
            try
            {
                var sample = (JObject)typeof(PerformanceCertifiedRouteValidation).GetMethod("Sample", Flags).Invoke(null, null);
                var config = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/video1-heading-authored-skills-template.json"));
                config["mode"] = "video1-certified-route"; config["certifiedManifestJson"] = sample["manifest"].ToString(Formatting.None);
                config["rehearsal"]["routeId"] = "video1-exploration-baseline-v1"; config["rehearsal"]["outputRoot"] = Path.Combine(root, "output");
                if (arm != null) config["measurementArm"] = arm;
                File.WriteAllText(path, config.ToString(Formatting.None));
                return PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", Path.Combine(root, "saves") }, Path.Combine(root, "personal")).success;
            }
            finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(root); }
        }
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode light policy fixture required.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true, detail = "Pure policy/synthetic structural evidence only" }); } catch (Exception error) { cases.Add(new { name, passed = false, detail = error.ToString() }); } };
            test("Process DTO explicitly carries a nullable measurement arm", () => Require(typeof(PerformanceProcessConfig).GetField("measurementArm")?.FieldType == typeof(string), "Process measurementArm field is not implemented."));
            test("Harness preserves null/full legacy Video1 while rejecting light and unknown arms", () => Require(ProcessParse(null) && ProcessParse("full") && !ProcessParse("light-control") && !ProcessParse("unknown"), "Measurement arm request policy changed legacy full behavior or accepted unsupported light mode."));
            test("Only explicit Tutorial certified light arm selects light", () => {
                Require((bool)Invoke("UseLightControl", "tutorial-certified-route", "light-control", "Tutorial_Final"), "Explicit Tutorial light rejected.");
                Require(!(bool)Invoke("UseLightControl", "tutorial-certified-route", null, "Tutorial_Final") && !(bool)Invoke("UseLightControl", "video1-certified-route", "full", "Video1"), "Default/full arm changed.");
                foreach (var mode in new[] { "video1-certified-route", "video1-route-attribution", "tutorial-ui-certified-route" })
                {
                    bool rejected = false; try { Invoke("UseLightControl", mode, "light-control", "Tutorial_Final"); } catch (ArgumentException) { rejected = true; }
                    Require(rejected, "Unsupported light family accepted: " + mode);
                }
            });
            test("Warm structural baseline permits routed movement without changing identity", () => {
                var before = Census(); var after = Census(); after.actors[0].position[0] = 100;
                Require((bool)Invoke("LightStructuralMatch", before, after, Composition()), "Movement was confused with changed actor structure.");
            });
            foreach (string mutation in new[] { "identity", "scene", "type", "role", "disabled", "dead", "nonfinite-health", "unsupported", "component", "duplicate", "missing", "wrong-composition" })
                test("Light endpoint structure rejects " + mutation, () => {
                    var before = Census(); var after = Census(); var composition = Composition();
                    switch (mutation)
                    {
                        case "identity": after.actors[1].objectId = 3; break;
                        case "scene": after.actors[1].sceneHandle++; break;
                        case "type": after.actors[1].primaryType = "Other"; break;
                        case "role": after.actors[1].role = "ambient-npc"; break;
                        case "disabled": after.actors[1].enabled = false; break;
                        case "dead": after.actors[1].dead = true; break;
                        case "nonfinite-health": after.actors[1].currentHealth = float.NaN; break;
                        case "unsupported": after.actors[1].supportedBinding = false; break;
                        case "component": after.actors[1].components = new[] { "Other" }; break;
                        case "duplicate": after.actors[1].objectId = 1; break;
                        case "missing": after.actors = after.actors.Take(1).ToArray(); break;
                        case "wrong-composition": composition[1].count = 2; break;
                    }
                    Require(!(bool)Invoke("LightStructuralMatch", before, after, composition), "Malformed endpoint structure accepted.");
                });
            test("Light completion permits functional control but never certified timing/population", () => {
                var evidence = Evidence(); Require((bool)Invoke("LightCompletion", evidence), "Complete diagnostic control rejected.");
                Require(!evidence.routePopulationQualified, "Light control promoted population.");
                var session = (PerformanceCertifiedRouteProcess)Activator.CreateInstance(typeof(PerformanceCertifiedRouteProcess), true);
                typeof(PerformanceCertifiedRouteProcess).GetProperty("Evidence").SetValue(session, evidence);
                Require(session.Qualified && !session.TimingQualified && !session.PopulationVerified, "Control completion and timing/population qualification were conflated.");
            });
            foreach (string mutation in new[] { "window", "pose", "release", "restore", "observers", "structure", "guard", "late-preparation", "nonfinite-preparation", "late-observer-release", "nonfinite-observer-release", "reversed-observer-release", "errors", "capture-errors" })
                test("Light completion rejects missing " + mutation, () => {
                    var evidence = Evidence();
                    switch (mutation)
                    {
                        case "window": evidence.capture.fullWindowCompleted = false; break;
                        case "pose": evidence.poses.closurePassed = false; break;
                        case "release": evidence.finalRelease = false; break;
                        case "restore": evidence.controlsRestored = false; break;
                        case "observers": Set(evidence, "lightObserversReleased", false); break;
                        case "structure": Set(evidence, "lightStructureUnchanged", false); break;
                        case "guard": Set(evidence, "lightPrePostGuardPassed", false); break;
                        case "late-preparation": Set(evidence, "lightPreparationEndedElapsed", 30.0); break;
                        case "nonfinite-preparation": Set(evidence, "lightPreparationStartedElapsed", double.NaN); break;
                        case "late-observer-release": Set(evidence, "lightObserversReleaseEndedElapsed", 30.0); break;
                        case "nonfinite-observer-release": Set(evidence, "lightObserversReleaseStartedElapsed", double.NaN); break;
                        case "reversed-observer-release": Set(evidence, "lightObserversReleaseEndedElapsed", 28.0); break;
                        case "errors": evidence.errors.Add("fault"); break;
                        case "capture-errors": evidence.capture.errors.Add("fault"); break;
                    }
                    Require(!(bool)Invoke("LightCompletion", evidence), "Incomplete diagnostic control accepted.");
                });
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/light-route-policy-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), completed = true, passed = cases.All(row => (bool)row.GetType().GetProperty("passed").GetValue(row)), cases, boundary = "Pure policy and synthetic endpoint structure, not live route, transient population provenance, timing or G1B acceptance." }, Formatting.Indented));
            Debug.Log("Light route fixture: " + path); return path;
        }
    }
}
