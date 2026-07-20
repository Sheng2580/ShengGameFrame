using System;
using System.Collections.Generic;
using Sheng.GameFramework.Core;

namespace Sheng.GameFramework.Events
{
    /// <summary>
    /// 支持零到五个强类型参数的同步事件管理器
    /// </summary>
    public sealed class EventManager : Singleton<EventManager>
    {
        private sealed class EventSlot
        {
            public Type DelegateType;
            public Delegate Listeners;
        }

        private readonly object _syncRoot = new object();
        private readonly Dictionary<GameEvent, EventSlot> _eventSlots =
            new Dictionary<GameEvent, EventSlot>();

        public int EventCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _eventSlots.Count;
                }
            }
        }

        public void AddEventListener(GameEvent gameEvent, Action action)
        {
            AddListener(gameEvent, action, typeof(Action));
        }

        public void AddEventListener<T1>(
            GameEvent gameEvent,
            Action<T1> action)
        {
            AddListener(gameEvent, action, typeof(Action<T1>));
        }

        public void AddEventListener<T1, T2>(
            GameEvent gameEvent,
            Action<T1, T2> action)
        {
            AddListener(gameEvent, action, typeof(Action<T1, T2>));
        }

        public void AddEventListener<T1, T2, T3>(
            GameEvent gameEvent,
            Action<T1, T2, T3> action)
        {
            AddListener(gameEvent, action, typeof(Action<T1, T2, T3>));
        }

        public void AddEventListener<T1, T2, T3, T4>(
            GameEvent gameEvent,
            Action<T1, T2, T3, T4> action)
        {
            AddListener(gameEvent, action, typeof(Action<T1, T2, T3, T4>));
        }

        public void AddEventListener<T1, T2, T3, T4, T5>(
            GameEvent gameEvent,
            Action<T1, T2, T3, T4, T5> action)
        {
            AddListener(
                gameEvent,
                action,
                typeof(Action<T1, T2, T3, T4, T5>));
        }

        public bool RemoveEventListener(GameEvent gameEvent, Action action)
        {
            return RemoveListener(gameEvent, action, typeof(Action));
        }

        public bool RemoveEventListener<T1>(
            GameEvent gameEvent,
            Action<T1> action)
        {
            return RemoveListener(gameEvent, action, typeof(Action<T1>));
        }

        public bool RemoveEventListener<T1, T2>(
            GameEvent gameEvent,
            Action<T1, T2> action)
        {
            return RemoveListener(gameEvent, action, typeof(Action<T1, T2>));
        }

        public bool RemoveEventListener<T1, T2, T3>(
            GameEvent gameEvent,
            Action<T1, T2, T3> action)
        {
            return RemoveListener(
                gameEvent,
                action,
                typeof(Action<T1, T2, T3>));
        }

        public bool RemoveEventListener<T1, T2, T3, T4>(
            GameEvent gameEvent,
            Action<T1, T2, T3, T4> action)
        {
            return RemoveListener(
                gameEvent,
                action,
                typeof(Action<T1, T2, T3, T4>));
        }

        public bool RemoveEventListener<T1, T2, T3, T4, T5>(
            GameEvent gameEvent,
            Action<T1, T2, T3, T4, T5> action)
        {
            return RemoveListener(
                gameEvent,
                action,
                typeof(Action<T1, T2, T3, T4, T5>));
        }

        public bool EventTrigger(GameEvent gameEvent)
        {
            Action listeners = GetListeners<Action>(gameEvent);
            if (listeners == null)
            {
                return false;
            }

            listeners.Invoke();
            return true;
        }

        public bool EventTrigger<T1>(GameEvent gameEvent, T1 value1)
        {
            Action<T1> listeners = GetListeners<Action<T1>>(gameEvent);
            if (listeners == null)
            {
                return false;
            }

            listeners.Invoke(value1);
            return true;
        }

        public bool EventTrigger<T1, T2>(
            GameEvent gameEvent,
            T1 value1,
            T2 value2)
        {
            Action<T1, T2> listeners =
                GetListeners<Action<T1, T2>>(gameEvent);
            if (listeners == null)
            {
                return false;
            }

            listeners.Invoke(value1, value2);
            return true;
        }

        public bool EventTrigger<T1, T2, T3>(
            GameEvent gameEvent,
            T1 value1,
            T2 value2,
            T3 value3)
        {
            Action<T1, T2, T3> listeners =
                GetListeners<Action<T1, T2, T3>>(gameEvent);
            if (listeners == null)
            {
                return false;
            }

            listeners.Invoke(value1, value2, value3);
            return true;
        }

        public bool EventTrigger<T1, T2, T3, T4>(
            GameEvent gameEvent,
            T1 value1,
            T2 value2,
            T3 value3,
            T4 value4)
        {
            Action<T1, T2, T3, T4> listeners =
                GetListeners<Action<T1, T2, T3, T4>>(gameEvent);
            if (listeners == null)
            {
                return false;
            }

            listeners.Invoke(value1, value2, value3, value4);
            return true;
        }

        public bool EventTrigger<T1, T2, T3, T4, T5>(
            GameEvent gameEvent,
            T1 value1,
            T2 value2,
            T3 value3,
            T4 value4,
            T5 value5)
        {
            Action<T1, T2, T3, T4, T5> listeners =
                GetListeners<Action<T1, T2, T3, T4, T5>>(gameEvent);
            if (listeners == null)
            {
                return false;
            }

            listeners.Invoke(value1, value2, value3, value4, value5);
            return true;
        }

        public bool HasListeners(GameEvent gameEvent)
        {
            ValidateGameEvent(gameEvent);
            lock (_syncRoot)
            {
                return _eventSlots.TryGetValue(
                           gameEvent,
                           out EventSlot slot)
                       && slot.Listeners != null;
            }
        }

        public int GetListenerCount(GameEvent gameEvent)
        {
            ValidateGameEvent(gameEvent);
            lock (_syncRoot)
            {
                return _eventSlots.TryGetValue(
                           gameEvent,
                           out EventSlot slot)
                       && slot.Listeners != null
                    ? slot.Listeners.GetInvocationList().Length
                    : 0;
            }
        }

        public bool ClearEvent(GameEvent gameEvent)
        {
            ValidateGameEvent(gameEvent);
            lock (_syncRoot)
            {
                return _eventSlots.Remove(gameEvent);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _eventSlots.Clear();
            }
        }

        private void AddListener(
            GameEvent gameEvent,
            Delegate action,
            Type delegateType)
        {
            ValidateGameEvent(gameEvent);
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            lock (_syncRoot)
            {
                if (!_eventSlots.TryGetValue(gameEvent, out EventSlot slot))
                {
                    slot = new EventSlot
                    {
                        DelegateType = delegateType
                    };
                    _eventSlots.Add(gameEvent, slot);
                }
                else
                {
                    EnsureSignature(gameEvent, slot, delegateType);
                }

                slot.Listeners = Delegate.Combine(slot.Listeners, action);
            }
        }

        private bool RemoveListener(
            GameEvent gameEvent,
            Delegate action,
            Type delegateType)
        {
            ValidateGameEvent(gameEvent);
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            lock (_syncRoot)
            {
                if (!_eventSlots.TryGetValue(gameEvent, out EventSlot slot))
                {
                    return false;
                }

                EnsureSignature(gameEvent, slot, delegateType);
                Delegate updated = Delegate.Remove(slot.Listeners, action);
                if (ReferenceEquals(updated, slot.Listeners))
                {
                    return false;
                }

                if (updated == null)
                {
                    _eventSlots.Remove(gameEvent);
                }
                else
                {
                    slot.Listeners = updated;
                }

                return true;
            }
        }

        private TDelegate GetListeners<TDelegate>(GameEvent gameEvent)
            where TDelegate : Delegate
        {
            ValidateGameEvent(gameEvent);
            lock (_syncRoot)
            {
                if (!_eventSlots.TryGetValue(gameEvent, out EventSlot slot))
                {
                    return null;
                }

                Type delegateType = typeof(TDelegate);
                EnsureSignature(gameEvent, slot, delegateType);
                return (TDelegate)slot.Listeners;
            }
        }

        private static void EnsureSignature(
            GameEvent gameEvent,
            EventSlot slot,
            Type expectedType)
        {
            if (slot.DelegateType == expectedType)
            {
                return;
            }

            throw new InvalidOperationException(
                $"事件 {gameEvent} 参数签名不一致 "
                + $"已注册 {FormatDelegateType(slot.DelegateType)} "
                + $"当前使用 {FormatDelegateType(expectedType)}");
        }

        private static string FormatDelegateType(Type delegateType)
        {
            if (delegateType == null)
            {
                return "None";
            }

            if (!delegateType.IsGenericType)
            {
                return delegateType.Name;
            }

            Type[] arguments = delegateType.GetGenericArguments();
            string[] names = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                names[i] = arguments[i].Name;
            }

            return "Action<" + string.Join(", ", names) + ">";
        }

        private static void ValidateGameEvent(GameEvent gameEvent)
        {
            if (!gameEvent.IsValid)
            {
                throw new ArgumentException("GameEvent 无效", nameof(gameEvent));
            }
        }
    }
}
