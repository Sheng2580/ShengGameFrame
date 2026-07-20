using System;

namespace Sheng.GameFramework.StateMachine
{
    /// <summary>
    /// 泛型状态基类
    /// </summary>
    public abstract class State<TOwner, TStateId>
        where TStateId : struct, Enum
    {
        public abstract TStateId Id { get; }
        public TOwner Owner { get; private set; }
        public StateMachine<TOwner, TStateId> Machine { get; private set; }
        public bool IsInitialized { get; private set; }

        public virtual bool CanEnterFrom(TStateId? previousStateId)
        {
            return true;
        }

        public virtual bool CanExitTo(TStateId nextStateId)
        {
            return true;
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual void OnEnter()
        {
        }

        protected virtual void OnTick(float deltaTime)
        {
        }

        protected virtual void OnFixedTick(float fixedDeltaTime)
        {
        }

        protected virtual void OnLateTick(float deltaTime)
        {
        }

        protected virtual void OnExit()
        {
        }

        protected virtual void OnDispose()
        {
        }

        internal void Initialize(TOwner owner, StateMachine<TOwner, TStateId> machine)
        {
            if (IsInitialized)
            {
                if (!ReferenceEquals(Machine, machine))
                {
                    throw new InvalidOperationException($"状态 {Id} 已绑定到其他状态机");
                }

                return;
            }

            Owner = owner;
            Machine = machine;
            IsInitialized = true;
            OnInitialize();
        }

        internal void Enter()
        {
            OnEnter();
        }

        internal void Tick(float deltaTime)
        {
            OnTick(deltaTime);
        }

        internal void FixedTick(float fixedDeltaTime)
        {
            OnFixedTick(fixedDeltaTime);
        }

        internal void LateTick(float deltaTime)
        {
            OnLateTick(deltaTime);
        }

        internal void Exit()
        {
            OnExit();
        }

        internal void Dispose()
        {
            if (!IsInitialized)
            {
                return;
            }

            OnDispose();
            Owner = default;
            Machine = null;
            IsInitialized = false;
        }
    }
}
