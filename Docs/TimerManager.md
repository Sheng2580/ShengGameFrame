# TimerManager 计时器模块

[返回首页](../README.md)

命名空间：

```csharp
using Sheng.GameFramework.Timing;
```

核心类型：`TimerManager`、`TimerScheduler`、`TimerId`、`TimerTimeMode`、`TimerLifetime`

## 延迟执行

```csharp
TimerId timerId = TimerManager.Instance.Delay(
    2f,
    () => Debug.Log("两秒后执行"));
```

`Delay` 只执行一次。延迟可以为 `0`，此时回调会在下一次计时器更新时执行，不会在注册方法内部立即执行

## 循环执行

无限循环：

```csharp
TimerId timerId = TimerManager.Instance.Repeat(
    0.5f,
    () => Debug.Log("每 0.5 秒执行"));
```

执行三次后自动结束：

```csharp
TimerId timerId = TimerManager.Instance.Repeat(
    1f,
    OnSecond,
    repeatCount: 3);
```

`repeatCount` 表示回调总执行次数，默认 `-1` 表示无限循环。`invokeImmediately: true` 会让第一次回调在下一次计时器更新时执行，之后再按间隔循环

## 取消和状态

```csharp
TimerManager.Instance.Cancel(timerId);
TimerManager.Instance.Pause(timerId);
TimerManager.Instance.Resume(timerId);

bool running = TimerManager.Instance.IsRunning(timerId);
bool paused = TimerManager.Instance.IsPaused(timerId);
float remaining = TimerManager.Instance.GetRemainingTime(timerId);
```

不存在或已经结束的计时器剩余时间返回 `-1`

全部暂停、继续和取消：

```csharp
TimerManager.Instance.PauseAll();
TimerManager.Instance.ResumeAll();
TimerManager.Instance.CancelAll();
```

## 时间缩放

默认使用 `Time.deltaTime`，暂停游戏时计时器也会暂停：

```csharp
TimerManager.Instance.Delay(1f, callback);
```

UI 提示、网络超时等不应受 `Time.timeScale` 影响的任务使用：

```csharp
TimerManager.Instance.Delay(
    1f,
    callback,
    ignoreTimeScale: true);
```

底层分别使用 `Time.deltaTime` 和 `Time.unscaledDeltaTime`

## 场景生命周期

计时器默认属于创建时的当前场景。场景卸载时，属于该场景且还未完成的计时器会自动取消，避免旧场景回调访问已经销毁的对象

需要跨场景保留时显式指定：

```csharp
TimerId timerId = TimerManager.Instance.Delay(
    5f,
    callback,
    lifetime: TimerLifetime.Persistent);
```

手动取消当前场景的全部计时器：

```csharp
int cancelledCount = TimerManager.Instance.CancelCurrentSceneTimers();
```

## 回调安全

计时器回调中可以安全执行以下操作：

- 取消当前计时器
- 取消其他计时器
- 创建新计时器
- 清空全部计时器

回调中新建的计时器从下一次 `Tick` 开始执行，不会插入当前遍历

单帧最多为同一个循环计时器补执行 8 次，避免卡顿后产生大量回调尖峰。超出部分会丢弃积压并从下一个间隔继续

回调抛出异常时，该计时器会自动取消并通过 `Debug.LogException` 输出异常，避免无限循环持续报错

## 外部时间驱动

需要测试、服务器逻辑或固定逻辑帧时，可以直接创建不依赖 MonoBehaviour 的 `TimerScheduler`：

```csharp
TimerScheduler scheduler = new TimerScheduler();

scheduler.Delay(1f, callback);
scheduler.Tick(scaledDeltaTime, unscaledDeltaTime);
```

通过 `group` 参数可以对计时器分组并批量取消：

```csharp
scheduler.Delay(1f, callback, group: 1001);
scheduler.CancelGroup(1001);
```

`TimerManager` 内部使用场景句柄作为分组，跨场景计时器使用独立常驻分组

## 公开 API

| API | 用途 |
| --- | --- |
| `Delay` | 注册一次延迟回调 |
| `Repeat` | 注册有限或无限循环回调 |
| `Cancel` | 取消指定计时器 |
| `Pause` / `Resume` | 暂停或继续指定计时器 |
| `PauseAll` / `ResumeAll` | 暂停或继续全部计时器 |
| `IsRunning` / `IsPaused` | 查询计时器状态 |
| `GetRemainingTime` | 查询剩余时间 |
| `CancelCurrentSceneTimers` | 取消当前场景计时器 |
| `CancelAll` | 取消全部计时器 |

## 使用限制

- 计时器在 Unity 主线程更新，回调也在主线程执行
- 计时器精度受帧率影响，不适合作为高精度音频或操作系统定时器
- 计时器不负责协程返回值、异步 IO 取消或网络重试策略
- 跨场景计时器必须由业务代码在不再需要时主动取消
