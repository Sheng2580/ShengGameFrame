# 单例模块

[返回首页](../README.md)

命名空间：

```csharp
using Sheng.GameFramework.Core;
```

## 选择方式

| 类型 | 是否为 MonoBehaviour | 是否跨场景 | 适用场景 |
| --- | --- | --- | --- |
| `Singleton<T>` | 否 | 静态实例 | 纯 C# 配置、规则和服务 |
| `MonoSingleton<T>` | 是 | 否 | 当前场景管理器 |
| `PersistentMonoSingleton<T>` | 是 | 是 | UI、资源、音频等全局管理器 |

## 普通 C# 单例

```csharp
using Sheng.GameFramework.Core;

public sealed class GameSettings : Singleton<GameSettings>
{
    public float MasterVolume { get; set; } = 1f;
}
```

使用：

```csharp
GameSettings.Instance.MasterVolume = 0.8f;
```

`Singleton<T>` 使用线程安全的延迟初始化。类型必须是引用类型，并且拥有公开无参构造函数

## 场景级 Mono 单例

```csharp
using Sheng.GameFramework.Core;

public sealed class BattleManager : MonoSingleton<BattleManager>
{
    protected override void OnSingletonAwake()
    {
    }

    protected override void OnSingletonDestroyed()
    {
    }
}
```

首次访问 `BattleManager.Instance` 时，框架按以下顺序处理：

1. 返回已经缓存的实例
2. 在场景中查找激活或未激活的实例
3. 找不到时创建名为 `[BattleManager]` 的 GameObject

同一类型出现重复实例时，后创建或后唤醒的对象会被销毁

## 跨场景 Mono 单例

```csharp
using Sheng.GameFramework.Core;

public sealed class AudioManager : PersistentMonoSingleton<AudioManager>
{
    protected override void OnSingletonAwake()
    {
    }
}
```

`PersistentMonoSingleton<T>` 会自动脱离父节点并调用 `DontDestroyOnLoad`，适合真正需要跨场景保留的服务

## 生命周期规则

- 子类不要自行维护第二份静态 `Instance`
- 初始化逻辑写入 `OnSingletonAwake`
- 清理事件订阅写入 `OnSingletonDestroyed`
- 不要依赖 Mono 单例在工作线程中创建，Unity 对象必须在主线程访问
- 应用退出后 `Instance` 返回 `null`，避免退出阶段再次创建对象

## 关闭 Domain Reload

框架通过 `RuntimeInitializeLoadType.SubsystemRegistration` 清理静态引用。即使 Editor 关闭了 Enter Play Mode 的 Domain Reload，下一次运行也不会继续持有上一次 Play Mode 的单例引用

这只负责重置单例静态字段，不会自动清理业务层自行声明的其他静态数据
