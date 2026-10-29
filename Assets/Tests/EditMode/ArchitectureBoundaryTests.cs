using System;
using System.Linq;
using Karawara.Core.Persistence;
using NUnit.Framework;
namespace Karawara.Tests.Architecture
{
    public class ArchitectureBoundaryTests
    {
        [Test] public void CoreNeverReferencesGameplayOrEngine()
        {
            var references = typeof(AtomicFileStore).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Any(n => n.StartsWith("UnityEngine") || n.StartsWith("Karawara.Gameplay") || n == "Assembly-CSharp"), Is.False);
        }
        [Test] public void GameplayHasNoPredefinedAssemblyDependency()
        {
            var gameplay = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "Karawara.Gameplay");
            Assert.That(gameplay, Is.Not.Null, "Gameplay still lives in the predefined assembly");
            Assert.That(gameplay.GetReferencedAssemblies().Any(a => a.Name == "Assembly-CSharp" || a.Name == "Assembly-CSharp-Editor"), Is.False);
        }
        [Test] public void GeneratedInputHasDedicatedAssembly()
        {
            var generated = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "Karawara.Input.Generated");
            Assert.That(generated, Is.Not.Null, "Generated input has no isolated assembly");
            Assert.That(generated.GetReferencedAssemblies().Any(a => a.Name == "Assembly-CSharp" || a.Name == "Karawara.Gameplay"), Is.False);
        }
    }
}
