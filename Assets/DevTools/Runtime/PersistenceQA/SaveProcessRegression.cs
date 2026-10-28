using System;
using System.Collections.Generic;
using System.IO;
using Karawara.Core.Persistence;
using UnityEngine;

namespace RPGKarawara.DevTools.PersistenceQA
{
    [Serializable]
    public sealed class SaveProcessReport
    {
        public string utc, action, saveRoot, diagnosticPath, observedError;
        public bool handled, success;
        public List<SaveProcessSlotResult> slots = new List<SaveProcessSlotResult>();
        public List<string> errors = new List<string>();
    }
    [Serializable]
    public sealed class SaveProcessSlotResult
    {
        public string slot, error, provenance;
        public bool success, exactScalars;
    }

    // Explicit disposable QA process transport check. Never creates a gameplay host.
    public static class SaveProcessRegression
    {
        public static SaveProcessReport Execute(string[] args, string personalRoot)
        {
            var report = new SaveProcessReport { utc = DateTime.UtcNow.ToString("O"), success = true };
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
            const string flag = "-karawaraSaveRegression";
            int activation = -1;
            if (args != null)
                for (int index = 0; index < args.Length; index++)
                    if (args[index] == flag)
                    {
                        report.handled = true;
                        if (activation >= 0) return Invalid(report, "Specify the save regression action only once.");
                        activation = index;
                    }
            if (!report.handled) return report;
            if (activation + 1 >= args.Length) return Invalid(report, "Save regression action is required.");
            report.action = args[activation + 1];
            if (report.action != "write" && report.action != "read" && report.action != "recover" &&
                report.action != "delete" && report.action != "failWrite")
                return Invalid(report, "Unknown save regression action.");
            if (Array.IndexOf(args, "-karawaraSaveRoot") < 0)
                return Invalid(report, "An explicit isolated -karawaraSaveRoot is required.");
            var resolved = SaveRootPolicy.Resolve(personalRoot, args, true);
            if (!resolved.Success) return Invalid(report, resolved.Message);
            report.saveRoot = resolved.Value;

            try
            {
                if (report.action == "failWrite") CheckFailedWrite(report);
                else
                    for (int slot = 1; slot <= 3; slot++) CheckSlot(report, slot);
            }
            catch (Exception error)
            {
                report.success = false;
                report.errors.Add(error.GetType().Name + ": " + error.Message);
            }
            try
            {
                Directory.CreateDirectory(report.saveRoot);
                report.diagnosticPath = Path.Combine(report.saveRoot, "qa-save-regression-" + Guid.NewGuid().ToString("N") + ".json");
                using (var stream = new FileStream(report.diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
            }
            catch (Exception error)
            {
                report.success = false;
                report.errors.Add("Diagnostic output failure: " + error.GetType().Name + ": " + error.Message);
            }
#endif
            return report;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || KARAWARA_QA
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RunFromCommandLine()
        {
            if (Application.isEditor) return;
            var report = Execute(Environment.GetCommandLineArgs(), Application.persistentDataPath);
            if (!report.handled) return;
            Debug.Log("QA save regression result: " + JsonUtility.ToJson(report));
            Application.Quit(report.success ? 0 : 1);
        }

        static SaveProcessReport Invalid(SaveProcessReport report, string message)
        {
            report.success = false;
            report.errors.Add(message);
            return report;
        }

        static void CheckSlot(SaveProcessReport report, int slot)
        {
            string filename = "characterSlot_0" + slot;
            var writer = new SaveFileDataWriter { saveDataDirectoryPath = report.saveRoot, saveFileName = filename };
            var detail = new SaveProcessSlotResult { slot = filename };
            report.slots.Add(detail);
            if (report.action == "write")
            {
                var first = writer.TrySave(Fixture(slot));
                var second = first.Success ? writer.TrySave(Fixture(slot)) : first;
                detail.success = first.Success && second.Success;
                detail.error = detail.success ? null : second.Error.ToString() + ": " + second.Message;
                if (detail.success)
                {
                    var loaded = writer.TryLoad();
                    detail.exactScalars = loaded.Success && ScalarsEqual(loaded.Value, Fixture(slot));
                    detail.success &= detail.exactScalars;
                }
            }
            else if (report.action == "delete")
            {
                var deleted = writer.TryDelete();
                string primary = Path.Combine(report.saveRoot, filename);
                detail.success = deleted.Success && !File.Exists(primary) && !File.Exists(primary + ".bak") && !File.Exists(primary + ".tmp");
                detail.error = detail.success ? null : deleted.Error.ToString() + ": " + deleted.Message;
            }
            else
            {
                var loaded = writer.TryLoad();
                detail.exactScalars = loaded.Success && ScalarsEqual(loaded.Value, Fixture(slot));
                detail.provenance = loaded.Message;
                detail.success = detail.exactScalars && (report.action != "recover" || loaded.Message == "RecoveredFromBackup");
                detail.error = detail.success ? null : loaded.Error.ToString() + ": " + loaded.Message + "; exact scalar/provenance comparison failed.";
            }
            if (!detail.success)
            {
                report.success = false;
                report.errors.Add(filename + ": " + (detail.error ?? "Exact scalar comparison failed."));
            }
        }

        static void CheckFailedWrite(SaveProcessReport report)
        {
            string name = "qa-occupied-primary-" + Guid.NewGuid().ToString("N");
            string occupied = Path.Combine(report.saveRoot, name);
            Directory.CreateDirectory(occupied);
            try
            {
                var writer = new SaveFileDataWriter { saveDataDirectoryPath = report.saveRoot, saveFileName = name };
                var saved = writer.TrySave(Fixture(1));
                report.observedError = saved.Error.ToString();
                if (saved.Success || saved.Error != SaveError.IoFailure)
                    throw new InvalidOperationException("Occupied primary must fail with IoFailure; got " + saved.Error + "/" + saved.Success);
            }
            finally
            {
                // Only this uniquely named, empty QA-owned directory is removed.
                Directory.Delete(occupied, false);
            }
        }

        static CharacterSaveData Fixture(int slot) => new CharacterSaveData
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
#endif
    }
}

