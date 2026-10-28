using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Karawara.Tests.Characters
{
    public class LocalRuntimeAuditTests
    {
        [TestCase("// Unity.Netcode\n/* NetworkBehaviour */ class Local {}", 0)]
        [TestCase("using Unity.Netcode; class Test : NetworkBehaviour {}", 2)]
        [TestCase("// old\n// old\nIsOwner", 1)]
        [TestCase("var type = \"Unity.Netcode.NetworkManager\";", 2)]
        [TestCase("var url = \"http://local\"; NetworkObject.Spawn();", 1)]
        public void SourceAuditRetainsCodeAndReflectionStringsButIgnoresComments(string source, int count)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Karawara.DevTools.Editor.LocalRuntimeAudit"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Missing repeatable local runtime audit");
            var method = type.GetMethod("FindNetworkTokens", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var result = (string[])method.Invoke(null, new object[] { source });
            Assert.That(result.Length, Is.EqualTo(count));
            if (source.EndsWith("IsOwner")) Assert.That(result[0], Is.EqualTo("3:IsOwner"));
        }
    }
}
