using NUnit.Framework;
using Sheng.GameFramework.BehaviorTree;

namespace Sheng.GameFramework.Tests
{
    public sealed class BehaviorTreeTests
    {
        private sealed class TestContext
        {
            public bool CanAct;
            public bool HighPriority;
            public int ActionCount;
            public int AbortCount;
        }

        private sealed class RunningNode : BehaviorNode<TestContext>
        {
            protected override NodeStatus OnTick(TestContext context, float deltaTime)
            {
                return NodeStatus.Running;
            }

            protected override void OnExit(TestContext context, NodeStatus status)
            {
                if (status == NodeStatus.Aborted)
                {
                    context.AbortCount++;
                }
            }
        }

        [Test]
        public void Sequence_StopsOnFailureAndRunsActionAfterConditionPasses()
        {
            TestContext context = new TestContext();
            BehaviorTree<TestContext> tree = BehaviorTreeBuilder<TestContext>
                .Create()
                .Sequence()
                .Condition(value => value.CanAct)
                .Action(value =>
                {
                    value.ActionCount++;
                    return NodeStatus.Success;
                })
                .End()
                .Build(context);

            Assert.AreEqual(NodeStatus.Failure, tree.Tick(0.02f));
            Assert.AreEqual(0, context.ActionCount);

            context.CanAct = true;
            tree.Reset();
            Assert.AreEqual(NodeStatus.Success, tree.Tick(0.02f));
            Assert.AreEqual(1, context.ActionCount);
        }

        [Test]
        public void PrioritySelector_AbortsRunningLowerPriorityNode()
        {
            TestContext context = new TestContext();
            PrioritySelectorNode<TestContext> root = new PrioritySelectorNode<TestContext>();
            SequenceNode<TestContext> highPriorityBranch = new SequenceNode<TestContext>();
            highPriorityBranch.AddChild(new ConditionNode<TestContext>(value => value.HighPriority));
            highPriorityBranch.AddChild(new ActionNode<TestContext>(value => NodeStatus.Success));
            root.AddChild(highPriorityBranch);
            root.AddChild(new RunningNode());
            BehaviorTree<TestContext> tree = new BehaviorTree<TestContext>(root, context);

            Assert.AreEqual(NodeStatus.Running, tree.Tick(0.02f));
            context.HighPriority = true;
            Assert.AreEqual(NodeStatus.Success, tree.Tick(0.02f));
            Assert.AreEqual(1, context.AbortCount);
        }

        [Test]
        public void Repeat_CompletesAfterConfiguredCount()
        {
            TestContext context = new TestContext();
            BehaviorTree<TestContext> tree = BehaviorTreeBuilder<TestContext>
                .Create()
                .Repeat(3)
                .Action(value =>
                {
                    value.ActionCount++;
                    return NodeStatus.Success;
                })
                .End()
                .Build(context);

            Assert.AreEqual(NodeStatus.Running, tree.Tick(0.02f));
            Assert.AreEqual(NodeStatus.Running, tree.Tick(0.02f));
            Assert.AreEqual(NodeStatus.Success, tree.Tick(0.02f));
            Assert.AreEqual(3, context.ActionCount);
        }

        [Test]
        public void Blackboard_RejectsSameNameWithDifferentValueType()
        {
            Blackboard blackboard = new Blackboard();
            BlackboardKey<int> scoreKey = new BlackboardKey<int>("Score");
            BlackboardKey<string> wrongTypeKey = new BlackboardKey<string>("Score");

            blackboard.Set(scoreKey, 10);

            Assert.AreEqual(10, blackboard.Get(scoreKey));
            Assert.Throws<System.InvalidOperationException>(() =>
                blackboard.Set(wrongTypeKey, "10"));
        }
    }
}
