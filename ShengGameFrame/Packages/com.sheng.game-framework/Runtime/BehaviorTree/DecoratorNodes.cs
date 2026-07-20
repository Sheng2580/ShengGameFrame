namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 反转子节点的成功和失败结果
    /// </summary>
    public sealed class InverterNode<TContext> : DecoratorNode<TContext>
    {
        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            NodeStatus childStatus = GetRequiredChild().Tick(context, deltaTime);
            switch (childStatus)
            {
                case NodeStatus.Success:
                    return NodeStatus.Failure;
                case NodeStatus.Failure:
                    return NodeStatus.Success;
                default:
                    return childStatus;
            }
        }
    }

    /// <summary>
    /// 重复执行子节点 负数次数代表无限重复
    /// </summary>
    public sealed class RepeatNode<TContext> : DecoratorNode<TContext>
    {
        private int _completedCount;

        public RepeatNode(int repeatCount)
        {
            RepeatCount = repeatCount;
        }

        public int RepeatCount { get; }

        protected override void OnEnter(TContext context)
        {
            _completedCount = 0;
            Child?.Reset();
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            if (RepeatCount == 0)
            {
                return NodeStatus.Success;
            }

            BehaviorNode<TContext> child = GetRequiredChild();
            NodeStatus childStatus = child.Tick(context, deltaTime);
            if (childStatus == NodeStatus.Running)
            {
                return NodeStatus.Running;
            }

            if (childStatus != NodeStatus.Success)
            {
                return NodeStatus.Failure;
            }

            _completedCount++;
            if (RepeatCount > 0 && _completedCount >= RepeatCount)
            {
                return NodeStatus.Success;
            }

            child.Reset();
            return NodeStatus.Running;
        }
    }
}
