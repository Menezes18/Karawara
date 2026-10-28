namespace Karawara.Core.Persistence
{
    public interface IAtomicFileStore
    {
        SaveResult<string> Read(string fileName);
        SaveResult<bool> Write(string fileName, string json);
        SaveResult<bool> Delete(string fileName);
    }
}
