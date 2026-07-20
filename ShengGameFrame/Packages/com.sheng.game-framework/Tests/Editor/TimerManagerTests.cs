using NUnit.Framework;
using Sheng.GameFramework.Timing;

namespace Sheng.GameFramework.Tests
{
    public sealed class TimerManagerTests
    {
        [Test]
        public void Delay_ExecutesOnceAfterElapsedTime()
        {
            TimerScheduler scheduler = new TimerScheduler();
            int callbackCount = 0;
            TimerId timerId = scheduler.Delay(1f, () => callbackCount++);

            scheduler.Tick(0.4f, 0.4f);
            scheduler.Tick(0.5f, 0.5f);
            Assert.AreEqual(0, callbackCount);
            Assert.IsTrue(scheduler.IsRunning(timerId));

            scheduler.Tick(0.2f, 0.2f);
            Assert.AreEqual(1, callbackCount);
            Assert.IsFalse(scheduler.IsRunning(timerId));
        }

        [Test]
        public void Repeat_StopsAfterConfiguredExecutionCount()
        {
            TimerScheduler scheduler = new TimerScheduler();
            int callbackCount = 0;
            TimerId timerId = scheduler.Repeat(
                0.5f,
                () => callbackCount++,
                repeatCount: 3);

            scheduler.Tick(1.6f, 1.6f);

            Assert.AreEqual(3, callbackCount);
            Assert.IsFalse(scheduler.IsRunning(timerId));
        }

        [Test]
        public void Callback_CanCancelItsOwnInfiniteTimer()
        {
            TimerScheduler scheduler = new TimerScheduler();
            TimerId timerId = default;
            int callbackCount = 0;
            timerId = scheduler.Repeat(
                0.1f,
                () =>
                {
                    callbackCount++;
                    scheduler.Cancel(timerId);
                });

            scheduler.Tick(1f, 1f);

            Assert.AreEqual(1, callbackCount);
            Assert.AreEqual(0, scheduler.Count);
        }

        [Test]
        public void PauseAndResume_PreserveRemainingTime()
        {
            TimerScheduler scheduler = new TimerScheduler();
            int callbackCount = 0;
            TimerId timerId = scheduler.Delay(1f, () => callbackCount++);
            scheduler.Tick(0.4f, 0.4f);

            Assert.IsTrue(scheduler.Pause(timerId));
            scheduler.Tick(5f, 5f);
            Assert.AreEqual(0, callbackCount);
            Assert.IsTrue(scheduler.TryGetRemainingTime(
                timerId,
                out float remainingSeconds));
            Assert.AreEqual(0.6f, remainingSeconds, 0.001f);

            Assert.IsTrue(scheduler.Resume(timerId));
            scheduler.Tick(0.7f, 0.7f);
            Assert.AreEqual(1, callbackCount);
        }

        [Test]
        public void TimeMode_UsesMatchingDeltaTime()
        {
            TimerScheduler scheduler = new TimerScheduler();
            int scaledCount = 0;
            int unscaledCount = 0;
            scheduler.Delay(1f, () => scaledCount++);
            scheduler.Delay(
                1f,
                () => unscaledCount++,
                TimerTimeMode.Unscaled);

            scheduler.Tick(0f, 1.1f);

            Assert.AreEqual(0, scaledCount);
            Assert.AreEqual(1, unscaledCount);
        }

        [Test]
        public void CancelGroup_OnlyRemovesMatchingTimers()
        {
            TimerScheduler scheduler = new TimerScheduler();
            TimerId sceneTimer = scheduler.Delay(1f, () => { }, group: 12);
            TimerId persistentTimer = scheduler.Delay(1f, () => { }, group: 0);

            Assert.AreEqual(1, scheduler.CancelGroup(12));
            Assert.IsFalse(scheduler.IsRunning(sceneTimer));
            Assert.IsTrue(scheduler.IsRunning(persistentTimer));
        }

        [Test]
        public void Callback_CreatedTimerStartsOnNextTick()
        {
            TimerScheduler scheduler = new TimerScheduler();
            int callbackCount = 0;
            scheduler.Delay(
                0f,
                () =>
                {
                    callbackCount++;
                    scheduler.Delay(0f, () => callbackCount++);
                });

            scheduler.Tick(0f, 0f);
            Assert.AreEqual(1, callbackCount);

            scheduler.Tick(0f, 0f);
            Assert.AreEqual(2, callbackCount);
        }

        [Test]
        public void CatchUp_RespectsPerTickCallbackLimit()
        {
            TimerScheduler scheduler = new TimerScheduler
            {
                MaxCallbacksPerTick = 3
            };
            int callbackCount = 0;
            scheduler.Repeat(0.1f, () => callbackCount++);

            scheduler.Tick(1f, 1f);
            Assert.AreEqual(3, callbackCount);

            scheduler.Tick(0.1f, 0.1f);
            Assert.AreEqual(4, callbackCount);
        }
    }
}
