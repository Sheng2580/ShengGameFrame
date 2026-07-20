using System.Collections.Generic;
using NUnit.Framework;
using Sheng.GameFramework.StateMachine;

namespace Sheng.GameFramework.Tests
{
    public sealed class StateMachineTests
    {
        private enum TestStateId
        {
            Idle,
            Move
        }

        private sealed class TestOwner
        {
            public readonly List<string> Log = new List<string>();
            public bool RequestMove;
        }

        private sealed class TestState : State<TestOwner, TestStateId>
        {
            private readonly TestStateId _id;

            public TestState(TestStateId id)
            {
                _id = id;
            }

            public override TestStateId Id => _id;
            public bool AllowExit { get; set; } = true;

            public override bool CanExitTo(TestStateId nextStateId)
            {
                return AllowExit;
            }

            protected override void OnInitialize()
            {
                Owner.Log.Add($"{Id}.Initialize");
            }

            protected override void OnEnter()
            {
                Owner.Log.Add($"{Id}.Enter");
            }

            protected override void OnTick(float deltaTime)
            {
                Owner.Log.Add($"{Id}.Tick");
                if (Id == TestStateId.Idle && Owner.RequestMove)
                {
                    Machine.RequestState(TestStateId.Move);
                }
            }

            protected override void OnExit()
            {
                Owner.Log.Add($"{Id}.Exit");
            }

            protected override void OnDispose()
            {
                Owner.Log.Add($"{Id}.Dispose");
            }
        }

        [Test]
        public void ChangeState_CallsExitBeforeNextEnter()
        {
            TestOwner owner = new TestOwner();
            StateMachine<TestOwner, TestStateId> machine =
                new StateMachine<TestOwner, TestStateId>(owner);
            machine.Register(new TestState(TestStateId.Idle));
            machine.Register(new TestState(TestStateId.Move));

            Assert.IsTrue(machine.Start(TestStateId.Idle));
            Assert.IsTrue(machine.ChangeState(TestStateId.Move));

            CollectionAssert.AreEqual(
                new[]
                {
                    "Idle.Initialize",
                    "Move.Initialize",
                    "Idle.Enter",
                    "Idle.Exit",
                    "Move.Enter"
                },
                owner.Log);
            Assert.AreEqual(TestStateId.Move, machine.CurrentStateId);
        }

        [Test]
        public void ChangeState_ForceBypassesExitGuard()
        {
            TestOwner owner = new TestOwner();
            StateMachine<TestOwner, TestStateId> machine =
                new StateMachine<TestOwner, TestStateId>(owner);
            TestState idle = new TestState(TestStateId.Idle) { AllowExit = false };
            machine.Register(idle);
            machine.Register(new TestState(TestStateId.Move));
            machine.Start(TestStateId.Idle);

            Assert.IsFalse(machine.ChangeState(TestStateId.Move));
            Assert.AreEqual(TestStateId.Idle, machine.CurrentStateId);
            Assert.IsTrue(machine.ChangeState(TestStateId.Move, true));
            Assert.AreEqual(TestStateId.Move, machine.CurrentStateId);
        }

        [Test]
        public void Tick_AppliesStateRequestedByCurrentState()
        {
            TestOwner owner = new TestOwner { RequestMove = true };
            StateMachine<TestOwner, TestStateId> machine =
                new StateMachine<TestOwner, TestStateId>(owner);
            machine.Register(new TestState(TestStateId.Idle));
            machine.Register(new TestState(TestStateId.Move));
            machine.Start(TestStateId.Idle);

            machine.Tick(0.02f);

            Assert.AreEqual(TestStateId.Move, machine.CurrentStateId);
            CollectionAssert.Contains(owner.Log, "Idle.Tick");
            CollectionAssert.Contains(owner.Log, "Idle.Exit");
            CollectionAssert.Contains(owner.Log, "Move.Enter");
        }

        [Test]
        public void Clear_DisposesEveryRegisteredState()
        {
            TestOwner owner = new TestOwner();
            StateMachine<TestOwner, TestStateId> machine =
                new StateMachine<TestOwner, TestStateId>(owner);
            machine.Register(new TestState(TestStateId.Idle));
            machine.Register(new TestState(TestStateId.Move));
            machine.Start(TestStateId.Idle);

            machine.Clear();

            Assert.AreEqual(0, machine.StateCount);
            CollectionAssert.Contains(owner.Log, "Idle.Dispose");
            CollectionAssert.Contains(owner.Log, "Move.Dispose");
        }
    }
}
