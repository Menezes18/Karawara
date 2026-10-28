using System;
using System.Reflection;
using Karawara.Core.Persistence;
using NUnit.Framework;

namespace Karawara.Tests.Characters
{
    // Reflection keeps the missing-contract RED runnable without breaking Editor compilation.
    internal static class CharacterContract
    {
        internal static Type Type(string name)
        {
            var type = typeof(AtomicFileStore).Assembly.GetType("Karawara.Core.Characters." + name);
            Assert.That(type, Is.Not.Null, "Missing local character contract: " + name);
            return type;
        }

        internal static object State() => Activator.CreateInstance(Type("CharacterRuntimeState"));
        internal static object Registry() => Activator.CreateInstance(Type("CharacterRegistry"));
        internal static T Get<T>(object value, string name) => (T)value.GetType().GetProperty(name).GetValue(value);
        internal static object Call(object value, string name, params object[] args)
        {
            try { return value.GetType().GetMethod(name).Invoke(value, args); }
            catch (TargetInvocationException e) { throw e.InnerException; }
        }
        internal static void Listen(object value, string name, Delegate callback) =>
            value.GetType().GetEvent(name).AddEventHandler(value, callback);
        internal static bool TryGet(object registry, int id, out object state)
        {
            var args = new object[] { id, null };
            var found = (bool)Call(registry, "TryGet", args);
            state = args[1];
            return found;
        }
    }

    public class CharacterRuntimeStateTests
    {
        [TestCase("Vitality", "SetVitality", "VitalityChanged")]
        [TestCase("IsMoving", "SetMoving", "MovingChanged")]
        [TestCase("IsChargingAttack", "SetChargingAttack", "ChargingAttackChanged")]
        [TestCase("IsLockedOn", "SetLockedOn", "LockedOnChanged")]
        public void ObservableValuesNotifyOnlyChanges(string property, string setter, string eventName)
        {
            var state = CharacterContract.State();
            int calls = 0;
            if (property == "Vitality")
            {
                CharacterContract.Listen(state, eventName, new Action<int, int>((a, b) =>
                { Assert.That(a, Is.EqualTo(1)); Assert.That(b, Is.EqualTo(5)); calls++; }));
                CharacterContract.Call(state, setter, 5);
                CharacterContract.Call(state, setter, 5);
                Assert.That(CharacterContract.Get<int>(state, property), Is.EqualTo(5));
            }
            else
            {
                CharacterContract.Listen(state, eventName, new Action<bool, bool>((a, b) =>
                { Assert.That(a, Is.False); Assert.That(b, Is.True); calls++; }));
                CharacterContract.Call(state, setter, true);
                CharacterContract.Call(state, setter, true);
                Assert.That(CharacterContract.Get<bool>(state, property), Is.True);
            }
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase("IsBlocking", "SetBlocking")]
        [TestCase("IsPowerPet", "SetPowerPet")]
        [TestCase("IsInvulnerable", "SetInvulnerable")]
        [TestCase("IsSprinting", "SetSprinting")]
        [TestCase("IsJumping", "SetJumping")]
        [TestCase("IsAttacking", "SetAttacking")]
        public void GameplayFlagsRemainLocalValues(string property, string setter)
        {
            var state = CharacterContract.State();
            Assert.That(CharacterContract.Get<bool>(state, property), Is.False);
            CharacterContract.Call(state, setter, true);
            Assert.That(CharacterContract.Get<bool>(state, property), Is.True);
            CharacterContract.Call(state, setter, false);
            Assert.That(CharacterContract.Get<bool>(state, property), Is.False);
        }

        [Test]
        public void AttributesDoNotImplicitlyHealAndRejectNegativeLevels()
        {
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetHealth", 10);
            CharacterContract.Call(state, "SetVitality", 8);
            CharacterContract.Call(state, "SetEndurance", 4);
            Assert.That(CharacterContract.Get<int>(state, "Endurance"), Is.EqualTo(4));
            Assert.That(CharacterContract.Get<int>(state, "Health"), Is.EqualTo(10));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterContract.Call(state, "SetVitality", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterContract.Call(state, "SetEndurance", -1));
        }

        [Test]
        public void UnlockClearsTarget()
        {
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetLockedOn", true);
            CharacterContract.Call(state, "SetTarget", CharacterContract.State());
            CharacterContract.Call(state, "SetLockedOn", false);
            Assert.That(CharacterContract.Get<object>(state, "Target"), Is.Null);
        }

