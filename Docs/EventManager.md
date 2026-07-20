# EventManager

[返回首页](../README.md)

`EventManager` 是同步、类型安全的全局事件管理器，支持无参数到 5 个参数，命名空间为 `Sheng.GameFramework.Events`

它适合在互不直接引用的系统之间传递状态变化，例如玩家死亡、武器切换、UI 刷新和关卡流程通知

## 模块组成

| 类型 | 用途 |
| --- | --- |
| `GameEvent` | 稳定且可扩展的事件标识 |
| `EventManager` | 注册、移除、触发和清理事件 |

`EventManager` 继承普通 C# `Singleton<T>`，不需要挂到 GameObject，也不会进入场景生命周期

## 声明项目事件

框架不会内置具体项目的业务事件。每个项目在自己的业务代码中集中声明：

```csharp
using Sheng.GameFramework.Events;

public static class GameEvents
{
    public static readonly GameEvent PlayerDied =
        new GameEvent("Player.Died");

    public static readonly GameEvent HealthChanged =
        new GameEvent("Player.HealthChanged");

    public static readonly GameEvent DamageResolved =
        new GameEvent("Combat.DamageResolved");
}
```

这种方式比把所有事件写进框架枚举更适合复用：新项目可以增加自己的事件，不需要修改 UPM 包

`GameEvent` 使用区分大小写的字符串作为身份，同名事件相等，名称两端空白会自动移除

## 无参数事件

```csharp
using System;
using Sheng.GameFramework.Events;

private void OnEnable()
{
    EventManager.Instance.AddEventListener(
        GameEvents.PlayerDied,
        OnPlayerDied);
}

private void OnDisable()
{
    EventManager.Instance.RemoveEventListener(
        GameEvents.PlayerDied,
        OnPlayerDied);
}

private void OnPlayerDied()
{
}
```

触发：

```csharp
EventManager.Instance.EventTrigger(GameEvents.PlayerDied);
```

## 一个参数

```csharp
private void OnHealthChanged(float currentHealth)
{
}

EventManager.Instance.AddEventListener<float>(
    GameEvents.HealthChanged,
    OnHealthChanged);

EventManager.Instance.EventTrigger(
    GameEvents.HealthChanged,
    75f);
```

## 两到五个参数

```csharp
EventManager.Instance.AddEventListener<int, float>(
    GameEvents.DamageResolved,
    OnDamageResolved);

private void OnDamageResolved(int targetId, float damage)
{
}
```

最多 5 个参数：

```csharp
EventManager.Instance.AddEventListener<int, int, float, bool, string>(
    GameEvents.DamageResolved,
    OnDetailedDamageResolved);

private void OnDetailedDamageResolved(
    int attackerId,
    int targetId,
    float damage,
    bool critical,
    string damageType)
{
}

EventManager.Instance.EventTrigger(
    GameEvents.DamageResolved,
    attackerId,
    targetId,
    damage,
    critical,
    damageType);
```

虽然框架支持 5 个参数，业务数据较多或可能继续扩展时，建议使用一个只读事件数据结构作为单参数

```csharp
public readonly struct DamageResolvedEventData
{
    public readonly int AttackerId;
    public readonly int TargetId;
    public readonly float Damage;

    public DamageResolvedEventData(
        int attackerId,
        int targetId,
        float damage)
    {
        AttackerId = attackerId;
        TargetId = targetId;
        Damage = damage;
    }
}
```

这样后续增加字段时，不需要修改每个监听方法的参数列表

## 签名保护

同一个 `GameEvent` 在拥有监听时只能使用一种参数签名

```csharp
EventManager.Instance.AddEventListener<int>(
    GameEvents.HealthChanged,
    OnHealthIntChanged);

EventManager.Instance.AddEventListener<float>(
    GameEvents.HealthChanged,
    OnHealthFloatChanged);
```

第二次注册会抛出 `InvalidOperationException`，已经存在的监听不会被删除或替换

下面这些差异都属于签名不一致：

- 参数数量不同
- 参数顺序不同
- 参数类型不同
- 无参数监听与有参数监听混用

触发时使用错误签名也会立即抛出异常，方便在开发阶段定位调用错误

## 返回值

`EventTrigger` 返回是否找到并调用了监听：

```csharp
bool triggered = EventManager.Instance.EventTrigger(
    GameEvents.PlayerDied);
```

`RemoveEventListener` 返回是否实际移除了对应委托：

```csharp
bool removed = EventManager.Instance.RemoveEventListener(
    GameEvents.PlayerDied,
    OnPlayerDied);
```

移除最后一个监听后，对应事件槽会自动删除

## 查询和清理

```csharp
bool hasListeners = EventManager.Instance.HasListeners(
    GameEvents.PlayerDied);

int listenerCount = EventManager.Instance.GetListenerCount(
    GameEvents.PlayerDied);

int eventCount = EventManager.Instance.EventCount;

EventManager.Instance.ClearEvent(GameEvents.PlayerDied);
EventManager.Instance.Clear();
```

`ClearEvent` 只清理一个事件，`Clear` 清理全部事件

建议在一次游戏流程结束、退出账号或重置框架上下文时调用 `Clear`，不要在普通场景切换时无条件清空全局事件

## 调用规则

- 事件同步执行，监听运行在触发者所在的线程
- 监听按注册顺序执行
- 重复注册同一委托会重复调用
- 回调抛出的异常会继续向触发者传播
- 触发期间移除监听是安全的，当前触发使用开始时的委托快照
- 字典操作有锁保护，但 Unity 对象相关回调仍应在主线程触发
- `EventManager` 不使用弱引用，组件应在 `OnDisable` 或 `OnDestroy` 主动移除监听

## 旧 EventCenter 迁移

旧项目：

```csharp
EventCenter.Instance.AddEventListener(
    GameEvent.PlayerDied,
    OnPlayerDied);
```

新框架：

```csharp
EventManager.Instance.AddEventListener(
    GameEvents.PlayerDied,
    OnPlayerDied);
```

迁移变化：

- `EventCenter` 改名为 `EventManager`
- `GameEvent` 从业务枚举改为可扩展标识结构
- `UnityAction` 改为标准 `System.Action`
- 方法名 `AddEventListener`、`RemoveEventListener`、`EventTrigger` 保持不变
- 参数上限从 1 个提高到 5 个
- 签名错误不再清空旧监听，而是抛出异常

已有枚举可以临时转换：

```csharp
GameEvent eventId = GameEvent.From(OldGameEvent.PlayerDied);
```

正式迁移后建议统一改为项目自己的 `GameEvents` 静态定义

## 主要 API

| API | 用途 |
| --- | --- |
| `AddEventListener` | 注册 0 到 5 参数监听 |
| `RemoveEventListener` | 移除指定监听 |
| `EventTrigger` | 同步触发事件 |
| `HasListeners` | 判断事件是否存在监听 |
| `GetListenerCount` | 获取事件监听数量 |
| `ClearEvent` | 清理指定事件 |
| `Clear` | 清理全部事件 |
| `EventCount` | 获取当前事件槽数量 |
