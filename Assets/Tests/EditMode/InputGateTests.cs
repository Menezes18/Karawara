using System;
using Karawara.Core.Persistence;
using NUnit.Framework;

namespace Karawara.Tests.Input
{
    public class InputGateTests
    {
        [Test] public void DeathExceptionNeverBypassesOtherOwners()
        {
            var gate = Gate();
            var method = gate.GetType().GetMethod("IsBlockedExcept");
            Assert.That(method, Is.Not.Null, "Revive needs a reason-aware gate query");
            var reason = Enum.Parse(method.GetParameters()[0].ParameterType, "Death");
            bool OtherLocks() => (bool)method.Invoke(gate, new[] { reason });
            using (Acquire(gate, "Death"))
            {
                Assert.That(OtherLocks(), Is.False);
                using (Acquire(gate, "Dialogue")) Assert.That(OtherLocks(), Is.True);
                Assert.That(OtherLocks(), Is.False);
            }
        }
        private object Gate()
        {
            var type = typeof(AtomicFileStore).Assembly.GetType("Karawara.Core.Input.InputGate");
            Assert.That(type, Is.Not.Null, "Missing InputGate");
            return Activator.CreateInstance(type);
        }
        private IDisposable Acquire(object gate, string reason)
        {
            var method = gate.GetType().GetMethod("Acquire");
            return (IDisposable)method.Invoke(gate, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, reason) });
        }
        private bool Blocked(object gate) => (bool)gate.GetType().GetProperty("IsBlocked").GetValue(gate);

        [Test] public void TwoLocksRequireTwoReleases()
        {
            var gate = Gate(); var pause = Acquire(gate, "Pause"); var dialogue = Acquire(gate, "Dialogue");
            pause.Dispose(); Assert.That(Blocked(gate), Is.True);
            dialogue.Dispose(); Assert.That(Blocked(gate), Is.False);
        }
        [Test] public void DisposeTokenTwiceIsSafe()
        {
            var gate = Gate(); var first = Acquire(gate, "Pause"); var second = Acquire(gate, "Pause");
            first.Dispose(); first.Dispose(); Assert.That(Blocked(gate), Is.True);
            second.Dispose(); Assert.That(Blocked(gate), Is.False);
        }
        [TestCase("Pause")][TestCase("Dialogue")][TestCase("Cutscene")][TestCase("Loading")][TestCase("Death")]
        public void EveryReasonBlocks(string reason)
        {
            var gate = Gate(); Assert.That(Blocked(gate), Is.False);
            using (Acquire(gate, reason)) Assert.That(Blocked(gate), Is.True);
            Assert.That(Blocked(gate), Is.False);
        }
        [Test] public void ChangedOnlyOnBlockedBoundary()
        {
            var gate = Gate(); int calls = 0;
            gate.GetType().GetEvent("Changed").AddEventHandler(gate, new Action(() => calls++));
            var first = Acquire(gate, "Pause"); var second = Acquire(gate, "Dialogue");
            Assert.That(calls, Is.EqualTo(1)); first.Dispose(); Assert.That(calls, Is.EqualTo(1));
            second.Dispose(); Assert.That(calls, Is.EqualTo(2));
        }
    }
}
