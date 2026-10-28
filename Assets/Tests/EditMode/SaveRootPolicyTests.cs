using Karawara.Core.Persistence;
using NUnit.Framework;

namespace Karawara.Tests.Persistence
{
    public class SaveRootPolicyTests
    {
        private const string PersistentRoot = @"C:\Karawara-tests\Personal";

        [Test]
        public void ReleaseIgnoresRootOverride()
        {
            var result = SaveRootPolicy.Resolve(PersistentRoot,
                new[] { "-karawaraSaveRoot", @"C:\Karawara-tests\QA" }, false);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(PersistentRoot));
        }

        [TestCase("QA")]
        [TestCase(@"C:QA")]
        [TestCase(@"\QA")]
        public void QaRejectsRelativeRoot(string root)
        {
            AssertInvalid(new[] { "-karawaraSaveRoot", root });
        }

        [TestCase(PersistentRoot)]
        [TestCase(@"c:/karawara-tests/personal/")]
        [TestCase(@"C:\Karawara-tests\QA\..\Personal")]
        public void QaRejectsRealPersistentRoot(string root)
        {
            AssertInvalid(new[] { "-karawaraSaveRoot", root });
        }

        [Test]
        public void QaAcceptsDistinctAbsoluteRoot()
        {
            var result = SaveRootPolicy.Resolve(PersistentRoot,
                new[] { "Karawara.exe", "-karawaraSaveRoot", @"C:/Karawara-tests/QA/child/../" }, true);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(@"C:\Karawara-tests\QA"));
        }

        [Test]
        public void QaWithoutOverrideUsesDefault()
        {
            var result = SaveRootPolicy.Resolve(PersistentRoot, new string[0], true);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(PersistentRoot));
        }

        [Test]
        public void QaRejectsMissingOverrideValue() => AssertInvalid(new[] { "-karawaraSaveRoot" });

        [TestCase("")]
        [TestCase(" ")]
        [TestCase(@"C:\QA\bad|name")]
        public void QaRejectsMalformedOverrideValue(string root) => AssertInvalid(new[] { "-karawaraSaveRoot", root });

        [Test]
        public void QaRejectsDuplicateOverride() => AssertInvalid(new[]
            { "-karawaraSaveRoot", @"C:\Karawara-tests\QA", "-karawaraSaveRoot", PersistentRoot });

        [Test]
        public void QaAcceptsUncRoot()
        {
            var result = SaveRootPolicy.Resolve(PersistentRoot,
                new[] { "-karawaraSaveRoot", @"\\qa-server\saves\Karawara\" }, true);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(@"\\qa-server\saves\Karawara"));
        }

        [Test]
        public void ReleaseIgnoresMalformedOverride()
        {
            var result = SaveRootPolicy.Resolve(PersistentRoot, new[] { "-karawaraSaveRoot" }, false);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(PersistentRoot));
        }

        private static void AssertInvalid(string[] args)
        {
            var result = SaveRootPolicy.Resolve(PersistentRoot, args, true);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(SaveError.InvalidData));
            Assert.That(result.Value, Is.Null);
        }
    }
}
