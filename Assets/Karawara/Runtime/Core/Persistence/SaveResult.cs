namespace Karawara.Core.Persistence
{
    public enum SaveError { None, Missing, InvalidData, IoFailure, Busy, UnsupportedVersion, MigrationRequired }

    public sealed class SaveResult<T>
    {
        public bool Success { get; }
        public T Value { get; }
        public SaveError Error { get; }
        public string Message { get; }

        private SaveResult(bool success, T value, SaveError error, string message)
        {
            Success = success;
            Value = value;
            Error = error;
            Message = message;
        }

        public static SaveResult<T> Ok(T value, string message = "") => new SaveResult<T>(true, value, SaveError.None, message);
        public static SaveResult<T> Fail(SaveError error, string message) => new SaveResult<T>(false, default(T), error, message);
    }
}
