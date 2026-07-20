using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheng.GameFramework.Timing
{
    /// <summary>
    /// 由外部时间驱动的计时器调度器
    /// </summary>
    public sealed class TimerScheduler
    {
        private sealed class TimerEntry
        {
            public TimerId Id;
            public Action Callback;
            public float Interval;
            public float Remaining;
            public int RemainingExecutions;
            public int Group;
            public TimerTimeMode TimeMode;
            public bool IsPaused;
        }

        private readonly Dictionary<int, TimerEntry> _timers =
            new Dictionary<int, TimerEntry>();
        private readonly List<int> _tickBuffer = new List<int>();
        private readonly List<int> _groupBuffer = new List<int>();
        private readonly Action<Exception> _exceptionHandler;

        private int _nextId;
        private int _maxCallbacksPerTick = 8;

        public TimerScheduler(Action<Exception> exceptionHandler = null)
        {
            _exceptionHandler = exceptionHandler ?? Debug.LogException;
        }

        public int Count => _timers.Count;

        public int MaxCallbacksPerTick
        {
            get => _maxCallbacksPerTick;
            set => _maxCallbacksPerTick = Math.Max(1, value);
        }

        public TimerId Delay(
            float delaySeconds,
            Action callback,
            TimerTimeMode timeMode = TimerTimeMode.Scaled,
            int group = 0)
        {
            ValidateSeconds(delaySeconds, true, nameof(delaySeconds));
            ValidateCallback(callback);
            return AddTimer(
                delaySeconds,
                delaySeconds,
                1,
                callback,
                timeMode,
                group);
        }

        public TimerId Repeat(
            float intervalSeconds,
            Action callback,
            int repeatCount = -1,
            bool invokeImmediately = false,
            TimerTimeMode timeMode = TimerTimeMode.Scaled,
            int group = 0)
        {
            ValidateSeconds(intervalSeconds, false, nameof(intervalSeconds));
            ValidateCallback(callback);
            if (repeatCount == 0 || repeatCount < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatCount),
                    "重复次数只能是正数或 -1");
            }

            return AddTimer(
                invokeImmediately ? 0f : intervalSeconds,
                intervalSeconds,
                repeatCount,
                callback,
                timeMode,
                group);
        }

        public bool Cancel(TimerId timerId)
        {
            return timerId.IsValid && _timers.Remove(timerId.Value);
        }

        public int CancelGroup(int group)
        {
            _groupBuffer.Clear();
            foreach (KeyValuePair<int, TimerEntry> pair in _timers)
            {
                if (pair.Value.Group == group)
                {
                    _groupBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _groupBuffer.Count; i++)
            {
                _timers.Remove(_groupBuffer[i]);
            }

            int cancelledCount = _groupBuffer.Count;
            _groupBuffer.Clear();
            return cancelledCount;
        }

        public bool Pause(TimerId timerId)
        {
            if (!TryGetEntry(timerId, out TimerEntry entry) || entry.IsPaused)
            {
                return false;
            }

            entry.IsPaused = true;
            return true;
        }

        public bool Resume(TimerId timerId)
        {
            if (!TryGetEntry(timerId, out TimerEntry entry) || !entry.IsPaused)
            {
                return false;
            }

            entry.IsPaused = false;
            return true;
        }

        public int PauseAll()
        {
            int changedCount = 0;
            foreach (TimerEntry entry in _timers.Values)
            {
                if (entry.IsPaused)
                {
                    continue;
                }

                entry.IsPaused = true;
                changedCount++;
            }

            return changedCount;
        }

        public int ResumeAll()
        {
            int changedCount = 0;
            foreach (TimerEntry entry in _timers.Values)
            {
                if (!entry.IsPaused)
                {
                    continue;
                }

                entry.IsPaused = false;
                changedCount++;
            }

            return changedCount;
        }

        public bool IsRunning(TimerId timerId)
        {
            return TryGetEntry(timerId, out _);
        }

        public bool IsPaused(TimerId timerId)
        {
            return TryGetEntry(timerId, out TimerEntry entry) && entry.IsPaused;
        }

        public bool TryGetRemainingTime(
            TimerId timerId,
            out float remainingSeconds)
        {
            if (!TryGetEntry(timerId, out TimerEntry entry))
            {
                remainingSeconds = -1f;
                return false;
            }

            remainingSeconds = Math.Max(0f, entry.Remaining);
            return true;
        }

        public void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            ValidateDeltaTime(scaledDeltaTime, nameof(scaledDeltaTime));
            ValidateDeltaTime(unscaledDeltaTime, nameof(unscaledDeltaTime));

            _tickBuffer.Clear();
            _tickBuffer.AddRange(_timers.Keys);
            for (int i = 0; i < _tickBuffer.Count; i++)
            {
                int timerKey = _tickBuffer[i];
                if (!_timers.TryGetValue(timerKey, out TimerEntry entry)
                    || entry.IsPaused)
                {
                    continue;
                }

                float deltaTime = entry.TimeMode == TimerTimeMode.Unscaled
                    ? unscaledDeltaTime
                    : scaledDeltaTime;
                entry.Remaining -= deltaTime;
                ExecuteElapsedTimer(entry);
            }

            _tickBuffer.Clear();
        }

        public void Clear()
        {
            _timers.Clear();
            _tickBuffer.Clear();
            _groupBuffer.Clear();
        }

        private TimerId AddTimer(
            float initialDelay,
            float interval,
            int repeatCount,
            Action callback,
            TimerTimeMode timeMode,
            int group)
        {
            TimerId timerId = NextId();
            _timers.Add(
                timerId.Value,
                new TimerEntry
                {
                    Id = timerId,
                    Callback = callback,
                    Interval = interval,
                    Remaining = initialDelay,
                    RemainingExecutions = repeatCount,
                    Group = group,
                    TimeMode = timeMode
                });
            return timerId;
        }

        private void ExecuteElapsedTimer(TimerEntry entry)
        {
            int callbackCount = 0;
            while (entry.Remaining <= 0f
                   && callbackCount < _maxCallbacksPerTick
                   && IsCurrentEntry(entry))
            {
                callbackCount++;
                if (entry.RemainingExecutions > 0)
                {
                    entry.RemainingExecutions--;
                }

                try
                {
                    entry.Callback.Invoke();
                }
                catch (Exception exception)
                {
                    _timers.Remove(entry.Id.Value);
                    _exceptionHandler.Invoke(exception);
                    return;
                }

                if (!IsCurrentEntry(entry))
                {
                    return;
                }

                if (entry.RemainingExecutions == 0)
                {
                    _timers.Remove(entry.Id.Value);
                    return;
                }

                entry.Remaining += entry.Interval;
            }

            if (entry.Remaining <= 0f && IsCurrentEntry(entry))
            {
                entry.Remaining = entry.Interval;
            }
        }

        private bool IsCurrentEntry(TimerEntry entry)
        {
            return _timers.TryGetValue(entry.Id.Value, out TimerEntry current)
                   && ReferenceEquals(current, entry);
        }

        private bool TryGetEntry(TimerId timerId, out TimerEntry entry)
        {
            entry = null;
            return timerId.IsValid
                   && _timers.TryGetValue(timerId.Value, out entry);
        }

        private TimerId NextId()
        {
            do
            {
                _nextId = _nextId == int.MaxValue ? 1 : _nextId + 1;
            }
            while (_timers.ContainsKey(_nextId));

            return new TimerId(_nextId);
        }

        private static void ValidateCallback(Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }
        }

        private static void ValidateSeconds(
            float value,
            bool allowZero,
            string parameterName)
        {
            bool invalidRange = allowZero ? value < 0f : value <= 0f;
            if (invalidRange || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateDeltaTime(float value, string parameterName)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
