using System;
using Karawara.Core.Characters;
using NUnit.Framework;

namespace Karawara.Tests.Characters
{
    public class CharacterRegistryTests
    {
        [Test]
        public void UnlockCallbackReplacementIsLastTargetNotification()
        {
            var observer = new CharacterRuntimeState();
            var oldTarget = new CharacterRuntimeState();
            var replacement = new CharacterRuntimeState();
            observer.SetTarget(oldTarget);
            observer.SetLockedOn(true);
            CharacterRuntimeState lastNotified = oldTarget;
            observer.TargetChanged += (previous, current) => lastNotified = current;
            observer.LockedOnChanged += (previous, current) =>
            {
                if (!current) observer.SetTarget(replacement);
            };
            oldTarget.SetActive(false);
            Assert.That(observer.Target, Is.SameAs(replacement));
            Assert.That(lastNotified, Is.SameAs(observer.Target));
        }

        [Test]
        public void TargetUnavailableCallbackCanSelectAnotherTarget()
        {
            var registry = new CharacterRegistry();
            var observer = new CharacterRuntimeState();
            var oldTarget = new CharacterRuntimeState();
            var replacement = new CharacterRuntimeState();
            registry.Register(oldTarget);
            observer.SetTarget(oldTarget);
            int replacements = 0;
            observer.TargetChanged += (previous, current) =>
            {
                if (current == null) { replacements++; observer.SetTarget(replacement); }
            };
            oldTarget.SetActive(false);
            Assert.That(observer.Target, Is.SameAs(replacement));
            Assert.That(replacements, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterClearsLockOnAsWellAsTarget()
        {
            var registry = new CharacterRegistry();
            var observer = new CharacterRuntimeState();
            var target = new CharacterRuntimeState();
            var id = registry.Register(target);
            observer.SetTarget(target);
            observer.SetLockedOn(true);
            registry.Unregister(id);
            Assert.That(observer.Target, Is.Null);
            Assert.That(observer.IsLockedOn, Is.False);
        }

        [Test]
        public void ImplementsRegistryContract()
        {
            Assert.That(CharacterContract.Type("ICharacterRegistry").IsAssignableFrom(
                CharacterContract.Type("CharacterRegistry")), Is.True);
        }

        [Test]
        public void RegisterIsIdempotentAndIdsAreNeverReused()
        {
            var registry = CharacterContract.Registry();
            var first = CharacterContract.State();
            int id = (int)CharacterContract.Call(registry, "Register", first);
            Assert.That(id, Is.GreaterThan(0));
            Assert.That(CharacterContract.Call(registry, "Register", first), Is.EqualTo(id));
            CharacterContract.Call(registry, "Unregister", id);
            int next = (int)CharacterContract.Call(registry, "Register", first);
            Assert.That(next, Is.GreaterThan(id));
            Assert.That(CharacterContract.TryGet(registry, id, out _), Is.False);
            Assert.That(CharacterContract.TryGet(registry, next, out var found), Is.True);
            Assert.That(found, Is.SameAs(first));
        }

        [Test]
        public void TargetClearsOnUnregister()
        {
            var registry = CharacterContract.Registry();
            var attacker = CharacterContract.State();
            var target = CharacterContract.State();
            CharacterContract.Call(registry, "Register", attacker);
            int id = (int)CharacterContract.Call(registry, "Register", target);
            CharacterContract.Call(attacker, "SetTarget", target);
            CharacterContract.Call(registry, "Unregister", id);
            CharacterContract.Call(registry, "Unregister", id);
            Assert.That(CharacterContract.Get<object>(attacker, "Target"), Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RegistryDoesNotReturnInactiveOrDeadCharacter(bool death)
        {
            var registry = CharacterContract.Registry();
            var state = CharacterContract.State();
            int id = (int)CharacterContract.Call(registry, "Register", state);
            if (death) CharacterContract.Call(state, "SetHealth", 0);
            else CharacterContract.Call(state, "SetActive", false);
            Assert.That(CharacterContract.TryGet(registry, id, out var found), Is.False);
            Assert.That(found, Is.Null);
            if (death) CharacterContract.Call(state, "SetHealth", 10);
            else CharacterContract.Call(state, "SetActive", true);
            Assert.That(CharacterContract.TryGet(registry, id, out _), Is.False,
                "Re-enable must explicitly register a fresh session identity.");
        }

        [Test]
        public void ClearInvalidatesTargetsAndAllLookups()
        {
            var registry = CharacterContract.Registry();
            var first = CharacterContract.State();
            var second = CharacterContract.State();
            int firstId = (int)CharacterContract.Call(registry, "Register", first);
            int secondId = (int)CharacterContract.Call(registry, "Register", second);
            CharacterContract.Call(first, "SetTarget", second);
            CharacterContract.Call(registry, "Clear");
            Assert.That(CharacterContract.TryGet(registry, firstId, out _), Is.False);
            Assert.That(CharacterContract.TryGet(registry, secondId, out _), Is.False);
            Assert.That(CharacterContract.Get<object>(first, "Target"), Is.Null);
        }

        [Test]
        public void RejectsNullInactiveAndCrossSessionRegistration()
        {
            var registry = CharacterContract.Registry();
            Assert.Throws<ArgumentNullException>(() => CharacterContract.Call(registry, "Register", new object[] { null }));
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetActive", false);
            Assert.Throws<ArgumentException>(() => CharacterContract.Call(registry, "Register", state));
            CharacterContract.Call(state, "SetActive", true);
            int id = (int)CharacterContract.Call(registry, "Register", state);
            var other = CharacterContract.Registry();
            Assert.Throws<InvalidOperationException>(() => CharacterContract.Call(other, "Register", state));
            CharacterContract.Call(registry, "Unregister", id);
            Assert.DoesNotThrow(() => CharacterContract.Call(other, "Register", state));
        }
    }
}
