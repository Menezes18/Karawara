using System;
using System.IO;

namespace Karawara.Core.Persistence
{
    public static class SaveRootPolicy
    {
        public static SaveResult<string> Resolve(string persistentRoot, string[] args, bool qaEnabled)
        {
            if (!qaEnabled || args == null) return SaveResult<string>.Ok(persistentRoot);

            const string argument = "-karawaraSaveRoot";
            int rootIndex = -1;
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], argument, StringComparison.Ordinal)) continue;
                if (rootIndex >= 0) return Invalid("Specify the QA save root only once.");
                rootIndex = i;
            }
            if (rootIndex < 0) return SaveResult<string>.Ok(persistentRoot);
            if (rootIndex + 1 >= args.Length) return Invalid("The QA save root argument requires a value.");

            try
            {
                string root = Canonicalize(args[rootIndex + 1]);
                string personalRoot = Canonicalize(persistentRoot);
                if (string.Equals(root, personalRoot, StringComparison.OrdinalIgnoreCase))
                    return Invalid("The QA save root must be distinct from the personal persistent-data directory.");
                return SaveResult<string>.Ok(root);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return Invalid("The QA save root must be a valid absolute path: " + ex.Message);
            }
        }

        private static string Canonicalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
                throw new ArgumentException("A fully qualified path is required.");
            if (path.IndexOfAny(new[] { '<', '>', '"', '|', '?', '*', '\0' }) >= 0)
                throw new ArgumentException("The path contains an invalid character.");
            string fullPath = Path.GetFullPath(path);
            int rootLength = Path.GetPathRoot(fullPath).Length;
            int length = fullPath.Length;
            while (length > rootLength && (fullPath[length - 1] == Path.DirectorySeparatorChar ||
                   fullPath[length - 1] == Path.AltDirectorySeparatorChar)) length--;
            return fullPath.Substring(0, length);
        }

        private static SaveResult<string> Invalid(string message) => SaveResult<string>.Fail(SaveError.InvalidData, message);
    }
}
