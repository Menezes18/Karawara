using System.IO;
using System.Text;

namespace Karawara.Core.Persistence
{
    public interface IFileOperations
    {
        bool Exists(string path);
        string Read(string path);
        void WriteAndFlush(string path, string content);
        void Replace(string temp, string primary, string backup);
        void Move(string source, string destination);
        void Delete(string path);
        void CreateDirectory(string path);
    }

    public sealed class SystemFileOperations : IFileOperations
    {
        public bool Exists(string path) => File.Exists(path);
        public string Read(string path) => File.ReadAllText(path);
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void Delete(string path) => File.Delete(path);
        public void Move(string source, string destination) => File.Move(source, destination);
        public void Replace(string temp, string primary, string backup) => File.Replace(temp, primary, backup);

        public void WriteAndFlush(string path, string content)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }
}
