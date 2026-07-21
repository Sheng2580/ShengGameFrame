# SceneManager 场景模块

[返回首页](../README.md)

命名空间：

```csharp
using Sheng.GameFramework.Scenes;
```

核心类型：`SceneManager`、`SceneLoadOptions`、`SceneLoadRequest`、`SceneLoadStatus`

`SceneManager` 是跨场景单例，负责串行异步加载、进度、Additive 场景、卸载、活动场景切换和生命周期事件

## 最简单加载

```csharp
SceneManager.Instance.LoadSceneAsync("Game");
```

监听加载完成：

```csharp
SceneManager.Instance.LoadSceneAsync(
    "Game",
    () => Debug.Log("加载完成"));
```

需要取得加载后的 `Scene` 时使用带场景参数的重载：

```csharp
SceneManager.Instance.LoadSceneAsync(
    "Game",
    scene => Debug.Log($"加载完成 {scene.name}"));
```

场景必须已经加入 `File > Build Settings` 并启用。异步加载和卸载只能在运行模式中调用

## 与 Unity SceneManager 同名

同时引用 `UnityEngine.SceneManagement` 时，建议使用别名：

```csharp
using FrameworkSceneManager = Sheng.GameFramework.Scenes.SceneManager;

FrameworkSceneManager.Instance.LoadSceneAsync("Game");
```

框架内部也使用别名区分 Unity 原生场景 API

## 加载选项

```csharp
SceneLoadRequest request = SceneManager.Instance.LoadSceneAsync(
    "Battle",
    new SceneLoadOptions
    {
        Mode = LoadSceneMode.Single,
        MinimumDuration = 0.5f,
        UnloadUnusedAssetsAfterLoad = true
    },
    scene => Debug.Log($"加载完成 {scene.name}"),
    progress => loadingBar.value = progress,
    error => Debug.LogError(error));
```

| 选项 | 默认值 | 说明 |
| --- | --- | --- |
| `Mode` | `Single` | 单场景替换或 Additive 叠加 |
| `SetActiveAfterLoad` | `false` | Additive 加载完成后是否设为活动场景 |
| `UnloadUnusedAssetsAfterLoad` | `false` | 完成前是否等待 `Resources.UnloadUnusedAssets` |
| `MinimumDuration` | `0` | 使用不受 `timeScale` 影响的最短加载时间 |

进度范围是 `0` 到 `1`：

- 加载资源阶段最高到 `0.9`
- 准备激活时进入 `0.95`
- 场景激活及可选资源清理完成后变为 `1`

`MinimumDuration` 适合避免加载界面快速闪烁，不代表强制延长场景激活后的等待时间

## Additive 加载

```csharp
SceneManager.Instance.LoadSceneAdditiveAsync(
    "BattleLighting",
    setActiveAfterLoad: false,
    scene => Debug.Log($"叠加场景加载完成 {scene.name}"));
```

需要把叠加场景设为活动场景时传入 `true`，后续未指定场景的新对象会创建到该场景

## 请求队列

同一时间只有一个框架加载请求会交给 Unity，后续请求按照调用顺序排队：

```csharp
SceneLoadRequest first = SceneManager.Instance.LoadSceneAsync("Lobby");
SceneLoadRequest second = SceneManager.Instance.LoadSceneAsync("Battle");
```

此时 `Lobby` 先加载，`Battle` 保持 `Queued`

```csharp
bool cancelled = SceneManager.Instance.CancelPendingLoad(second);
int clearedCount = SceneManager.Instance.ClearPendingLoads();
```

Unity 的 `AsyncOperation` 开始后不能真正撤销，因此：

- `CancelPendingLoad` 只取消状态为 `Queued` 的请求
- 当前 `Loading` 或 `Activating` 请求会返回 `false`
- 框架不会通过永久关闭 `allowSceneActivation` 伪装取消，否则加载可能一直卡在 90%

## 请求状态

