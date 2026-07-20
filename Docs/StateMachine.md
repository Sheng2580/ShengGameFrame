# 状态机模块

[返回首页](../README.md)

命名空间：

```csharp
using Sheng.GameFramework.StateMachine;
```

核心类型：`State<TOwner, TStateId>`、`StateMachine<TOwner, TStateId>`

## 适用场景

状态机适合表达同一时刻只有一个主状态的对象，例如：

- 玩家待机、移动、跳跃、受击、死亡
- 武器待机、射击、换弹、切枪
- 敌人的最终动作执行层

状态编号必须是枚举。Owner 可以是 MonoBehaviour，也可以是普通 C# 对象

## 完整示例

```csharp
using Sheng.GameFramework.StateMachine;
using UnityEngine;

public enum PlayerStateId
{
    Idle,
    Move
}

public sealed class PlayerMotor : MonoBehaviour
{
    private StateMachine<PlayerMotor, PlayerStateId> _machine;

    public Vector2 MoveInput { get; set; }

    private void Awake()
    {
        _machine = new StateMachine<PlayerMotor, PlayerStateId>(this);
        _machine.Register(new PlayerIdleState());
        _machine.Register(new PlayerMoveState());
        _machine.Start(PlayerStateId.Idle);
    }

    private void Update()
    {
        _machine.Tick(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        _machine.FixedTick(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        _machine.LateTick(Time.deltaTime);
    }

    public void Move(float deltaTime)
    {
        Vector3 direction = new Vector3(MoveInput.x, 0f, MoveInput.y);
        transform.position += direction * (4f * deltaTime);
    }

    private void OnDestroy()
    {
        _machine?.Clear();
    }
}

public sealed class PlayerIdleState : State<PlayerMotor, PlayerStateId>
{
    public override PlayerStateId Id => PlayerStateId.Idle;

    protected override void OnTick(float deltaTime)
    {
        if (Owner.MoveInput.sqrMagnitude > 0.01f)
        {
            Machine.RequestState(PlayerStateId.Move);
        }
    }
}

public sealed class PlayerMoveState : State<PlayerMotor, PlayerStateId>
{
    public override PlayerStateId Id => PlayerStateId.Move;

    protected override void OnTick(float deltaTime)
    {
        if (Owner.MoveInput.sqrMagnitude <= 0.01f)
        {
            Machine.RequestState(PlayerStateId.Idle);
            return;
        }

        Owner.Move(deltaTime);
    }
}
```

## 状态生命周期

```text
Register
  -> OnInitialize

Start 或 ChangeState
  -> 旧状态 OnExit
  -> 新状态 OnEnter

Tick
  -> OnTick

Clear
  -> 当前状态 OnExit
  -> 全部状态 OnDispose
```

`OnInitialize` 在注册时调用一次。`OnEnter` 和 `OnExit` 可以多次调用

## 切换方式

立即尝试切换：

```csharp
bool changed = machine.ChangeState(PlayerStateId.Move);
```

延迟到安全位置切换：

```csharp
machine.RequestState(PlayerStateId.Move);
```

`Tick`、`FixedTick` 和 `LateTick` 会在执行当前状态前后应用待切换请求。在状态回调内部优先使用 `RequestState`，可以避免回调执行到一半时立刻退出和进入另一个状态

如果同一帧连续请求多个状态，只保留最后一次请求

## 进入和退出守卫

```csharp
public override bool CanExitTo(PlayerStateId nextStateId)
{
    return !isLocked;
}

public override bool CanEnterFrom(PlayerStateId? previousStateId)
{
    return previousStateId != PlayerStateId.Dead;
}
```

强制切换会跳过重复状态检查和两个守卫：

```csharp
machine.ChangeState(PlayerStateId.Dead, force: true);
```

强制切换仍会执行旧状态 `OnExit` 和新状态 `OnEnter`

## 监听状态变化

```csharp
machine.StateChanged += (previous, current) =>
{
    Debug.Log($"{previous} -> {current}");
};
```

事件在切换完成后触发

## 外部逻辑帧

状态机不直接读取 `Time.deltaTime`，时间由调用者传入。这使同一状态代码可以由 Unity Update、固定逻辑帧或测试驱动

```csharp
machine.Tick(logicFrameDeltaTime);
```

这只是帧同步友好的接口设计。当前框架尚未实现定点数、确定性物理、输入同步、回滚和网络传输，不能仅靠传入固定时间就实现真正帧同步

## 其他 API

| API | 说明 |
| --- | --- |
| `Register` | 注册并初始化状态 |
| `Unregister` | 注销非当前状态 |
| `Contains` | 判断是否注册 |
| `TryGetState` | 获取状态实例 |
| `Start` | 在没有当前状态时启动 |
| `Stop` | 停止当前状态 |
| `Clear` | 停止并释放全部状态 |

## 当前限制

- 单状态机同一时刻只有一个当前状态
- 不包含层级状态机、并行状态区和状态历史
- 不自动绑定 Animator，也不负责移动、物理或输入
- 不提供运行时序列化和网络状态快照
