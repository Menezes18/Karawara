using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGKarawara.DevTools.Performance;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class PerformanceTutorialRouteValidation
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
        [MenuItem("Karawara/Validation/TOBE/Validate Tutorial Route Contract")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("EditMode contract fixture required.");
            var cases = new List<object>();
            Action<string, Action> test = (name, body) => { try { body(); cases.Add(new { name, passed = true }); } catch (Exception error) { cases.Add(new { name, passed = false, error = error.ToString() }); } };
            test("Explicit Tutorial certified mode recognized", () => Require(Mode("IsCertifiedRouteMode", "tutorial-certified-route"), "Tutorial mode missing"));
            test("Tutorial certified mode is not Video1", () => Require(!Mode("IsVideoMode", "tutorial-certified-route"), "Tutorial was classified as Video1"));
            test("Video1 certified mode preserves its scene", () => Require(Mode("IsVideoMode", "video1-certified-route"), "Video1 classification regressed"));
            test("Parse source-bound Tutorial manifest and frozen poses", () => Parse(Sample()));
            test("Legacy Video1 snapshot does not inject a null pose field", () =>
            {
                var source = (JObject)typeof(PerformanceCertifiedRouteValidation).GetMethod("Sample", Private).Invoke(null, null);
                var frozen = (PerformanceCertifiedRouteProcess.FrozenManifest)Parse(source);
                Require(JObject.FromObject(frozen.Snapshot)["poseCheckpoints"] == null, "Legacy raw manifest equality changed through an injected null field");
            });
            foreach (string mutation in new[] { "mixed-pair", "wrong-seed-scene", "missing-poses", "loose-poses", "missing-npc", "static-generation", "late-generation", "wrong-build", "wrong-distribution", "wrong-input" })
                test("Reject Tutorial contract: " + mutation, () =>
                {
                    var sample = Sample(); var manifest = (JObject)sample["manifest"];
                    switch (mutation)
                    {
                        case "mixed-pair": manifest["scene"] = "Video1"; break;
                        case "wrong-seed-scene": var seed = JObject.Parse((string)sample["seed"]); seed["sceneIndex"] = 2; sample["seed"] = seed.ToString(Formatting.None); manifest["seedSha256"] = Hash((string)sample["seed"]); break;
                        case "missing-poses": manifest.Remove("poseCheckpoints"); break;
                        case "loose-poses": manifest["poseCheckpoints"][0]["positionTolerance"] = 3; break;
                        case "missing-npc": manifest["missionNpcPath"] = ""; break;
                        case "static-generation": manifest["expectedCreations"] = 0; break;
                        case "late-generation": manifest["transitionLatestSeconds"] = 50; break;
                        case "wrong-build": manifest["buildAssemblySha256"] = new string('B', 64); break;
                        case "wrong-distribution": manifest["distributionSha256"] = new string('E', 64); break;
                        case "wrong-input": manifest["timelineSha256"] = new string('F', 64); break;
                    }
                    Reject(() => Parse(sample));
                });
            test("Parse exact Tutorial mode/manifest/seed request", () => Process(false));
            test("Reject Tutorial manifest under Video1 mode", () => Process(true));
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/performance-tutorial-route-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { completed = true, passed = cases.All(c => (bool)c.GetType().GetProperty("passed").GetValue(c)), cases,
                boundary = "Tutorial manifest/mode/seed/pose contract only; not live scene coverage, route, timing or G1B acceptance." }, Formatting.Indented));
            Debug.Log("Tutorial route fixture: " + path);
        }
        static bool Mode(string method, string mode) => (bool)typeof(PerformanceProcessHarness).GetMethod(method, Private).Invoke(null, new object[] { mode });
        static JObject Sample()
        {
            var sample = (JObject)typeof(PerformanceCertifiedRouteValidation).GetMethod("Sample", Private).Invoke(null, null);
            var frozen = JObject.Parse(File.ReadAllText("Docs/Validation/TOBE/Execution/tutorial-progression-frozen-boundary-checkpoints-v1.json"));
            sample["timeline"] = frozen["rehearsal"]["timelineJson"].DeepClone();
            sample["seed"] = File.ReadAllText("Docs/Validation/TOBE/Execution/tutorial-progression-seed-v1.json");
            var manifest = sample["manifest"];
            manifest["scene"] = "Tutorial_Final"; manifest["routeId"] = "tutorial-exploration-v1";
            manifest["timelineSha256"] = Hash((string)sample["timeline"]); manifest["seedSha256"] = Hash((string)sample["seed"]);
            manifest["missionNpcPath"] = "Capivara_Prefab/NPC TESTE/GameObject"; manifest["missionChoice"] = 1; manifest["expectedCreations"] = 1;
            manifest["transitionEarliestSeconds"] = 23; manifest["transitionLatestSeconds"] = 24.5;
            var initial = new JArray(
                new JObject { ["role"] = "local-player", ["primaryType"] = "RPGKarawara.PlayerManager", ["count"] = 1, ["active"] = true, ["enabled"] = true, ["spawned"] = true },
                new JObject { ["role"] = "ambient-npc", ["primaryType"] = "RPGKarawara.NPCDialogue", ["count"] = 1, ["active"] = true, ["enabled"] = true, ["spawned"] = false },
                new JObject { ["role"] = "world-visual", ["primaryType"] = "UnityEngine.Animator", ["count"] = 1, ["active"] = true, ["enabled"] = true, ["spawned"] = false });
            manifest["initialComposition"] = initial; var settled = (JArray)initial.DeepClone();
            settled.Add(new JObject { ["role"] = "hostile", ["primaryType"] = "RPGKarawara.AISaciCharacterManager", ["count"] = 1, ["active"] = true, ["enabled"] = true, ["spawned"] = true });
            manifest["settledComposition"] = settled; manifest["poseCheckpoints"] = frozen["rehearsal"]["checkpoints"].DeepClone();
            return sample;
        }
        static object Parse(JObject sample)
        {
            var manifest = sample["manifest"];
            return PerformanceCertifiedRouteProcess.ParseManifest(manifest.ToString(Formatting.None), (string)sample["timeline"], (string)sample["seed"],
                new string('A', 64), "11111111111111111111111111111111", new string('D', 64));
        }
        static void Process(bool wrongMode)
        {
            var sample = Sample(); string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Karawara-TutorialContract-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "config.json");
                var config = new PerformanceProcessConfig { mode = wrongMode ? "video1-certified-route" : "tutorial-certified-route", width = 1920, height = 1080, quality = "High Fidelity", vsync = 0, targetFrameRate = -1,
                    certifiedManifestJson = sample["manifest"].ToString(Formatting.None), rehearsal = new TutorialRehearsalConfig { routeId = "tutorial-exploration-v1", durationSeconds = 150, maxLatenessSeconds = .25,
                        timelineJson = (string)sample["timeline"], seedSaveJson = (string)sample["seed"], outputRoot = Path.Combine(root, "output") } };
                File.WriteAllText(path, JsonConvert.SerializeObject(config));
                var result = PerformanceProcessHarness.ParseRequest(new[] { "-karawaraPerformanceConfig", path, "-karawaraSaveRoot", Path.Combine(root, "save") }, Path.Combine(root, "personal"));
                Require(result.handled && result.success != wrongMode, "Tutorial mode/manifest request mismatch: " + string.Join(";", result.errors));
            }
            finally
            {
                if (!root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Refuse cleanup outside generated temporary root");
                Directory.Delete(root, true);
            }
        }
        static void Reject(Action body) { try { body(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Invalid Tutorial contract accepted"); }
        static string Hash(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", ""); }
        static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    }
}