```csharp
SceneLoadRequest request = SceneManager.Instance.LoadSceneAsync("Game");

Debug.Log(request.Status);
Debug.Log(request.Progress);
Debug.Log(request.IsDone);
Debug.Log(request.Succeeded);
Debug.Log(request.Error);
```

| 状态 | 含义 |
| --- | --- |
| `Queued` | 等待前面的场景请求完成 |
| `Loading` | Unity 正在加载场景数据 |
| `Activating` | 场景正在激活或清理未使用资源 |
| `Succeeded` | 加载完成 |
| `Failed` | 参数、Build Settings 或 Unity 加载失败 |
| `Cancelled` | 排队期间被取消 |

## 卸载和重载

卸载 Additive 场景：

```csharp
SceneManager.Instance.UnloadSceneAsync(
    "BattleLighting",
    () => Debug.Log("卸载完成"),
    error => Debug.LogError(error));
```

重载当前活动场景：

```csharp
SceneManager.Instance.ReloadActiveSceneAsync();
```

查询和切换活动场景：

```csharp
bool loaded = SceneManager.Instance.IsSceneLoaded("BattleLighting");
bool changed = SceneManager.Instance.SetActiveScene("BattleLighting");
Scene activeScene = SceneManager.Instance.GetActiveScene();
```

重复卸载同一场景会被拒绝。Unity 不允许卸载的场景也会进入失败回调

## 生命周期事件

```csharp
SceneManager manager = SceneManager.Instance;
manager.LoadStarted += OnLoadStarted;
manager.LoadProgressChanged += OnLoadProgressChanged;
manager.LoadCompleted += OnLoadCompleted;
manager.LoadFailed += OnLoadFailed;
manager.LoadCancelled += OnLoadCancelled;

manager.SceneLoaded += OnSceneLoaded;
manager.SceneUnloaded += OnSceneUnloaded;
manager.ActiveSceneChanged += OnActiveSceneChanged;
```

| 事件 | 触发范围 |
| --- | --- |
| `LoadStarted` | 框架请求开始执行 |
| `LoadProgressChanged` | 框架请求进度增加 |
| `LoadCompleted` | 框架请求完整结束 |
| `LoadFailed` | 框架请求失败 |
| `LoadCancelled` | 排队请求被取消 |
| `SceneLoaded` | Unity 加载了任意场景 |
| `SceneUnloaded` | Unity 卸载了任意场景 |
| `ActiveSceneChanged` | Unity 活动场景发生变化 |

后三个事件直接转发 Unity 生命周期，因此业务代码绕过框架直接调用 Unity API 时仍然会触发

事件和回调中的异常会单独写入 Console，不会阻止其他监听者或后续排队请求执行。不再监听时仍应主动取消订阅

## 与其他模块配合

- `PoolManager` 继续监听 Unity 场景卸载事件并清理 `Scene` 生命周期对象池
- `TimerManager` 继续按场景句柄取消场景计时器
- `UIManager` 在场景加载后补齐 EventSystem
- 加载界面可以通过 `LoadProgressChanged` 更新，不需要轮询管理器
- AB 场景加载不在本模块范围内，当前只支持 Build Settings 场景

## 公开 API

| API | 说明 |
| --- | --- |
| `LoadSceneAsync` | 使用默认或自定义选项排队加载场景 |
| `LoadSceneAdditiveAsync` | 便捷叠加加载 |
| `ReloadActiveSceneAsync` | 重载当前活动场景 |
| `CancelPendingLoad` | 取消一个排队请求 |
| `ClearPendingLoads` | 取消全部排队请求 |
| `UnloadSceneAsync` | 异步卸载已加载场景 |
| `SetActiveScene` | 设置活动场景 |
| `IsSceneLoaded` | 查询场景是否已加载 |
| `GetActiveScene` | 获取当前活动场景 |

## 当前边界

- 不支持取消已经开始的 Unity 场景加载
- 不负责打开或关闭具体加载界面
- 不支持 Addressables 或远程 AB 场景
- 不静默修改 Build Settings
- 不自动清空全局 EventManager 监听
