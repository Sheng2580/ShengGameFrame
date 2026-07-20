namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 子节点依次成功后返回成功
    /// </summary>
    public sealed class SequenceNode<TContext> : CompositeNode<TContext>
    {
        private int _currentIndex;

        protected override void OnEnter(TContext context)
        {
            _currentIndex = 0;
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            if (ChildCount == 0)
            {
                return NodeStatus.Success;
            }

            while (_currentIndex < ChildCount)
            {
                NodeStatus childStatus = GetChild(_currentIndex).Tick(context, deltaTime);
                if (childStatus == NodeStatus.Success)
                {
                    _currentIndex++;
                    continue;
                }

                return childStatus;
            }

            return NodeStatus.Success;
        }
    }

    /// <summary>
    /// 按顺序选择第一个没有失败的子节点
    /// </summary>
    public sealed class SelectorNode<TContext> : CompositeNode<TContext>
    {
        private int _currentIndex;

        protected override void OnEnter(TContext context)
        {
            _currentIndex = 0;
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            if (ChildCount == 0)
            {
                return NodeStatus.Failure;
            }

            while (_currentIndex < ChildCount)
            {
                NodeStatus childStatus = GetChild(_currentIndex).Tick(context, deltaTime);
                if (childStatus == NodeStatus.Failure)
                {
                    _currentIndex++;
                    continue;
                }

                return childStatus;
            }

            return NodeStatus.Failure;
        }
    }

    /// <summary>
    /// 每次 Tick 都从最高优先级重新判断
    /// </summary>
    public sealed class PrioritySelectorNode<TContext> : CompositeNode<TContext>
    {
        private int _runningChildIndex = -1;

        protected override void OnEnter(TContext context)
        {
            _runningChildIndex = -1;
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            int previousRunningIndex = _runningChildIndex;
            for (int i = 0; i < ChildCount; i++)
            {
                BehaviorNode<TContext> child = GetChild(i);
                NodeStatus childStatus = child.Tick(context, deltaTime);
                if (childStatus == NodeStatus.Failure)
                {
                    continue;
                }

                if (previousRunningIndex >= 0 && previousRunningIndex != i)
                {
                    BehaviorNode<TContext> previousChild = GetChild(previousRunningIndex);
                    if (previousChild.IsRunning)
                    {
                        previousChild.Abort(context);
                    }
                }

                _runningChildIndex = childStatus == NodeStatus.Running ? i : -1;
                return childStatus;
            }

            if (previousRunningIndex >= 0)
            {
                BehaviorNode<TContext> previousChild = GetChild(previousRunningIndex);
                if (previousChild.IsRunning)
                {
                    previousChild.Abort(context);
                }
            }

            _runningChildIndex = -1;
            return NodeStatus.Failure;
        }

        protected override void OnExit(TContext context, NodeStatus status)
        {
            base.OnExit(context, status);
            _runningChildIndex = -1;
        }
    }

    public enum ParallelPolicy
    {
        RequireOne,
        RequireAll
    }

    /// <summary>
    /// 同时推进全部子节点并按策略汇总结果
    /// </summary>
    public sealed class ParallelNode<TContext> : CompositeNode<TContext>
    {
        public ParallelNode(ParallelPolicy successPolicy, ParallelPolicy failurePolicy)
        {
            SuccessPolicy = successPolicy;
            FailurePolicy = failurePolicy;
        }

        public ParallelPolicy SuccessPolicy { get; }
        public ParallelPolicy FailurePolicy { get; }

        protected override void OnEnter(TContext context)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                GetChild(i).Reset();
            }
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            if (ChildCount == 0)
            {
                return NodeStatus.Success;
            }

            int successCount = 0;
            int failureCount = 0;
            for (int i = 0; i < ChildCount; i++)
            {
                BehaviorNode<TContext> child = GetChild(i);
                NodeStatus childStatus = child.IsTerminated
                    ? child.Status
                    : child.Tick(context, deltaTime);

                if (childStatus == NodeStatus.Success)
                {
                    successCount++;
                    if (SuccessPolicy == ParallelPolicy.RequireOne)
                    {
                        return NodeStatus.Success;
                    }
                }
                else if (childStatus == NodeStatus.Failure || childStatus == NodeStatus.Aborted)
                {
                    failureCount++;
                    if (FailurePolicy == ParallelPolicy.RequireOne)
                    {
                        return NodeStatus.Failure;
                    }
                }
            }

            if (FailurePolicy == ParallelPolicy.RequireAll && failureCount == ChildCount)
            {
                return NodeStatus.Failure;
            }

            if (SuccessPolicy == ParallelPolicy.RequireAll && successCount == ChildCount)
            {
                return NodeStatus.Success;
            }

            return NodeStatus.Running;
        }
    }
}
