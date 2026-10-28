using System;
using System.Collections.Generic;
using System.IO;
using Karawara.Core.Persistence;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace RPGKarawara.DevTools.Editor
{
    public static class LegacySaveWriterRegression
    {
        [Serializable] public sealed class CaseResult
        {
            public string name, detail;
            public bool passed;
        }
        [Serializable] public sealed class Report
        {
            public string utc, temporaryRoot;
            public bool completed, passed, cleanedUp;
            public int total, failures;
            public List<CaseResult> cases = new List<CaseResult>();
        }
        private const string Slot = "fixture.json";
        private const string Nested = "{\"characterName\":\"Character\",\"ignored\":{\"sceneIndex\":1,\"xPosition\":1,\"yPosition\":2,\"zPosition\":3,\"currentHealth\":100}}";

        [MenuItem("Karawara/Validation/TOBE/Run Legacy Save Writer Regression")]
        public static void Run()
        {
            var root = Path.Combine(Path.GetTempPath(), "Karawara-LegacySaveWriter-" + Guid.NewGuid().ToString("N"));
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), temporaryRoot = root };
            Directory.CreateDirectory(root);
            try
            {
                Case(report, root, "ValidDtoCanSaveAndLoad", (writer, path) =>
                {
                    Require(writer.TrySave(Valid()).Success, "Valid legacy DTO must save.");
                    var loaded = writer.TryLoad();
                    Require(loaded.Success && loaded.Value.characterName == "Legacy Fixture" && loaded.Value.sceneIndex == 1 &&
                        loaded.Value.xPosition == 11 && loaded.Value.yPosition == 22 && loaded.Value.zPosition == 33 &&
                        loaded.Value.currentHealth == 87 && loaded.Value.secondsPlayed == 44, "Valid DTO restoration differs.");
                });
                Rejected(report, root, "MalformedRejected", "{broken");
                Rejected(report, root, "EmptyObjectRejected", "{}");
                Rejected(report, root, "NestedRequiredFieldsRejected", Nested);
                Rejected(report, root, "WrongSceneTypeRejected", Mutated("sceneIndex", "1"));
                Rejected(report, root, "WrongNameTypeRejected", Mutated("characterName", 99));
                Rejected(report, root, "WrongPositionTypeRejected", Mutated("xPosition", "11"));
                Rejected(report, root, "WrongHealthTypeRejected", Mutated("currentHealth", 87.5));
                Case(report, root, "NestedPrimaryRecoversBackup", (writer, path) =>
                {
                    File.WriteAllText(path, Nested);
                    File.WriteAllText(path + ".bak", JsonUtility.ToJson(Valid()));
                    var result = writer.TryLoad();
                    Require(result.Success && result.Value.characterName == "Legacy Fixture" && result.Value.currentHealth == 87 &&
                        result.Value.xPosition == 11, "Load must select complete backup, not default-filled nested primary.");
                    Require(result.Message == "RecoveredFromBackup", "Typed legacy load must propagate backup recovery provenance.");
                });
                Case(report, root, "WriteOverNestedPrimaryPreservesBackup", (writer, path) =>
                {
                    string backup = JsonUtility.ToJson(Valid());
                    File.WriteAllText(path, Nested);
                    File.WriteAllText(path + ".bak", backup);
                    var next = Valid(); next.characterName = "Next Fixture"; next.currentHealth = 65;
                    Require(writer.TrySave(next).Success, "Valid overwrite must succeed.");
                    Require(File.ReadAllText(path + ".bak") == backup, "Invalid primary must not replace the last valid backup.");
                    Require(writer.TryLoad().Value.characterName == "Next Fixture", "New valid primary must load.");
                });
                Case(report, root, "NullSaveRejected", (writer, path) =>
                    Require(writer.TrySave(null).Error == SaveError.InvalidData && !File.Exists(path), "Null save must fail without a file."));
                Case(report, root, "FailedSaveWrapperThrows", (writer, path) =>
                {
                    Directory.CreateDirectory(path);
                    Require(writer.TrySave(Valid()).Error == SaveError.IoFailure, "Occupied primary path must report IO failure.");
                    bool threw = false;
                    try { writer.CreateNewCharacterSaveFile(Valid()); } catch (IOException) { threw = true; }
                    Require(threw, "Void compatibility wrapper must not return success after failed save.");
                });
            }
            finally
            {
                try { Directory.Delete(root, true); report.cleanedUp = !Directory.Exists(root); }
                catch (Exception ex) { report.cases.Add(new CaseResult { name = "Cleanup", detail = ex.ToString(), passed = false }); report.failures++; }
                report.total = report.cases.Count;
                report.completed = true;
                report.passed = report.failures == 0 && report.cleanedUp;
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Validation/TOBE/Execution/legacy-save-writer-fixture-result.json"));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                if (report.passed) Debug.Log("Legacy save writer regression passed: " + report.total + " cases.");
                else Debug.LogError("Legacy save writer regression failed: " + report.failures + " of " + report.total + ". See " + output);
            }
        }

        private static CharacterSaveData Valid() => new CharacterSaveData
        {
            characterName = "Legacy Fixture", sceneIndex = 1, xPosition = 11, yPosition = 22, zPosition = 33,
            currentHealth = 87, currentStamina = 45, secondsPlayed = 44, vitality = 12, endurance = 13
        };
        private static string Mutated(string property, JToken value)
        {
            var json = JObject.Parse(JsonUtility.ToJson(Valid()));
            json[property] = value;
            return json.ToString();
        }
        private static void Rejected(Report report, string root, string name, string json) =>
            Case(report, root, name, (writer, path) =>
            {
                File.WriteAllText(path, json);
                var result = writer.TryLoad();
                Require(!result.Success && result.Error == SaveError.InvalidData,
                    "Structurally invalid JSON must return InvalidData; got " + result.Error + "/" + result.Success);
            });
        private static void Case(Report report, string root, string name, Action<SaveFileDataWriter, string> test)
        {
            var result = new CaseResult { name = name };
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            try
            {
                test(new SaveFileDataWriter { saveDataDirectoryPath = directory, saveFileName = Slot }, Path.Combine(directory, Slot));
                result.passed = true;
                result.detail = "passed";
            }
            catch (Exception ex) { result.detail = ex.ToString(); report.failures++; }
            report.cases.Add(result);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
