using System;

namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 由外部逻辑帧驱动的泛型行为树
    /// </summary>
    public sealed class BehaviorTree<TContext>
    {
        public BehaviorTree(BehaviorNode<TContext> root, TContext context)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Context = context;
        }

        public BehaviorNode<TContext> Root { get; }
        public TContext Context { get; set; }
        public ulong TickCount { get; private set; }
        public NodeStatus Status => Root.Status;

        public NodeStatus Tick(float deltaTime)
        {
            TickCount++;
            return Root.Tick(Context, deltaTime);
        }

        public void Abort()
        {
            Root.Abort(Context);
        }

        public void Reset()
        {
            if (Root.IsRunning)
            {
                Root.Abort(Context);
            }

            Root.Reset();
            TickCount = 0;
        }
    }
}
