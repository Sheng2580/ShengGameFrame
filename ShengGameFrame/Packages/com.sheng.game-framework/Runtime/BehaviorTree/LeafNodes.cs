using System;

namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 通过布尔条件返回成功或失败
    /// </summary>
    public sealed class ConditionNode<TContext> : BehaviorNode<TContext>
    {
        private readonly Func<TContext, bool> _condition;

        public ConditionNode(Func<TContext, bool> condition)
        {
            _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            return _condition.Invoke(context) ? NodeStatus.Success : NodeStatus.Failure;
        }
    }

    /// <summary>
    /// 通过委托执行具体行为
    /// </summary>
    public sealed class ActionNode<TContext> : BehaviorNode<TContext>
    {
        private readonly Func<TContext, float, NodeStatus> _action;

        public ActionNode(Func<TContext, float, NodeStatus> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public ActionNode(Func<TContext, NodeStatus> action)
            : this((context, _) => action.Invoke(context))
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }
        }

        protected override NodeStatus OnTick(TContext context, float deltaTime)
        {
            return _action.Invoke(context, deltaTime);
        }
    }
}
