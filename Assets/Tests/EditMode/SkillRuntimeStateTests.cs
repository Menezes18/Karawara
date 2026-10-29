using System;
using Karawara.Core.Persistence;
using NUnit.Framework;

namespace Karawara.Tests.Progression
{
    public class SkillRuntimeStateTests
    {
        private static object Runtime(string id = "existing-id", float seconds = 10)
        {
            var type = typeof(AtomicFileStore).Assembly.GetType("Karawara.Core.Progression.SkillRuntimeState");
            Assert.That(type, Is.Not.Null, "Missing per-user skill runtime state");
            return Activator.CreateInstance(type, id, seconds);
        }
        private static bool Activate(object state, float time) => (bool)state.GetType().GetMethod("TryActivate").Invoke(state, new object[] { time });
        private static float Progress(object state, float time) => (float)state.GetType().GetMethod("Progress").Invoke(state, new object[] { time });
        private static object Progression(object state) => state.GetType().GetProperty("Progression").GetValue(state);
        private static void Unlock(object state) => Progression(state).GetType().GetMethod("Unlock").Invoke(Progression(state), null);

        [Test] public void SameDefinitionHasIndependentCooldown()
        {
            var a = Runtime(); var b = Runtime(); Unlock(a); Unlock(b);
            Assert.That(Activate(a, 20), Is.True);
            Assert.That(Activate(a, 21), Is.False);
            Assert.That(Activate(b, 21), Is.True);
            Assert.That(Progress(a, 25), Is.EqualTo(0.5f).Within(0.0001));
            Assert.That(Activate(a, 30), Is.True);
        }
        [Test] public void LockedSkillCannotActivate()
        {
            var a = Runtime(); var b = Runtime(); Unlock(a);
            Assert.That(Activate(b, 20), Is.False);
            Assert.That(Activate(a, 20), Is.True);
        }
        [Test] public void CooldownRespectsPausedGameplayTime()
        {
            var state = Runtime(); Unlock(state); Activate(state, 20);
            Assert.That(Progress(state, 23), Is.EqualTo(0.7f).Within(0.0001));
            Assert.That(Progress(state, 23), Is.EqualTo(0.7f).Within(0.0001));
            Assert.That(Activate(state, 23), Is.False);
            Assert.That(Progress(state, 30), Is.Zero);
        }
        [Test] public void EquipBelongsToEachUser()
        {
            var a = Runtime(); var b = Runtime(); Unlock(a); Unlock(b);
            var progress = Progression(a);
            progress.GetType().GetMethod("SetEquipped").Invoke(progress, new object[] { true });
            Assert.That(progress.GetType().GetProperty("IsEquipped").GetValue(progress), Is.True);
            Assert.That(Progression(b).GetType().GetProperty("IsEquipped").GetValue(Progression(b)), Is.False);
        }
        [Test] public void WorldProgressPreservesExistingIdsWithoutSharingUsers()
        {
            var type = typeof(AtomicFileStore).Assembly.GetType("Karawara.Core.Progression.WorldProgressState");
            Assert.That(type, Is.Not.Null, "Missing per-user world progression");
            var a = Activator.CreateInstance(type); var b = Activator.CreateInstance(type);
            var checkpoints = (System.Collections.Generic.IDictionary<int, bool>)type.GetProperty("Checkpoints").GetValue(a);
            checkpoints[42] = true;
            Assert.That(((System.Collections.Generic.IDictionary<int, bool>)type.GetProperty("Checkpoints").GetValue(b)).ContainsKey(42), Is.False);
            Assert.That(checkpoints[42], Is.True);
        }
    }
}
