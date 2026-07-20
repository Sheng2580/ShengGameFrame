using System;
using System.Collections.Generic;

namespace Sheng.GameFramework.StateMachine
{
    /// <summary>
    /// 由外部逻辑帧驱动的泛型状态机
    /// </summary>
    public sealed class StateMachine<TOwner, TStateId>
        where TStateId : struct, Enum
    {
        private readonly Dictionary<TStateId, State<TOwner, TStateId>> _states =
            new Dictionary<TStateId, State<TOwner, TStateId>>();

        private bool _isTransitioning;
        private bool _hasPendingState;
        private bool _pendingForce;
        private TStateId _pendingStateId;

        public StateMachine(TOwner owner)
        {
            Owner = owner;
        }

        public TOwner Owner { get; }
        public State<TOwner, TStateId> CurrentState { get; private set; }
        public bool HasCurrentState => CurrentState != null;
        public TStateId? CurrentStateId => CurrentState != null ? CurrentState.Id : null;
        public int StateCount => _states.Count;

        public event Action<TStateId?, TStateId> StateChanged;

        public void Register(State<TOwner, TStateId> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_states.ContainsKey(state.Id))
            {
                throw new InvalidOperationException($"状态编号重复 {state.Id}");
            }

            state.Initialize(Owner, this);
            _states.Add(state.Id, state);
        }

        public bool Unregister(TStateId stateId)
        {
            if (!_states.TryGetValue(stateId, out State<TOwner, TStateId> state)
                || state == CurrentState)
            {
                return false;
            }

            _states.Remove(stateId);
            state.Dispose();
            return true;
        }

        public bool Contains(TStateId stateId)
        {
            return _states.ContainsKey(stateId);
        }

        public bool TryGetState(TStateId stateId, out State<TOwner, TStateId> state)
        {
            return _states.TryGetValue(stateId, out state);
        }

        public bool Start(TStateId stateId)
        {
            if (CurrentState != null)
            {
                return false;
            }

            return ChangeState(stateId);
        }

        public bool ChangeState(TStateId stateId, bool force = false)
        {
            if (_isTransitioning)
            {
                RequestState(stateId, force);
                return false;
            }

            if (!_states.TryGetValue(stateId, out State<TOwner, TStateId> nextState))
            {
                return false;
            }

            if (!force && CurrentState == nextState)
            {
                return false;
            }

            TStateId? previousStateId = CurrentStateId;
            if (!force
                && CurrentState != null
                && !CurrentState.CanExitTo(stateId))
            {
                return false;
            }

            if (!force && !nextState.CanEnterFrom(previousStateId))
            {
                return false;
            }

            _isTransitioning = true;
            try
            {
                CurrentState?.Exit();
                CurrentState = nextState;
                CurrentState.Enter();
            }
            finally
            {
                _isTransitioning = false;
            }

            StateChanged?.Invoke(previousStateId, stateId);
            return true;
        }

        public void RequestState(TStateId stateId, bool force = false)
        {
            _pendingStateId = stateId;
            _pendingForce = force;
            _hasPendingState = true;
        }

        public bool ApplyPendingState()
        {
            if (!_hasPendingState)
            {
                return false;
            }

            TStateId stateId = _pendingStateId;
            bool force = _pendingForce;
            _hasPendingState = false;
            _pendingForce = false;
            return ChangeState(stateId, force);
        }

        public void Tick(float deltaTime)
        {
            ApplyPendingState();
            CurrentState?.Tick(deltaTime);
            ApplyPendingState();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            ApplyPendingState();
            CurrentState?.FixedTick(fixedDeltaTime);
            ApplyPendingState();
        }

        public void LateTick(float deltaTime)
        {
            ApplyPendingState();
            CurrentState?.LateTick(deltaTime);
            ApplyPendingState();
        }

        public void Stop(bool callExit = true)
        {
            _hasPendingState = false;
            _pendingForce = false;
            if (CurrentState == null)
            {
                return;
            }

            State<TOwner, TStateId> oldState = CurrentState;
            CurrentState = null;
            if (callExit)
            {
                oldState.Exit();
            }
        }

        public void Clear(bool callExit = true)
        {
            Stop(callExit);
            foreach (KeyValuePair<TStateId, State<TOwner, TStateId>> pair in _states)
            {
                pair.Value.Dispose();
            }

            _states.Clear();
        }
    }
}
