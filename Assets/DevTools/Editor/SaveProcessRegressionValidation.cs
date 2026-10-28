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
    public static class SaveProcessRegressionValidation
    {
        [Serializable] sealed class Case { public string name, detail; public bool passed; }
        [Serializable] sealed class Report { public string utc; public bool completed, passed, cleanedUp; public List<Case> cases = new List<Case>(); }
        [MenuItem("Karawara/Validation/TOBE/Validate Save Process Regression")]
        public static void Run()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("O") };
            string root = Path.Combine(Path.GetTempPath(), "Karawara-SaveProcess-" + Guid.NewGuid().ToString("N"));
            string personal = Path.Combine(root, "personal-unused");
            Test(report, "Missing flag performs no operation", () =>
            {
                var result = Execute(new[] { "-karawaraSaveRoot", Path.Combine(root, "no-flag") }, personal);
                Require(!(bool)result["handled"] && !Directory.Exists(root), "No activation must create no files/directories.");
            });
            foreach (string badRoot in new[] { "relative-path", personal })
                Test(report, "Invalid root rejected before I/O: " + badRoot, () =>
                {
                    var result = Execute(new[] { "-karawaraSaveRegression", "write", "-karawaraSaveRoot", badRoot }, personal);
                    Require((bool)result["handled"] && !(bool)result["success"] && !Directory.Exists(root), "Invalid root must fail without I/O.");
                });
            Test(report, "Missing root rejected before I/O", () =>
            {
                var result = Execute(new[] { "-karawaraSaveRegression", "write" }, personal);
                Require(!(bool)result["success"] && !Directory.Exists(root), "Explicit activation requires root.");
            });
            Test(report, "Unknown and duplicate actions rejected before I/O", () =>
            {
                var unknown = Execute(new[] { "-karawaraSaveRegression", "unknown", "-karawaraSaveRoot", root }, personal);
                var duplicate = Execute(new[] { "-karawaraSaveRegression", "write", "-karawaraSaveRegression", "delete", "-karawaraSaveRoot", root }, personal);
                Require(!(bool)unknown["success"] && !(bool)duplicate["success"] && !Directory.Exists(root), "Malformed action must fail before creating diagnostics.");
            });
            Test(report, "Write/read exact scalar payload across all three slots", () =>
            {
                var written = Action("write", root, personal);
                Require((bool)written["success"], "Fixture write must succeed.");
                var read = Action("read", root, personal);
                Require((bool)read["success"], "Exact read must succeed.");
                for (int slot = 1; slot <= 3; slot++)
                {
                    string file = Path.Combine(root, "characterSlot_0" + slot);
                    Require(File.Exists(file) && File.Exists(file + ".bak"), "Write creates primary and known valid backup.");
                    var data = new SaveFileDataWriter { saveDataDirectoryPath = root, saveFileName = Path.GetFileName(file) }.TryLoad();
                    Require(data.Success && ScalarsEqual(data.Value, Fixture(slot)), "Real writer returned exact fixture for slot " + slot);
                }
                string first = (string)written["diagnosticPath"], second = (string)read["diagnosticPath"];
                Require(first != second && File.Exists(first) && File.Exists(second), "Each action has exclusive diagnostic file.");
                Require((bool)JObject.Parse(File.ReadAllText(first))["success"], "Diagnostic JSON is parseable and truthful.");
            });
            Test(report, "Read rejects wrong scalar rather than successful DTO load", () =>
            {
                Action("write", root, personal);
                var changed = Fixture(2); changed.currentHealth++;
                Require(new SaveFileDataWriter { saveDataDirectoryPath = root, saveFileName = "characterSlot_02" }.TrySave(changed).Success, "Mutation setup must save.");
                Require(!(bool)Action("read", root, personal)["success"], "Successful deserialization alone must not pass fixture comparison.");
            });
            Test(report, "Recovery reports provenance and exact backup scalars", () =>
            {
                Require((bool)Action("write", root, personal)["success"], "Recovery setup writes known backups.");
                for (int slot = 1; slot <= 3; slot++) File.WriteAllText(Path.Combine(root, "characterSlot_0" + slot), "{broken");
                var recovered = Action("recover", root, personal);
                Require((bool)recovered["success"], "Recover must validate backup values/provenance for every slot.");
                for (int slot = 1; slot <= 3; slot++)
                {
                    var loaded = new SaveFileDataWriter { saveDataDirectoryPath = root, saveFileName = "characterSlot_0" + slot }.TryLoad();
                    Require(loaded.Success && loaded.Message == "RecoveredFromBackup" && ScalarsEqual(loaded.Value, Fixture(slot)), "Recovered real writer payload/provenance differs.");
                }
            });
            Test(report, "Failed write reports IoFailure and no false success", () =>
            {
                var failedWrite = Action("failWrite", root, personal);
                Require((bool)failedWrite["success"] && (string)failedWrite["observedError"] == "IoFailure", "Action passes only after observing expected real writer IoFailure.");
            });
            Test(report, "Delete removes all three primary/backup/temp candidates", () =>
            {
                Action("write", root, personal);
                for (int slot = 1; slot <= 3; slot++) File.WriteAllText(Path.Combine(root, "characterSlot_0" + slot + ".tmp"), "ignored");
                Require((bool)Action("delete", root, personal)["success"], "Delete action must succeed.");
                for (int slot = 1; slot <= 3; slot++) foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    Require(!File.Exists(Path.Combine(root, "characterSlot_0" + slot + suffix)), "Deleted slot candidate still exists.");
            });
            try { if (Directory.Exists(root)) Directory.Delete(root, true); report.cleanedUp = !Directory.Exists(root); }
            catch (Exception error) { report.cases.Add(new Case { name = "Cleanup", detail = error.ToString() }); }
            report.completed = true;
            report.passed = report.cleanedUp && report.cases.All(c => c.passed);
            string output = Path.GetFullPath("Docs/Validation/TOBE/Execution/save-process-fixture-result.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (report.passed) Debug.Log("Save process fixture passed: " + report.cases.Count);
            else Debug.LogError("Save process fixture FAILED: " + report.cases.Count(c => !c.passed) + "; " + output);
        }
        static JObject Action(string action, string root, string personal) => Execute(new[] { "-karawaraSaveRegression", action, "-karawaraSaveRoot", root }, personal);
        static JObject Execute(string[] args, string personal)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("RPGKarawara.DevTools.PersistenceQA.SaveProcessRegression")).FirstOrDefault(t => t != null);
            Require(type != null, "QA standalone storage harness implementation missing.");
            var method = type.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public);
            Require(method != null, "Execute(args, personalRoot) contract missing.");
            return JObject.Parse(JsonConvert.SerializeObject(method.Invoke(null, new object[] { args, personal })));
        }
        public static CharacterSaveData Fixture(int slot) => new CharacterSaveData
        {
            characterName = "QA Process Slot " + slot, sceneIndex = slot,
            xPosition = slot * 11.25f, yPosition = slot * -2.5f, zPosition = slot * 3.75f,
            currentHealth = 80 + slot, currentStamina = 40.5f + slot, secondsPlayed = 100.25f + slot,
            vitality = 10 + slot, endurance = 20 + slot
        };
        static bool ScalarsEqual(CharacterSaveData actual, CharacterSaveData expected) => actual != null &&
            actual.characterName == expected.characterName && actual.sceneIndex == expected.sceneIndex &&
            actual.xPosition == expected.xPosition && actual.yPosition == expected.yPosition && actual.zPosition == expected.zPosition &&
            actual.currentHealth == expected.currentHealth && actual.currentStamina == expected.currentStamina &&
            actual.secondsPlayed == expected.secondsPlayed && actual.vitality == expected.vitality && actual.endurance == expected.endurance;
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
