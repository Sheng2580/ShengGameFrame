# UnityAgentBridge

[返回首页](../README.md)

UnityAgentBridge 在 Unity Editor 内启动本地 HTTP 服务，让命令行工具或 AI 在不手动点击编辑器的情况下检查编译、调用受控的静态方法、运行测试和执行构建

该能力只在 Editor 中可用

## 能解决什么

- 修改代码后主动刷新 Unity 并读取编译错误
- 获取场景、Prefab、UI 和运行时数据
- 自动进入或退出 Play Mode
- 获取 FPS、内存和对象数量
- 截取 Game View
- 运行 EditMode 测试
- 构建 AB 和完整包

Bridge 不会让 AI 自动理解整个 Unity 项目。真正可执行的能力来自框架提供的静态命令，未暴露的方法不能直接作为框架工作流使用

## 项目依赖

框架包声明：

```json
"com.unityagentbridge.server": "0.3.1"
```

Unity 项目的 `Packages/manifest.json` 使用固定上游提交：

```json
"com.unityagentbridge.server": "https://github.com/nitzanwilnai/unity-agent-bridge.git?path=/UnityPackage#0d7f6fb3249540c02f632f8d7cd343ac900b61e9"
```

固定提交可以避免上游分支变化导致项目突然失效

## CLI 安装

需要 Node.js，然后安装 CLI：

```bash
npm install --global unity-agent-cli@0.3.0
```

确认安装：

```bash
unity-agent-cli --help
```

## 连接检查

先用 Unity 打开目标项目并等待编译完成，然后执行：

```bash
unity-agent-cli --project <Unity项目路径> doctor
```

检查编译：

```bash
unity-agent-cli --project <Unity项目路径> check
```

`check` 会请求 Unity 刷新 AssetDatabase，并返回当前编译错误

## 调用框架命令

框架入口类：

```text
Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands
```

读取命令目录：

```bash
unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.GetCommandCatalog()"
```

项目验证：

```bash
unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.ValidateProject()"
```

## 命令目录

| 命令 | 作用 |
| --- | --- |
| `GetCommandCatalog` | 返回全部命令及描述 |
| `GetProjectSnapshot` | 项目、场景、平台和 AB 概况 |
| `ValidateProject` | 构建场景、Prefab 和丢失脚本检查 |
| `RunAllDiagnostics` | 项目验证和运行时快照 |
| `DumpSceneHierarchy` | 导出当前场景层级和组件 |
| `DumpUIHierarchy` | 导出当前场景 UI 布局参数 |
| `GetRuntimeSnapshot` | FPS、内存和对象概况 |
| `GetEditorState` | 编译和 Play Mode 状态 |
| `EnterPlayMode` | 请求进入 Play Mode |
| `ExitPlayMode` | 请求退出 Play Mode |
| `CaptureGameView` | 请求截取 Game View |
| `GetScreenshotStatus` | 查询截图结果 |
| `StartEditModeTests` | 异步启动全部 EditMode 测试 |
| `GetEditModeTestStatus` | 查询测试结果 |
| `StartEditorAssetBundleBuild` | 异步构建编辑器 AB |
| `StartAndroidAssetBundleBuild` | 异步构建 Android AB |
| `StartWindowsAssetBundleBuild` | 异步构建 Windows AB |
| `StartAndroidPackageBuild` | 异步构建 Android 完整包 |
| `StartWindowsPackageBuild` | 异步构建 Windows 完整包 |
| `GetTaskStatus` | 查询最近构建任务 |
| `ResetTaskStatus` | 重置已结束的构建任务状态 |

## 异步任务

测试、截图和构建不会在第一次调用时等待完成，需要启动后轮询对应状态方法

测试示例：

```bash
unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.StartEditModeTests()"

unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.GetEditModeTestStatus()"
```

构建示例：

```bash
unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.StartAndroidAssetBundleBuild()"

unity-agent-cli --project <Unity项目路径> \
  exec "Sheng.GameFramework.Editor.AgentBridge.FrameworkAgentCommands.GetTaskStatus()"
```

同一时间只允许一个框架构建任务运行

## 截图

先进入 Play Mode，再调用 `CaptureGameView`。截图输出到：

```text
AgentArtifacts/Screenshots/
```

该目录属于自动生成内容，不应提交 Git

## 连接和安全

- 服务只监听 `127.0.0.1:5142`
- 所有请求需要项目 `Library/UnityAgentBridge/token` 中的令牌
- CLI 会根据项目路径自动定位令牌
- 服务拒绝非回环 Host 和带浏览器来源头的请求
- `Library` 和令牌不能提交 Git

由于端口固定，同一台机器同一时间只能稳定连接一个启用 Bridge 的 Unity 项目。打开多个项目时，应关闭其他项目或避免让多个 Bridge 同时监听

## 常见问题

### doctor 无法连接

1. 确认 Unity 已打开正确项目
2. 等待 Package Manager 和脚本编译完成
3. 确认没有另一个 Unity 项目占用 `5142`
4. 使用绝对项目路径执行 `doctor`

### check 成功但业务行为不正确

`check` 只验证编译。需要继续运行测试、项目诊断和 Play Mode 验证

### 命令返回 Queued

这是正常的异步状态。继续调用对应的状态查询命令，直到得到 `Succeeded`、`Failed`、`Rejected` 或其他结束状态
