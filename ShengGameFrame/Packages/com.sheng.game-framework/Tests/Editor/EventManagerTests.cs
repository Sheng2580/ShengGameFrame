using System;
using NUnit.Framework;
using Sheng.GameFramework.Events;

namespace Sheng.GameFramework.Tests
{
    public sealed class EventManagerTests
    {
        private enum LegacyEvent
        {
            Started
        }

        private static readonly GameEvent EmptyEvent =
            new GameEvent("Test.Empty");
        private static readonly GameEvent ValueEvent =
            new GameEvent("Test.Value");
        private static readonly GameEvent MultipleEvent =
            new GameEvent("Test.Multiple");

        private EventManager _manager;

        [SetUp]
        public void SetUp()
        {
            _manager = EventManager.Instance;
            _manager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Clear();
        }

        [Test]
        public void GameEvent_UsesStableOrdinalIdentityAndCanWrapEnum()
        {
            GameEvent first = new GameEvent("  Player.Died  ");
            GameEvent second = new GameEvent("Player.Died");
            GameEvent differentCase = new GameEvent("player.died");
            GameEvent fromEnum = GameEvent.From(LegacyEvent.Started);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
            Assert.AreNotEqual(first, differentCase);
            Assert.AreEqual(
                typeof(LegacyEvent).FullName + ".Started",
                fromEnum.Name);
        }

        [Test]
        public void ZeroParameter_AddTriggerAndRemoveManageEventSlot()
        {
            int callCount = 0;
            Action listener = () => callCount++;

            _manager.AddEventListener(EmptyEvent, listener);

            Assert.IsTrue(_manager.EventTrigger(EmptyEvent));
            Assert.AreEqual(1, callCount);
            Assert.AreEqual(1, _manager.EventCount);
            Assert.AreEqual(1, _manager.GetListenerCount(EmptyEvent));
            Assert.IsTrue(_manager.RemoveEventListener(EmptyEvent, listener));
            Assert.AreEqual(0, _manager.EventCount);
            Assert.IsFalse(_manager.EventTrigger(EmptyEvent));
        }

        [Test]
        public void OneParameter_PassesTypedValue()
        {
            int received = 0;
            _manager.AddEventListener<int>(ValueEvent, value => received = value);

            bool triggered = _manager.EventTrigger(ValueEvent, 42);

            Assert.IsTrue(triggered);
            Assert.AreEqual(42, received);
        }

        [Test]
        public void TwoParameters_PassValuesInOrder()
        {
            string received = string.Empty;
            _manager.AddEventListener<int, string>(
                MultipleEvent,
                (id, name) => received = id + ":" + name);

            _manager.EventTrigger(MultipleEvent, 7, "Sword");

            Assert.AreEqual("7:Sword", received);
        }

        [Test]
        public void ThreeParameters_PassValuesInOrder()
        {
            string received = string.Empty;
            _manager.AddEventListener<int, float, bool>(
                MultipleEvent,
                (id, damage, critical) =>
                    received = $"{id}:{damage}:{critical}");

            _manager.EventTrigger(MultipleEvent, 3, 12.5f, true);

            Assert.AreEqual("3:12.5:True", received);
        }

        [Test]
        public void FourParameters_PassValuesInOrder()
        {
            int total = 0;
            _manager.AddEventListener<int, int, int, int>(
                MultipleEvent,
                (a, b, c, d) => total = a + b + c + d);

            _manager.EventTrigger(MultipleEvent, 1, 2, 3, 4);

            Assert.AreEqual(10, total);
        }

        [Test]
        public void FiveParameters_PassValuesInOrder()
        {
            string received = string.Empty;
            _manager.AddEventListener<int, int, int, int, string>(
                MultipleEvent,
                (a, b, c, d, text) =>
                    received = $"{a + b + c + d}:{text}");

            _manager.EventTrigger(MultipleEvent, 1, 2, 3, 4, "Done");

            Assert.AreEqual("10:Done", received);
        }

        [Test]
        public void SignatureMismatch_ThrowsWithoutReplacingRegisteredListeners()
        {
            int received = 0;
            _manager.AddEventListener<int>(ValueEvent, value => received = value);

            Assert.Throws<InvalidOperationException>(() =>
                _manager.AddEventListener<string>(ValueEvent, _ => { }));
            Assert.Throws<InvalidOperationException>(() =>
                _manager.EventTrigger(ValueEvent, "Wrong"));

            Assert.IsTrue(_manager.EventTrigger(ValueEvent, 9));
            Assert.AreEqual(9, received);
            Assert.AreEqual(1, _manager.GetListenerCount(ValueEvent));
        }

        [Test]
        public void ClearEventAndClear_RemoveSelectedOrAllEvents()
        {
            _manager.AddEventListener(EmptyEvent, () => { });
            _manager.AddEventListener<int>(ValueEvent, _ => { });

            Assert.IsTrue(_manager.ClearEvent(EmptyEvent));
            Assert.IsFalse(_manager.HasListeners(EmptyEvent));
            Assert.IsTrue(_manager.HasListeners(ValueEvent));

            _manager.Clear();

            Assert.AreEqual(0, _manager.EventCount);
            Assert.IsFalse(_manager.HasListeners(ValueEvent));
        }

        [Test]
        public void Listener_CanRemoveItselfDuringDispatch()
        {
            int callCount = 0;
            Action listener = null;
            listener = () =>
            {
                callCount++;
                _manager.RemoveEventListener(EmptyEvent, listener);
            };
            _manager.AddEventListener(EmptyEvent, listener);

            Assert.IsTrue(_manager.EventTrigger(EmptyEvent));
            Assert.IsFalse(_manager.EventTrigger(EmptyEvent));
            Assert.AreEqual(1, callCount);
        }
    }
}
