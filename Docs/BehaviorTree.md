# 行为树模块

[返回首页](../README.md)

命名空间：

```csharp
using Sheng.GameFramework.BehaviorTree;
```

核心类型：`BehaviorTree<TContext>`、`BehaviorTreeBuilder<TContext>`、`BehaviorNode<TContext>`、`Blackboard`

## 职责边界

行为树负责决策，不直接负责移动、动画或物理。叶节点通过 Context 调用业务对象，或者把结果写入黑板，最终执行仍由业务代码或状态机完成

当前框架没有内置 `EnemyAIScheduler`。如果需要根据距离降低远处敌人的思考频率，应由业务层调度各棵行为树的 `Tick`

## 节点状态

| 状态 | 含义 |
| --- | --- |
| `Invalid` | 尚未执行或已重置 |
| `Running` | 当前行为未完成，下次继续执行 |
| `Success` | 节点成功完成 |
| `Failure` | 节点执行失败 |
| `Aborted` | Running 节点被父节点或外部中断 |

## 创建行为树

```csharp
using Sheng.GameFramework.BehaviorTree;

public sealed class EnemyContext
{
    public bool IsDead;
    public bool HasTarget;
    public bool InAttackRange;

    public NodeStatus Die()
    {
        return NodeStatus.Success;
    }

    public NodeStatus Attack(float deltaTime)
    {
        return NodeStatus.Running;
    }

    public NodeStatus Chase(float deltaTime)
    {
        return HasTarget ? NodeStatus.Running : NodeStatus.Failure;
    }
}

EnemyContext context = new EnemyContext();

BehaviorTree<EnemyContext> tree = BehaviorTreeBuilder<EnemyContext>
    .Create()
    .PrioritySelector()
        .Sequence()
            .Condition(value => value.IsDead)
            .Action(value => value.Die())
        .End()
        .Sequence()
            .Condition(value => value.HasTarget && value.InAttackRange)
            .Action((value, deltaTime) => value.Attack(deltaTime))
        .End()
        .Action((value, deltaTime) => value.Chase(deltaTime))
    .End()
    .Build(context);
```

每创建一个组合节点或装饰节点，都必须使用 `End()` 回到父节点。存在未闭合父节点时，`Build` 会抛出异常

## 驱动行为树

```csharp
private void Update()
{
    tree.Tick(Time.deltaTime);
}
```

也可以由外部 AI 调度器按指定间隔驱动：

```csharp
if (Time.time >= nextThinkTime)
{
    float thinkDeltaTime = Time.time - lastThinkTime;
    tree.Tick(thinkDeltaTime);
    lastThinkTime = Time.time;
    nextThinkTime = Time.time + thinkInterval;
}
```

调度器只是决定何时调用 `Tick`。行为树自己不会上报下次执行时间，也不会持续运行 Unity Update

## 组合节点

| 节点 | 规则 |
| --- | --- |
| `SequenceNode` | 从前向后执行，遇到 Failure 停止，全部 Success 才成功 |
| `SelectorNode` | 从前向后选择，遇到 Success 停止，全部 Failure 才失败 |
| `PrioritySelectorNode` | 每次从最高优先级重新判断，可中断正在运行的低优先级分支 |
| `ParallelNode` | 同时推进全部子节点，按成功和失败策略汇总 |

`PrioritySelectorNode` 适合死亡、受击、攻击、追击、待机这种随时可能被高优先级事件打断的决策

并行节点策略：

```csharp
.Parallel(
    ParallelPolicy.RequireAll,
    ParallelPolicy.RequireOne)
```

上例表示全部子节点成功才成功，任意一个子节点失败就失败

## 装饰节点

| 节点 | 规则 |
| --- | --- |
| `InverterNode` | Success 和 Failure 互换，Running 不变 |
| `RepeatNode` | 重复执行子节点，`0` 次直接成功，负数表示无限重复 |

装饰节点只能拥有一个子节点

## 叶节点

条件节点：

```csharp
new ConditionNode<EnemyContext>(value => value.HasTarget);
```

动作节点：

```csharp
new ActionNode<EnemyContext>((value, deltaTime) =>
{
    return value.Chase(deltaTime);
});
```

复杂业务可以继承 `BehaviorNode<TContext>` 创建自定义节点，并实现 `OnEnter`、`OnTick`、`OnExit` 和 `OnReset`

## 黑板

黑板使用带泛型的键保护值类型：

```csharp
using UnityEngine;

public static class EnemyBlackboardKeys
{
    public static readonly BlackboardKey<Transform> Target =
        new BlackboardKey<Transform>("Target");

    public static readonly BlackboardKey<float> Distance =
        new BlackboardKey<float>("Distance");
}
```

读写：

```csharp
Blackboard blackboard = new Blackboard();

blackboard.Set(EnemyBlackboardKeys.Target, playerTransform);
blackboard.Set(EnemyBlackboardKeys.Distance, 8.5f);

float distance = blackboard.Get(EnemyBlackboardKeys.Distance);
Transform target = blackboard.GetOrDefault(EnemyBlackboardKeys.Target);
```

同一个字符串名称第一次写入后会绑定值类型。使用相同名称但不同泛型类型再次写入会抛出 `InvalidOperationException`

常用 API：`Set`、`TryGet`、`Get`、`GetOrDefault`、`Contains`、`Remove`、`Clear`

## 中止和重置

```csharp
tree.Abort();
tree.Reset();
```

- `Abort` 只中止当前 Running 根节点，并把状态设为 `Aborted`
- `Reset` 会先中止 Running 节点，再递归恢复为 `Invalid`，同时清零 `TickCount`

被父节点中止的自定义节点应在 `OnExit` 中检查 `NodeStatus.Aborted`，停止移动、动画或异步行为

## 与状态机配合

推荐分工：

```text
行为树：判断现在应该攻击、追击还是待机
黑板：保存目标、距离、视野结果和临时决策数据
状态机：执行移动、攻击、受击、死亡等互斥动作
```

行为树叶节点可以调用状态机的 `RequestState`，状态机在自己的逻辑帧中安全切换

## 当前限制

- 没有可视化编辑器和 ScriptableObject 树资源
- 没有内置 AI 距离分层调度器
- 没有异步任务句柄、超时节点和冷却节点
- 没有行为树运行快照和网络同步
