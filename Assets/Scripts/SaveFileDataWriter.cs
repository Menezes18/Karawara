using System;
using System.IO;
using Karawara.Core.Persistence;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RPGKarawara
{
    public class SaveFileDataWriter
    {
        public string saveDataDirectoryPath = "";
        public string saveFileName = "";

        public bool CheckToSeeIfFileExists() => File.Exists(Path.Combine(saveDataDirectoryPath, saveFileName));

        public SaveResult<CharacterSaveData> TryLoad()
        {
            try
            {
                var result = CreateStore().Read(saveFileName);
                if (!result.Success) return SaveResult<CharacterSaveData>.Fail(result.Error, result.Message);
                return SaveResult<CharacterSaveData>.Ok(JsonUtility.FromJson<CharacterSaveData>(result.Value), result.Message);
            }
            catch (ArgumentException ex) { return SaveResult<CharacterSaveData>.Fail(SaveError.InvalidData, ex.Message); }
            catch (IOException ex) { return SaveResult<CharacterSaveData>.Fail(SaveError.IoFailure, ex.Message); }
            catch (UnauthorizedAccessException ex) { return SaveResult<CharacterSaveData>.Fail(SaveError.IoFailure, ex.Message); }
        }

        public SaveResult<bool> TrySave(CharacterSaveData characterData)
        {
            if (characterData == null) return SaveResult<bool>.Fail(SaveError.InvalidData, "Character save data is null.");
            try { return CreateStore().Write(saveFileName, JsonUtility.ToJson(characterData, true)); }
            catch (ArgumentException ex) { return SaveResult<bool>.Fail(SaveError.InvalidData, ex.Message); }
            catch (IOException ex) { return SaveResult<bool>.Fail(SaveError.IoFailure, ex.Message); }
            catch (UnauthorizedAccessException ex) { return SaveResult<bool>.Fail(SaveError.IoFailure, ex.Message); }
        }

        public SaveResult<bool> TryDelete()
        {
            try { return CreateStore().Delete(saveFileName); }
            catch (ArgumentException ex) { return SaveResult<bool>.Fail(SaveError.InvalidData, ex.Message); }
            catch (IOException ex) { return SaveResult<bool>.Fail(SaveError.IoFailure, ex.Message); }
            catch (UnauthorizedAccessException ex) { return SaveResult<bool>.Fail(SaveError.IoFailure, ex.Message); }
        }

        // Compatibility methods preserve their signatures. Failed writes/deletes cannot return as success.
        public void CreateNewCharacterSaveFile(CharacterSaveData characterData)
        {
            var result = TrySave(characterData);
            if (!result.Success) throw new IOException("Character save failed (" + result.Error + "): " + result.Message);
        }

        public void DeleteSaveFile()
        {
            var result = TryDelete();
            if (!result.Success) throw new IOException("Character save deletion failed (" + result.Error + "): " + result.Message);
        }

        public CharacterSaveData LoadSaveFile()
        {
            var result = TryLoad();
            if (!result.Success && result.Error != SaveError.Missing)
                Debug.LogWarning("Character save load failed (" + result.Error + "): " + result.Message);
            return result.Success ? result.Value : null;
        }

        private AtomicFileStore CreateStore() => new AtomicFileStore(saveDataDirectoryPath, IsValidLegacySave, new SystemFileOperations());

        private static bool IsValidLegacySave(string json)
        {
            // Validate actual DTO-root properties before JsonUtility can fill missing fields with defaults.
            if (string.IsNullOrWhiteSpace(json)) return false;
            var fields = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (fields["characterName"]?.Type != JTokenType.String ||
                !ValidInteger(fields["sceneIndex"]) || !ValidInteger(fields["currentHealth"]) ||
                !ValidNumber(fields["xPosition"]) || !ValidNumber(fields["yPosition"]) || !ValidNumber(fields["zPosition"])) return false;
            foreach (string field in new[] { "vitality", "endurance" })
                if (fields[field] != null && !ValidInteger(fields[field])) return false;
            foreach (string field in new[] { "secondsPlayed", "currentStamina" })
                if (fields[field] != null && !ValidNumber(fields[field])) return false;
            var data = JsonUtility.FromJson<CharacterSaveData>(json);
            return data != null && !string.IsNullOrWhiteSpace(data.characterName) && data.sceneIndex >= 0 &&
                data.currentHealth >= 0 && data.vitality >= 0 && data.endurance >= 0 && data.secondsPlayed >= 0 &&
                IsFinite(data.secondsPlayed) && IsFinite(data.xPosition) && IsFinite(data.yPosition) &&
                IsFinite(data.zPosition) && IsFinite(data.currentStamina);
        }

        private static bool ValidInteger(JToken token)
        {
            if (token == null || token.Type != JTokenType.Integer) return false;
            long value = token.Value<long>();
            return value >= 0 && value <= int.MaxValue;
        }

        private static bool ValidNumber(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return false;
            double value = token.Value<double>();
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= -float.MaxValue && value <= float.MaxValue;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
