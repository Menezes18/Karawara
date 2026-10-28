using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    // Parsing/comparison regressions only. This fixture never starts a host or executes the process bootstrap.
    public static class HostProcessRegressionValidation
    {
        [Serializable] sealed class Case { public string name, detail; public bool passed; }
        [Serializable] sealed class Report
        {
            public string utc;
            public bool completed, passed, cleanedUp;
            public List<Case> cases = new List<Case>();
            public List<string> limits = new List<string>();
        }

        [MenuItem("Karawara/Validation/TOBE/Validate Host Process Regression Contracts")]
        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Karawara-HostProcessContracts-" + Guid.NewGuid().ToString("N")));
            string personal = Path.Combine(root, "personal-unused"), save = Path.Combine(root, "saves"), output = Path.Combine(root, "output");
            string expectedPath = Path.Combine(root, "expected.json");
            Test(report, "No activation has no I/O or handled request", () =>
            {
                var result = Parse(new[] { "-karawaraSaveRoot", save }, personal);
                Require(!(bool)result["handled"] && !Directory.Exists(root), "Missing activation must return without creating any QA path.");
            });
            Test(report, "Unknown action fails before I/O", () => Reject(Args("unknown", save, output), personal, root));
            Test(report, "Missing action fails before I/O", () => Reject(new[] { "-karawaraHostSaveRegression" }, personal, root));
            Test(report, "Duplicate action fails before I/O", () => Reject(Args("write-three", save, output).Concat(new[] { "-karawaraHostSaveRegression", "read" }).ToArray(), personal, root));
            Test(report, "Missing explicit save root fails before I/O", () => Reject(new[] { "-karawaraHostSaveRegression", "write-three", "-karawaraHostSaveOutput", output }, personal, root));
            Test(report, "Missing explicit output root fails before I/O", () => Reject(new[] { "-karawaraHostSaveRegression", "write-three", "-karawaraSaveRoot", save }, personal, root));
            foreach (string badSave in new[] { "relative-save", personal, Path.Combine(personal, "nested"), root })
            {
                string candidate = badSave;
                Test(report, "Reject invalid/overlapping save root: " + candidate, () => Reject(Args("write-three", candidate, output), personal, root));
            }
            foreach (string badOutput in new[] { "relative-output", personal, save, Path.Combine(save, "nested") })
            {
                string candidate = badOutput;
                Test(report, "Reject invalid/overlapping output root: " + candidate, () => Reject(Args("write-three", save, candidate), personal, root));
            }
            Test(report, "Duplicate save/output roots fail before I/O", () =>
            {
                Reject(Args("write-three", save, output).Concat(new[] { "-karawaraSaveRoot", save }).ToArray(), personal, root);
                Reject(Args("write-three", save, output).Concat(new[] { "-karawaraHostSaveOutput", output }).ToArray(), personal, root);
            });
            Test(report, "Writer parses exactly three slots without creating host/save/output", () =>
            {
                var result = Parse(Args("write-three", save, output), personal);
                Require((bool)result["handled"] && (bool)result["success"] && (string)result["action"] == "write-three", "Three-slot writer request must parse.");
                Require((string)result["saveRoot"] == save && (string)result["outputRoot"] == output, "Validated roots must be returned exactly.");
                Require(((JArray)result["slots"]).Values<int>().SequenceEqual(new[] { 1, 2, 3 }), "Writer must target slots 1, 2 and 3, not a caller-selected subset.");
                Require(!Directory.Exists(root), "Parsing must not create host/save/output artifacts.");
            });
            Test(report, "Reader requires selected slot and expected manifest", () =>
            {
                Reject(Args("read", save, output), personal, root);
                Reject(Args("read", save, output).Concat(new[] { "-karawaraHostSaveSlot", "1" }).ToArray(), personal, root);
                Reject(Args("read", save, output).Concat(new[] { "-karawaraHostSaveExpected", expectedPath }).ToArray(), personal, root);
            });
            foreach (string invalidSlot in new[] { "0", "4", "unknown" })
            {
                string candidate = invalidSlot;
                Test(report, "Reader rejects invalid slot: " + candidate, () => Reject(ReadArgs(save, output, expectedPath, candidate), personal, root));
            }
            Test(report, "Reader rejects relative or missing expected file before I/O", () =>
            {
                Reject(ReadArgs(save, output, "relative.json", "1"), personal, root);
                Reject(ReadArgs(save, output, expectedPath, "1"), personal, root);
            });
            Test(report, "Each reader parses one selected slot without modifying manifest/save/output", () =>
            {
                Directory.CreateDirectory(root);
                string json = "{\"schemaVersion\":1,\"fixtureOnly\":true}";
                File.WriteAllText(expectedPath, json);
                for (int slot = 1; slot <= 3; slot++)
                {
                    var result = Parse(ReadArgs(save, output, expectedPath, slot.ToString(System.Globalization.CultureInfo.InvariantCulture)), personal);
                    Require((bool)result["success"] && (string)result["action"] == "read" && (int)result["slot"] == slot, "Explicit reader slot must parse.");
                    Require((string)result["expectedPath"] == expectedPath && ((JArray)result["slots"]).Values<int>().SequenceEqual(new[] { slot }), "Reader must retain only the requested slot and manifest path.");
                }
                Require(File.ReadAllText(expectedPath) == json && !Directory.Exists(save) && !Directory.Exists(output), "Parsing reads no gameplay state and mutates no inputs or outputs.");
            });
            Test(report, "Duplicate reader slot/expected flags are rejected", () =>
            {
                Require(!(bool)Parse(ReadArgs(save, output, expectedPath, "1").Concat(new[] { "-karawaraHostSaveSlot", "2" }).ToArray(), personal)["success"], "Duplicate selected slot must fail.");
                Require(!(bool)Parse(ReadArgs(save, output, expectedPath, "1").Concat(new[] { "-karawaraHostSaveExpected", expectedPath }).ToArray(), personal)["success"], "Duplicate expected manifest must fail.");
                Require(!Directory.Exists(save) && !Directory.Exists(output), "Malformed reads must not create output/save directories.");
            });
            Test(report, "Supported live fields compare exactly for all three slots", () =>
            {
                for (int slot = 1; slot <= 3; slot++) Require(Equal(Fixture(slot), Fixture(slot)), "Equal supported live fields must pass for slot " + slot);
            });
            foreach (string field in new[] { "characterName", "sceneIndex", "xPosition", "yPosition", "zPosition", "currentHealth", "vitality", "endurance" })
            {
                string candidate = field;
                Test(report, "Successful DTO load with wrong live field fails: " + candidate, () =>
                {
                    var actual = Fixture(3);
                    var member = typeof(CharacterSaveData).GetField(candidate);
                    if (member.FieldType == typeof(string)) member.SetValue(actual, "Wrong slot");
                    else if (member.FieldType == typeof(int)) member.SetValue(actual, (int)member.GetValue(actual) + 1);
                    else member.SetValue(actual, (float)member.GetValue(actual) + 1);
                    Require(!Equal(actual, Fixture(3)), "Wrong live field must reject host equality.");
                });
            }
            Test(report, "Null/non-finite live snapshot never compares equal", () =>
            {
                Require(!Equal(null, Fixture(1)) && !Equal(Fixture(1), null), "Missing host snapshots cannot pass.");
                var actual = Fixture(1); var expected = Fixture(1);
                actual.xPosition = expected.xPosition = float.PositiveInfinity;
                Require(!Equal(actual, expected), "Matching invalid non-finite poses cannot pass.");
            });
            Test(report, "DTO-only stamina/time do not masquerade as live fields", () =>
            {
                var actual = Fixture(1); actual.currentStamina += 2; actual.secondsPlayed += 2;
                Require(Equal(actual, Fixture(1)), "Supported host comparison intentionally excludes fields with no current live capture/apply path; transport equality remains separate.");
            });
            Test(report, "Complete host/skills/cleanup result is full pass", () =>
            {
                var result = Outcome(true, true, true, true, true);
                Require((bool)result["success"] && (bool)result["supportedHostPassed"] && (bool)result["skillRoundTripPassed"] && (string)result["status"] == "Passed" && (int)result["exitCode"] == 0, "Full pass requires all observed contracts.");
            });
            Test(report, "Skill resolution mismatch is explicit separate outcome", () =>
            {
                var result = Outcome(true, true, true, false, true);
                Require(!(bool)result["success"] && (bool)result["supportedHostPassed"] && !(bool)result["skillRoundTripPassed"] && (string)result["status"] == "LegacySkillsUnresolved" && (int)result["exitCode"] == 3, "Legacy skill mismatch retains supported host proof without claiming full success.");
            });
            Test(report, "World/pose/once/cleanup failures cannot become skill-only limitation", () =>
            {
                foreach (bool[] flags in new[] { new[] { false, true, true, false, true }, new[] { true, false, true, false, true }, new[] { true, true, false, false, true }, new[] { true, true, true, true, false } })
                {
                    var result = Outcome(flags[0], flags[1], flags[2], flags[3], flags[4]);
                    Require(!(bool)result["success"] && !(bool)result["supportedHostPassed"] && (string)result["status"] == "Failed" && (int)result["exitCode"] == 1, "Material host/cleanup failure must be a failed outcome.");
                }
            });

            Test(report, "Actual runtime warnings do not invalidate supported host outcome", () =>
            {
                WithLogCollector(root, save, output, collector =>
                {
                    Debug.LogWarning("[TOBE HostLogFixture] deliberate allowed warning");
                    Require(LogFailures(collector).Length == 0, "Production warnings remain diagnostic, not runtime failure.");
                    var result = ObservedOutcome(collector);
                    Require((bool)result["success"] && (bool)result["supportedHostPassed"], "Warning-only real collector must permit otherwise passing observed outcome.");
                });
            });
            Test(report, "Actual production Error event rejects otherwise passing host result", () =>
            {
                WithLogCollector(root, save, output, collector =>
                {
                    Debug.LogError("[TOBE HostLogFixture] deliberate production error");
                    Require(LogFailures(collector).Any(message => message.Contains("deliberate production error")), "Actual Application Error callback must be preserved.");
                    RejectObservedOutcome(collector);
                });
            });
            Test(report, "Actual production Exception event rejects otherwise passing host result", () =>
            {
                WithLogCollector(root, save, output, collector =>
                {
                    Debug.LogException(new InvalidOperationException("[TOBE HostLogFixture] deliberate production exception"));
                    Require(LogFailures(collector).Any(message => message.Contains("deliberate production exception")), "Actual Application Exception callback must be preserved.");
                    RejectObservedOutcome(collector);
                });
            });
            Test(report, "Actual production Assert event rejects otherwise passing host result", () =>
            {
                WithLogCollector(root, save, output, collector =>
                {
                    Debug.LogAssertion("[TOBE HostLogFixture] deliberate production assertion");
                    Require(LogFailures(collector).Any(message => message.Contains("deliberate production assertion")), "Actual Application Assert callback must be preserved.");
                    RejectObservedOutcome(collector);
                });
            });
            Test(report, "Collector cleanup unsubscribes actual runtime callback", () =>
            {
                WithLogCollector(root, save, output, collector =>
                {
                    Debug.LogError("[TOBE HostLogFixture] before disposal");
                    string[] before = LogFailures(collector);
                    Require(before.Any(message => message.Contains("before disposal")), "Real subscribed callback must capture before cleanup.");
                    ((IDisposable)collector).Dispose();
                    Debug.LogError("[TOBE HostLogFixture] after disposal must not be captured");
                    Require(LogFailures(collector).SequenceEqual(before), "Disposed collector must receive no subsequent real Application log callback.");
                });
            });
            Test(report, "Writer accepts a finite actual recent stable position window", () =>
            {
                Vector3 p = new Vector3(-316, 170, 593);
                Require(Stable(new[] { p, p + Vector3.right * .01f, p + Vector3.up * .01f, p + Vector3.forward * .02f, p }), "Finite stable recent positions should satisfy capture precondition.");
            });
            Test(report, "Writer rejects sliding and return-to-origin position windows", () =>
            {
                Vector3 p = new Vector3(-316, 170, 593);
                Require(!Stable(new[] { p, p + Vector3.right * .05f, p + Vector3.right * .10f, p + Vector3.right * .15f, p + Vector3.right * .20f }), "Grounding alone does not certify a stationary pose.");
                Require(!Stable(new[] { p, p + Vector3.right * .20f, p + Vector3.right * .10f, p, p }), "Matching endpoints cannot hide movement within the recent window.");
            });
            Test(report, "Writer rejects missing/short/non-finite position windows", () =>
            {
                Require(!Stable(null) && !Stable(new[] { Vector3.zero, Vector3.zero }), "A recent actual five-sample window is required.");
                Require(!Stable(new[] { Vector3.zero, Vector3.zero, new Vector3(float.NaN, 0, 0), Vector3.zero, Vector3.zero }), "Non-finite terrain observations cannot pass stable-pose validation.");
            });

            report.limits.Add("Reflection contract fixture only; it never starts a host, calls LoadGame, creates gameplay saves, or runs the runtime initializer.");
            report.limits.Add("Expected file here is only a parser path fixture. Semantic manifest validation and three-slot data/skills preservation require the runtime harness and actual process reports.");
            report.limits.Add("Ordinary Release ignores activation only when verified through actual Release compilation/process evidence; no Editor simulation proves that gate.");
            report.limits.Add("Actual QA Release write-three then fresh reader processes, grounding/HUD/apply-once and unresolved Skill[] outcomes remain pending.");
            report.limits.Add("Runtime log regression intentionally emits labeled [TOBE HostLogFixture] Error/Exception/Assert callbacks. These expected injected console failures are complementary fixture evidence, never zero-runtime-error gameplay proof.");
            try
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Require(root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("Karawara-HostProcessContracts-", StringComparison.Ordinal), "Cleanup target must be this fixture-owned absolute temporary directory.");
                if (Directory.Exists(root)) Directory.Delete(root, true);
                report.cleanedUp = !Directory.Exists(root);
            }
            catch (Exception error) { report.cases.Add(new Case { name = "Cleanup", detail = error.ToString() }); }
            report.completed = true; report.passed = report.cleanedUp && report.cases.All(c => c.passed);
            string path = Path.GetFullPath("Docs/Validation/TOBE/Execution/host-process-fixture-" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            Debug.Log("Host process contract fixture result: " + path);
            if (!report.passed) Debug.LogError("Host process contract fixture FAILED; " + path);
        }

        static string[] Args(string action, string save, string output) => new[] { "-karawaraHostSaveRegression", action, "-karawaraSaveRoot", save, "-karawaraHostSaveOutput", output };
        static string[] ReadArgs(string save, string output, string expected, string slot) => Args("read", save, output).Concat(new[] { "-karawaraHostSaveSlot", slot, "-karawaraHostSaveExpected", expected }).ToArray();
        static void Reject(string[] args, string personal, string untouchedRoot)
        {
            var result = Parse(args, personal);
            Require((bool)result["handled"] && !(bool)result["success"] && !Directory.Exists(untouchedRoot), "Invalid explicit request must fail before I/O.");
        }
        static JObject Parse(string[] args, string personal) => Json("ParseRequest", new object[] { args, personal });
        static JObject Outcome(bool scalars, bool spawn, bool once, bool skills, bool cleanup) => Json("EvaluateOutcome", new object[] { scalars, spawn, once, skills, cleanup });
        static bool Equal(CharacterSaveData actual, CharacterSaveData expected) => (bool)Invoke("SupportedScalarsEqual", new object[] { actual, expected });
        static bool Stable(Vector3[] positions) => (bool)Invoke("IsStableGroundWindow", new object[] { positions });
        static JObject ObservedOutcome(object collector) => Json("EvaluateObservedOutcome", new object[] { true, true, true, true, true, collector });
        static void RejectObservedOutcome(object collector)
        {
            var result = ObservedOutcome(collector);
            Require(!(bool)result["success"] && !(bool)result["supportedHostPassed"] && (string)result["status"] == "Failed" && (int)result["exitCode"] == 1, "Observed production runtime failure must reject an otherwise passing process outcome.");
        }
        static string[] LogFailures(object collector)
        {
            var property = collector.GetType().GetProperty("Failures", BindingFlags.Instance | BindingFlags.Public);
            Require(property != null, "Collector must expose preserved runtime failures.");
            return ((System.Collections.IEnumerable)property.GetValue(collector)).Cast<object>().Select(value => value.ToString()).ToArray();
        }
        static void WithLogCollector(string root, string save, string output, Action<object> test)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.PersistenceQA.HostRuntimeLogCollector")).FirstOrDefault(t => t != null);
            Require(type != null && typeof(IDisposable).IsAssignableFrom(type), "Actual Application runtime-error collector implementation missing.");
            string[] files = Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>();
            bool listening = Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;
            var collector = Activator.CreateInstance(type);
            try { test(collector); }
            finally
            {
                ((IDisposable)collector).Dispose();
                Require(listening == (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening), "Collector regression must not change host state.");
                Require(!Directory.Exists(save) && !Directory.Exists(output) && files.SequenceEqual(Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray() : Array.Empty<string>()), "Actual log collector must have no save/output/file side effects.");
            }
        }
        static JObject Json(string method, object[] args) => JObject.Parse(JsonConvert.SerializeObject(Invoke(method, args)));
        static object Invoke(string method, object[] args)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.PersistenceQA.HostSaveProcessRegression")).FirstOrDefault(t => t != null);
            Require(type != null, "Actual-host fresh-process QA harness implementation missing.");
            var member = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Require(member != null, method + " public static contract missing.");
            return member.Invoke(null, args);
        }
        static CharacterSaveData Fixture(int slot) => new CharacterSaveData
        {
            characterName = "QA Host Slot " + slot, sceneIndex = 1,
            xPosition = -316.62823f + slot, yPosition = 170.30302f, zPosition = 593.5127f,
            currentHealth = 60 + 10 * slot, currentStamina = 40.5f + slot, secondsPlayed = 100.25f + slot,
            vitality = 5 + 5 * slot, endurance = 10 + slot
        };
        static void Test(Report report, string name, Action test)
        {
            var result = new Case { name = name };
            try { test(); result.passed = true; result.detail = "passed"; }
            catch (Exception error) { result.detail = error.ToString(); }
            report.cases.Add(result);
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