        [TestCase(-10, 0)]
        [TestCase(0, 0)]
        [TestCase(37, 37)]
        [TestCase(120, 100)]
        public void HealthClampsToMax(int requested, int expected)
        {
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetMaxHealth", 100);
            CharacterContract.Call(state, "SetHealth", requested);
            Assert.That(CharacterContract.Get<int>(state, "Health"), Is.EqualTo(expected));
        }

        [Test]
        public void HealthChangedFiresOnce()
        {
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetHealth", 100);
            int calls = 0, previous = 0, current = 0;
            CharacterContract.Listen(state, "HealthChanged", new Action<int, int>((oldValue, newValue) =>
            { calls++; previous = oldValue; current = newValue; }));
            CharacterContract.Call(state, "SetHealth", 30);
            CharacterContract.Call(state, "SetHealth", 30);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(previous, Is.EqualTo(100));
            Assert.That(current, Is.EqualTo(30));
        }

        [Test]
        public void DeathIsIdempotentAndRevivalAllowsNextDeath()
        {
            var state = CharacterContract.State();
            int deaths = 0;
            CharacterContract.Listen(state, "Died", new Action(() => deaths++));
            CharacterContract.Call(state, "SetHealth", 0);
            CharacterContract.Call(state, "SetHealth", -1);
            Assert.That(CharacterContract.Get<bool>(state, "IsDead"), Is.True);
            Assert.That(deaths, Is.EqualTo(1));
            CharacterContract.Call(state, "SetHealth", 10);
            Assert.That(CharacterContract.Get<bool>(state, "IsDead"), Is.False);
            CharacterContract.Call(state, "SetHealth", 0);
            Assert.That(deaths, Is.EqualTo(2));
        }

        [Test]
        public void LowerMaxClampsHealthAndNotifiesBothChangesOnce()
        {
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetMaxHealth", 100);
            CharacterContract.Call(state, "SetHealth", 90);
            int healthCalls = 0, maxCalls = 0;
            CharacterContract.Listen(state, "HealthChanged", new Action<int, int>((a, b) => healthCalls++));
            CharacterContract.Listen(state, "MaxHealthChanged", new Action<int, int>((a, b) => maxCalls++));
            CharacterContract.Call(state, "SetMaxHealth", 50);
            CharacterContract.Call(state, "SetMaxHealth", 50);
            Assert.That(CharacterContract.Get<int>(state, "Health"), Is.EqualTo(50));
            Assert.That(healthCalls, Is.EqualTo(1));
            Assert.That(maxCalls, Is.EqualTo(1));
        }

        [Test]
        public void RaisingMaxDoesNotImplicitlyHeal()
        {
            var state = CharacterContract.State();
            CharacterContract.Call(state, "SetHealth", 10);
            CharacterContract.Call(state, "SetMaxHealth", 500);
            Assert.That(CharacterContract.Get<int>(state, "Health"), Is.EqualTo(10));
        }

        [Test]
        public void NegativeMaxIsRejectedWithoutMutation()
        {
            var state = CharacterContract.State();
            var before = CharacterContract.Get<int>(state, "MaxHealth");
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterContract.Call(state, "SetMaxHealth", -1));
            Assert.That(CharacterContract.Get<int>(state, "MaxHealth"), Is.EqualTo(before));
        }

        [Test]
        public void TargetChangesOnceAndClearsWhenTargetBecomesUnavailable()
        {
            var state = CharacterContract.State();
            var target = CharacterContract.State();
            CharacterContract.Call(state, "SetTarget", target);
            Assert.That(CharacterContract.Get<object>(state, "Target"), Is.SameAs(target));
            CharacterContract.Call(target, "SetActive", false);
            Assert.That(CharacterContract.Get<object>(state, "Target"), Is.Null);
        }

        [Test]
        public void ReplacingTargetDetachesPreviousTarget()
        {
            var state = CharacterContract.State();
            var previous = CharacterContract.State();
            var current = CharacterContract.State();
            CharacterContract.Call(state, "SetTarget", previous);
            CharacterContract.Call(state, "SetTarget", current);
            CharacterContract.Call(previous, "SetHealth", 0);
            Assert.That(CharacterContract.Get<object>(state, "Target"), Is.SameAs(current));
            CharacterContract.Call(current, "SetHealth", 0);
            Assert.That(CharacterContract.Get<object>(state, "Target"), Is.Null);
        }

        [Test]
        public void SelfTargetIsRejected()
        {
            var state = CharacterContract.State();
            Assert.Throws<ArgumentException>(() => CharacterContract.Call(state, "SetTarget", state));
        }
    }
}
