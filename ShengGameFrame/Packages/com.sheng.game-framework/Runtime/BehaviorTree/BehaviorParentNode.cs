using System;
using System.Collections.Generic;

namespace Sheng.GameFramework.BehaviorTree
{
    public interface IBehaviorParentNode<TContext>
    {
        void AddChild(BehaviorNode<TContext> child);
    }

    /// <summary>
    /// 拥有多个子节点的组合节点
    /// </summary>
    public abstract class CompositeNode<TContext> :
        BehaviorNode<TContext>,
        IBehaviorParentNode<TContext>
    {
        private readonly List<BehaviorNode<TContext>> _children =
            new List<BehaviorNode<TContext>>();

        public IReadOnlyList<BehaviorNode<TContext>> Children => _children;
        protected int ChildCount => _children.Count;

        public virtual void AddChild(BehaviorNode<TContext> child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            _children.Add(child);
        }

        public bool RemoveChild(BehaviorNode<TContext> child)
        {
            return child != null && _children.Remove(child);
        }

        public override void Reset()
        {
            base.Reset();
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Reset();
            }
        }

        protected BehaviorNode<TContext> GetChild(int index)
        {
            return _children[index];
        }

        protected override void OnExit(TContext context, NodeStatus status)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                if (_children[i].IsRunning)
                {
                    _children[i].Abort(context);
                }
            }
        }
    }

    /// <summary>
    /// 拥有一个子节点的装饰节点
    /// </summary>
    public abstract class DecoratorNode<TContext> :
        BehaviorNode<TContext>,
        IBehaviorParentNode<TContext>
    {
        public BehaviorNode<TContext> Child { get; private set; }

        public void AddChild(BehaviorNode<TContext> child)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (Child != null)
            {
                throw new InvalidOperationException("装饰节点只能拥有一个子节点");
            }

            Child = child;
        }

        public override void Reset()
        {
            base.Reset();
            Child?.Reset();
        }

        protected BehaviorNode<TContext> GetRequiredChild()
        {
            if (Child == null)
            {
                throw new InvalidOperationException("装饰节点缺少子节点");
            }

            return Child;
        }

        protected override void OnExit(TContext context, NodeStatus status)
        {
            if (Child != null && Child.IsRunning)
            {
                Child.Abort(context);
            }
        }
    }
}
