using System;
using Sheng.GameFramework.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sheng.GameFramework.Timing
{
    /// <summary>
    /// 管理延迟和循环计时器
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimerManager : PersistentMonoSingleton<TimerManager>
    {
        private const int PersistentGroup = 0;

        private TimerScheduler _scheduler;

        public int Count => Scheduler.Count;

        protected override void OnSingletonAwake()
        {
            _scheduler = new TimerScheduler();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        protected override void OnSingletonDestroyed()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _scheduler?.Clear();
            _scheduler = null;
        }

        private void Update()
        {
            Scheduler.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        public TimerId Delay(
            float delaySeconds,
            Action callback,
            bool ignoreTimeScale = false,
            TimerLifetime lifetime = TimerLifetime.Scene)
        {
            return Scheduler.Delay(
                delaySeconds,
                callback,
                ResolveTimeMode(ignoreTimeScale),
                ResolveGroup(lifetime));
        }

        public TimerId Repeat(
            float intervalSeconds,
            Action callback,
            int repeatCount = -1,
            bool invokeImmediately = false,
            bool ignoreTimeScale = false,
            TimerLifetime lifetime = TimerLifetime.Scene)
        {
            return Scheduler.Repeat(
                intervalSeconds,
                callback,
                repeatCount,
                invokeImmediately,
                ResolveTimeMode(ignoreTimeScale),
                ResolveGroup(lifetime));
        }

        public bool Cancel(TimerId timerId)
        {
            return Scheduler.Cancel(timerId);
        }

        public bool Pause(TimerId timerId)
        {
            return Scheduler.Pause(timerId);
        }

        public bool Resume(TimerId timerId)
        {
            return Scheduler.Resume(timerId);
        }

        public int PauseAll()
        {
            return Scheduler.PauseAll();
        }

        public int ResumeAll()
        {
            return Scheduler.ResumeAll();
        }

        public bool IsRunning(TimerId timerId)
        {
            return Scheduler.IsRunning(timerId);
        }

        public bool IsPaused(TimerId timerId)
        {
            return Scheduler.IsPaused(timerId);
        }

        public float GetRemainingTime(TimerId timerId)
        {
            return Scheduler.TryGetRemainingTime(
                timerId,
                out float remainingSeconds)
                ? remainingSeconds
                : -1f;
        }

        public int CancelCurrentSceneTimers()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid()
                ? Scheduler.CancelGroup(activeScene.handle)
                : 0;
        }

        public void CancelAll()
        {
            Scheduler.Clear();
        }

        private TimerScheduler Scheduler =>
            _scheduler ??= new TimerScheduler();

        private static TimerTimeMode ResolveTimeMode(bool ignoreTimeScale)
        {
            return ignoreTimeScale
                ? TimerTimeMode.Unscaled
                : TimerTimeMode.Scaled;
        }

        private static int ResolveGroup(TimerLifetime lifetime)
        {
            if (lifetime == TimerLifetime.Persistent)
            {
                return PersistentGroup;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() ? activeScene.handle : PersistentGroup;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            Scheduler.CancelGroup(scene.handle);
        }
    }
}
