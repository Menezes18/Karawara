using System;
using System.Collections.Generic;
using System.IO;

namespace Karawara.Core.Persistence
{
    public sealed class AtomicFileStore : IAtomicFileStore
    {
        // Shared by store instances, but entries exist only while an operation is in flight.
        private static readonly HashSet<string> ActiveSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string root;
        private readonly Func<string, bool> validator;
        private readonly IFileOperations files;

        public AtomicFileStore(string root, Func<string, bool> validator, IFileOperations files)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                throw new ArgumentException("Save root must be absolute.", nameof(root));
            this.root = Path.GetFullPath(root);
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.files = files ?? new SystemFileOperations();
        }

        public SaveResult<string> Read(string fileName) => Run(fileName, primary =>
        {
            var main = ReadCandidate(primary);
            if (main.Success) return main;
            var backup = ReadCandidate(primary + ".bak");
            if (backup.Success) return SaveResult<string>.Ok(backup.Value, "RecoveredFromBackup");
            return main.Error == SaveError.Missing ? backup : main;
        });

        public SaveResult<bool> Write(string fileName, string json) => Run(fileName, primary =>
        {
            if (!IsValid(json)) return SaveResult<bool>.Fail(SaveError.InvalidData, "Save payload failed validation.");
            string temp = primary + ".tmp";
            try
            {
                files.CreateDirectory(root);
                // Do not destroy a valid backup by replacing it with a corrupt primary.
                bool primaryExists = files.Exists(primary);
                bool primaryValid = primaryExists && IsValid(files.Read(primary));
                files.WriteAndFlush(temp, json);
                string flushed = files.Read(temp);
                if (!IsValid(flushed) || !string.Equals(flushed, json, StringComparison.Ordinal))
                    return SaveResult<bool>.Fail(SaveError.InvalidData, "Flushed temporary save failed validation.");
                if (primaryExists) files.Replace(temp, primary, primaryValid ? primary + ".bak" : null);
                else files.Move(temp, primary);
                return SaveResult<bool>.Ok(true);
            }
            finally
            {
                // A failed cleanup leaves only an ignored temp; it cannot change the operation's outcome.
                try { if (files.Exists(temp)) files.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        });

        public SaveResult<bool> Delete(string fileName) => Run(fileName, primary =>
        {
            // Remove recovery candidates first so a failed deletion never resurrects a deleted primary.
            files.Delete(primary + ".bak");
            files.Delete(primary + ".tmp");
            files.Delete(primary);
            return SaveResult<bool>.Ok(true);
        });

        private SaveResult<string> ReadCandidate(string path)
        {
            try
            {
                if (!files.Exists(path)) return SaveResult<string>.Fail(SaveError.Missing, "Save file is missing.");
                string json = files.Read(path);
                return IsValid(json) ? SaveResult<string>.Ok(json) : SaveResult<string>.Fail(SaveError.InvalidData, "Save file failed validation.");
            }
            catch (IOException ex) { return SaveResult<string>.Fail(SaveError.IoFailure, ex.Message); }
            catch (UnauthorizedAccessException ex) { return SaveResult<string>.Fail(SaveError.IoFailure, ex.Message); }
        }

        private bool IsValid(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try { return validator(json); }
            catch (Exception) { return false; }
        }

        private SaveResult<T> Run<T>(string fileName, Func<string, SaveResult<T>> operation)
        {
            // Flat filenames avoid traversal, Windows alternate streams, and aliases of recovery files.
            if (string.IsNullOrWhiteSpace(fileName) || fileName == "." || fileName == ".." ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 ||
                Path.IsPathRooted(fileName) || fileName.EndsWith(".", StringComparison.Ordinal) ||
                fileName.EndsWith(" ", StringComparison.Ordinal) ||
                fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return SaveResult<T>.Fail(SaveError.InvalidData, "Save filename must be a relative primary filename.");

            string primary = Path.Combine(root, fileName);
            lock (ActiveSlots)
            {
                if (!ActiveSlots.Add(primary)) return SaveResult<T>.Fail(SaveError.Busy, "Another operation is using this save slot.");
            }
            try { return operation(primary); }
            catch (IOException ex) { return SaveResult<T>.Fail(SaveError.IoFailure, ex.Message); }
            catch (UnauthorizedAccessException ex) { return SaveResult<T>.Fail(SaveError.IoFailure, ex.Message); }
            finally { lock (ActiveSlots) ActiveSlots.Remove(primary); }
        }
    }
}
