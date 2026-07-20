using System;
using System.Collections.Generic;

namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 行为树链式构建器
    /// </summary>
    public sealed class BehaviorTreeBuilder<TContext>
    {
        private readonly Stack<IBehaviorParentNode<TContext>> _parentStack =
            new Stack<IBehaviorParentNode<TContext>>();

        private BehaviorNode<TContext> _root;

        public static BehaviorTreeBuilder<TContext> Create()
        {
            return new BehaviorTreeBuilder<TContext>();
        }

        public BehaviorTreeBuilder<TContext> Sequence()
        {
            return AddParent(new SequenceNode<TContext>());
        }

        public BehaviorTreeBuilder<TContext> Selector()
        {
            return AddParent(new SelectorNode<TContext>());
        }

        public BehaviorTreeBuilder<TContext> PrioritySelector()
        {
            return AddParent(new PrioritySelectorNode<TContext>());
        }

        public BehaviorTreeBuilder<TContext> Parallel(
            ParallelPolicy successPolicy,
            ParallelPolicy failurePolicy)
        {
            return AddParent(new ParallelNode<TContext>(successPolicy, failurePolicy));
        }

        public BehaviorTreeBuilder<TContext> Inverter()
        {
            return AddParent(new InverterNode<TContext>());
        }

        public BehaviorTreeBuilder<TContext> Repeat(int repeatCount)
        {
            return AddParent(new RepeatNode<TContext>(repeatCount));
        }

        public BehaviorTreeBuilder<TContext> Condition(Func<TContext, bool> condition)
        {
            return AddLeaf(new ConditionNode<TContext>(condition));
        }

        public BehaviorTreeBuilder<TContext> Action(
            Func<TContext, float, NodeStatus> action)
        {
            return AddLeaf(new ActionNode<TContext>(action));
        }

        public BehaviorTreeBuilder<TContext> Action(Func<TContext, NodeStatus> action)
        {
            return AddLeaf(new ActionNode<TContext>(action));
        }

        public BehaviorTreeBuilder<TContext> Node(BehaviorNode<TContext> node)
        {
            return AddLeaf(node);
        }

        public BehaviorTreeBuilder<TContext> End()
        {
            if (_parentStack.Count == 0)
            {
                throw new InvalidOperationException("没有可以结束的父节点");
            }

            _parentStack.Pop();
            return this;
        }

        public BehaviorTree<TContext> Build(TContext context)
        {
            if (_root == null)
            {
                throw new InvalidOperationException("行为树缺少根节点");
            }

            if (_parentStack.Count > 0)
            {
                throw new InvalidOperationException("行为树存在未调用 End 的父节点");
            }

            return new BehaviorTree<TContext>(_root, context);
        }

        private BehaviorTreeBuilder<TContext> AddParent<TNode>(TNode node)
            where TNode : BehaviorNode<TContext>, IBehaviorParentNode<TContext>
        {
            AddNode(node);
            _parentStack.Push(node);
            return this;
        }

        private BehaviorTreeBuilder<TContext> AddLeaf(BehaviorNode<TContext> node)
        {
            AddNode(node);
            return this;
        }

        private void AddNode(BehaviorNode<TContext> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (_root == null)
            {
                _root = node;
                return;
            }

            if (_parentStack.Count == 0)
            {
                throw new InvalidOperationException("根节点已经存在 当前没有可接收子节点的父节点");
            }

            _parentStack.Peek().AddChild(node);
        }
    }
}
