using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Karawara.Core.Persistence;
using NUnit.Framework;

namespace Karawara.Tests.Persistence
{
    public class AtomicFileStoreTests
    {
        private string root;
        private const string Slot = "slot01.json";
        private const string Before = "{\"health\":10}";
        private const string After = "{\"health\":20}";
        private TestFiles files;
        private AtomicFileStore store;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "Karawara-storage-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            files = new TestFiles();
            store = new AtomicFileStore(root, json => json == Before || json == After, files);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [Test]
        public void FirstWriteCanBeRead()
        {
            Assert.That(store.Write(Slot, Before).Success, Is.True);
            var result = store.Read(Slot);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(Before));
            Assert.That(File.Exists(Path.Combine(root, Slot + ".tmp")), Is.False);
        }

        [Test]
        public void SecondWritePreservesBackup()
        {
            File.WriteAllText(Path.Combine(root, Slot), Before);
            Assert.That(store.Write(Slot, After).Success, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(root, Slot)), Is.EqualTo(After));
            Assert.That(File.ReadAllText(Path.Combine(root, Slot + ".bak")), Is.EqualTo(Before));
        }

        [Test]
        public void ReplaceFailurePreservesPrimary()
        {
            File.WriteAllText(Path.Combine(root, Slot), Before);
            files.FailReplace = true;
            var result = store.Write(Slot, After);
            Assert.That(files.Flushed, Is.True, "The injected failure must occur after the temporary file is flushed.");
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.IoFailure));
            Assert.That(File.ReadAllText(Path.Combine(root, Slot)), Is.EqualTo(Before));
        }

        [Test]
        public void CorruptedFlushedTempPreservesPrimaryAndBackup()
        {
            File.WriteAllText(Path.Combine(root, Slot), Before);
            File.WriteAllText(Path.Combine(root, Slot + ".bak"), Before);
            files.CorruptTemp = true;
            var result = store.Write(Slot, After);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.InvalidData));
            Assert.That(File.ReadAllText(Path.Combine(root, Slot)), Is.EqualTo(Before));
            Assert.That(File.ReadAllText(Path.Combine(root, Slot + ".bak")), Is.EqualTo(Before));
        }

        [Test]
        public void InvalidPrimaryRecoversBackup()
        {
            File.WriteAllText(Path.Combine(root, Slot), "corrupted");
            File.WriteAllText(Path.Combine(root, Slot + ".bak"), Before);
            var recovered = store.Read(Slot);
            Assert.That(recovered.Success, Is.True);
            Assert.That(recovered.Value, Is.EqualTo(Before));
            Assert.That(recovered.Message, Is.EqualTo("RecoveredFromBackup"));
        }

        [Test]
        public void WritingOverInvalidPrimaryPreservesValidBackup()
        {
            File.WriteAllText(Path.Combine(root, Slot), "corrupted");
            File.WriteAllText(Path.Combine(root, Slot + ".bak"), Before);
            Assert.That(store.Write(Slot, After).Success, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(root, Slot)), Is.EqualTo(After));
            Assert.That(File.ReadAllText(Path.Combine(root, Slot + ".bak")), Is.EqualTo(Before));
        }

        [Test]
        public void InvalidPayloadDoesNotReplace()
        {
            File.WriteAllText(Path.Combine(root, Slot), Before);
            var result = store.Write(Slot, "{}");
            Assert.That(result.Error, Is.EqualTo(SaveError.InvalidData));
            Assert.That(File.ReadAllText(Path.Combine(root, Slot)), Is.EqualTo(Before));
            Assert.That(File.Exists(Path.Combine(root, Slot + ".tmp")), Is.False);
        }

        [Test]
        public void ReadIgnoresTmp()
        {
            File.WriteAllText(Path.Combine(root, Slot + ".tmp"), After);
            var result = store.Read(Slot);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.Missing));
            File.WriteAllText(Path.Combine(root, Slot), Before);
            Assert.That(store.Read(Slot).Value, Is.EqualTo(Before));
        }

        [Test]
        public void ConcurrentSameSlotReturnsBusy()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                files.BeforeFlush = () => { entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); };
                var pending = Task.Run(() => store.Write(Slot, Before));
                try
                {
                    Assert.That(entered.Wait(TimeSpan.FromSeconds(3)), Is.True, "First write must enter the file operation.");
                    var otherStore = new AtomicFileStore(root, json => true, new TestFiles());
                    Assert.That(otherStore.Write(Slot, After).Error, Is.EqualTo(SaveError.Busy));
                    Assert.That(otherStore.Read(Slot).Error, Is.EqualTo(SaveError.Busy));
                    Assert.That(otherStore.Delete(Slot).Error, Is.EqualTo(SaveError.Busy));
                }
                finally { release.Set(); pending.GetAwaiter().GetResult(); }
                Assert.That(pending.Result.Success, Is.True);
                Assert.That(store.Read(Slot).Value, Is.EqualTo(Before));
            }
        }

        [Test]
        public void DeleteDoesNotRecoverBackup()
        {
            File.WriteAllText(Path.Combine(root, Slot), Before);
            File.WriteAllText(Path.Combine(root, Slot + ".bak"), After);
            File.WriteAllText(Path.Combine(root, Slot + ".tmp"), After);
            Assert.That(store.Delete(Slot).Success, Is.True);
            Assert.That(Directory.GetFiles(root), Is.Empty);
            Assert.That(store.Read(Slot).Error, Is.EqualTo(SaveError.Missing));
        }

        [TestCase("../outside.json")]
        [TestCase("slot01.json.tmp")]
        [TestCase("slot01.json.bak")]
        public void UnsafeNameIsRejected(string name)
        {
            Assert.That(store.Write(name, Before).Error, Is.EqualTo(SaveError.InvalidData));
            Assert.That(store.Read(name).Error, Is.EqualTo(SaveError.InvalidData));
            Assert.That(store.Delete(name).Error, Is.EqualTo(SaveError.InvalidData));
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }

        // Real disk IO with only the two required fault/timing seams overridden.
        private sealed class TestFiles : IFileOperations
        {
            public bool FailReplace;
            public bool CorruptTemp;
            public bool Flushed;
            public Action BeforeFlush;
            public bool Exists(string path) => File.Exists(path);
            public string Read(string path) => File.ReadAllText(path);
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public void Delete(string path) => File.Delete(path);
            public void Move(string source, string destination) => File.Move(source, destination);
            public void Replace(string temp, string primary, string backup)
            {
                if (FailReplace) throw new IOException("Injected failure after flush");
                File.Replace(temp, primary, backup);
            }
            public void WriteAndFlush(string path, string content)
            {
                BeforeFlush?.Invoke();
                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                Flushed = true;
                if (CorruptTemp) File.WriteAllText(path, "corrupted");
            }
        }
    }
}
