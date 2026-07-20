namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 行为树节点基类
    /// </summary>
    public abstract class BehaviorNode<TContext>
    {
        public NodeStatus Status { get; private set; } = NodeStatus.Invalid;
        public bool IsRunning => Status == NodeStatus.Running;
        public bool IsTerminated => Status == NodeStatus.Success
                                    || Status == NodeStatus.Failure
                                    || Status == NodeStatus.Aborted;

        public NodeStatus Tick(TContext context, float deltaTime)
        {
            if (!IsRunning)
            {
                OnEnter(context);
            }

            Status = OnTick(context, deltaTime);
            if (!IsRunning)
            {
                OnExit(context, Status);
            }

            return Status;
        }

        public void Abort(TContext context)
        {
            if (!IsRunning)
            {
                return;
            }

            Status = NodeStatus.Aborted;
            OnExit(context, Status);
        }

        public virtual void Reset()
        {
            Status = NodeStatus.Invalid;
            OnReset();
        }

        protected virtual void OnEnter(TContext context)
        {
        }

        protected abstract NodeStatus OnTick(TContext context, float deltaTime);

        protected virtual void OnExit(TContext context, NodeStatus status)
        {
        }

        protected virtual void OnReset()
        {
        }
    }
}
