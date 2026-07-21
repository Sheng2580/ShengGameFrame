# 测试与文档维护

[返回首页](../README.md)

## 当前自动化测试

框架当前包含 92 项 EditMode 测试：

| 测试组 | 数量 | 覆盖内容 |
| --- | ---: | --- |
| `AssetManagerTests` | 10 | 简单加载、预加载、实例释放、失败回调、真实 AB、缓存策略和路径安全 |
| `ObjectPoolTests` | 10 | 预热、LIFO 复用、容量、归还检查、缩容、清理和租约 |
| `PoolManagerTests` | 13 | 预制体池、回调、容量、租用版本、删池、场景生命周期和 AssetManager 引用 |
| `StateMachineTests` | 4 | 生命周期、强制切换、延迟请求、释放 |
| `BehaviorTreeTests` | 4 | Sequence、优先级中断、Repeat、黑板类型保护 |
| `EventManagerTests` | 10 | GameEvent、0 到 5 参数、签名保护、移除和清理 |
| `AgentBridgeTests` | 4 | 命令目录、项目快照、验证报告、层级导出 |
| `JsonManagerTests` | 7 | 序列化、存档、备份、路径安全、损坏数据和 StreamingAssets |
| `LubanToolTests` | 11 | 类型扫描、Excel 同步、字段迁移、线程和路径安全、数据校验和真实 Luban 生成 |
| `FrameworkUpdateTests` | 4 | Git 地址识别、提交解析、依赖替换和本地包保护 |
| `TimerManagerTests` | 8 | 延迟、循环、自取消、暂停、时间类型、分组和追赶上限 |
| `SceneManagerTests` | 6 | 加载选项、队列、请求状态及真实 Additive 加载和卸载 |

## 在 Unity 中运行

打开：

```text
Window > General > Test Runner
```

选择 `EditMode`，点击 `Run All`

测试程序集位于：

```text
ShengGameFrame/Packages/com.sheng.game-framework/Tests/Editor
```

## 使用 UnityAgentBridge 运行

```bash
unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.StartEditModeTests()"
```

随后查询：

```bash
unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.GetEditModeTestStatus()"
```

详细连接配置见 [UnityAgentBridge](UnityAgentBridge.md)

## 修改模块后的最低验证

| 修改范围 | 最低验证 |
| --- | --- |
| Core | 编译、进入和退出 Play Mode、关闭 Domain Reload 后重复运行 |
| AssetManager | Editor 直读、真实 AB、引用计数、依赖释放、限流和卸载 |
| PoolManager | 泛型池、容量边界、重复归还、场景池、常驻池和资源引用释放 |
| UI | Resources 与 AB 面板、层级、遮罩、安全区、关闭缓存 |
| StateMachine | 全部状态机测试，新增切换规则对应测试 |
| BehaviorTree | 全部行为树测试，自定义节点的 Running 和 Abort 路径 |
| EventManager | 0 到 5 参数、签名冲突、移除监听和全局清理 |
| TimerManager | 延迟、有限与无限循环、暂停恢复、场景清理和回调内修改 |
| SceneManager | 请求顺序、进度、Additive、卸载、活动场景和生命周期事件 |
| JsonManager | 序列化、原子覆盖、备份、路径边界和目标平台 StreamingAssets |
| Luban Tool | 字段新增、改名、删除确认、真实 Luban 校验和 JSON 反序列化 |
| Framework Updater | 官方 Git 依赖识别、提交比较、manifest 定向修改和失败保护 |
| Build Pipeline | 目标平台 AB；发布前实际构建对应完整包 |
| AgentBridge | `doctor`、`check`、命令目录、测试状态轮询 |

编译通过不等于功能完成。涉及 Player、触摸输入、平台文件路径和完整包时，必须在真实目标平台验证

## 新增模块

新增 Runtime 模块时：

1. 在 `Runtime/<ModuleName>/` 中添加代码
2. 保持命名空间为 `Sheng.GameFramework.<ModuleName>`
3. 不引用 `UnityEditor`
4. 在 `Tests/Editor/` 增加核心行为测试
5. 在 `Docs/` 增加模块文档
6. 更新根 `README.md` 的功能表、架构图和文档导航
7. 更新包内 `README.md`

新增编辑器模块时，代码必须放入 `Editor/`，并确认不会进入 Runtime 程序集

## 文档同步矩阵

| 变化 | 需要更新 |
| --- | --- |
| 新增、重命名或删除公开类 | 对应模块文档和首页功能概览 |
| 方法签名变化 | API 表、调用示例和迁移说明 |
| 默认值变化 | 配置表和行为说明 |
| 菜单路径变化 | 构建文档或 Agent 文档 |
| 输出目录变化 | 构建文档和 `.gitignore` 检查 |
| 平台支持变化 | 首页、入门文档和模块限制 |
| UPM 依赖变化 | `package.json`、入门文档和包内说明 |
| Agent 命令变化 | 命令目录、示例和测试 |
| 事件签名、参数上限或清理规则变化 | `Docs/EventManager.md` 和事件测试 |
| JSON 存档行为变化 | `Docs/JsonManager.md` 和数据兼容说明 |
| 计时精度、重复规则、时间类型或生命周期变化 | `Docs/TimerManager.md` 和计时器测试 |
| 场景加载、队列、进度或生命周期变化 | `Docs/SceneManager.md` 和场景管理测试 |
| Luban 特性、目录或生成行为变化 | `Docs/Luban_Tool.md`、入门文档和依赖说明 |
| 更新检查地址、周期或安装行为变化 | `Docs/Framework_Updater.md`、入门文档和包内说明 |
| 新增未来规划 | 必须明确标记为尚未实现 |

## 文档完成标准

一次框架改动只有同时满足以下条件，才算文档同步完成：

1. 示例只调用真实存在的公开 API
2. Runtime、Editor-only 和平台限制标记清楚
3. 默认值与源码一致
4. 所有相对链接可从当前 Markdown 文件正确跳转
5. 根 README、模块文档和包内 README 没有互相矛盾
6. 没有把规划功能描述成已实现功能
7. 测试结果和已知未验证项如实记录

## 提交前检查

```bash
git diff --check
```

查看文档改动：

```bash
git diff -- README.md Docs ShengGameFrame/Packages/com.sheng.game-framework/README.md
```

检查文档中的框架类型引用：

```bash
rg "EventManager|AssetManager|UIManager|StateMachine|BehaviorTree|FrameworkAgentCommands" README.md Docs
```

这些文本检查不能替代 Unity 编译和测试

## 版本维护

对外发布包版本时更新：

```text
ShengGameFrame/Packages/com.sheng.game-framework/package.json
```

版本建议遵循语义化版本：

- PATCH：兼容的修复和文档修正
- MINOR：向后兼容的新功能
- MAJOR：存在破坏性 API 或行为变化

发布前应使用固定 Git 标签，让使用方可以锁定可复现版本
