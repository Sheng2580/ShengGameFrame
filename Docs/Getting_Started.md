# 安装与快速开始

[返回首页](../README.md)

## 环境要求

- Unity `2022.3` 或更高的兼容版本
- uGUI `1.0.0`
- Unity Test Framework `1.1.33`
- Newtonsoft Json `3.2.1`
- 需要构建 Android 或 Windows 时，Unity Hub 必须安装对应平台模块
- 使用 UnityAgentBridge CLI 时需要 Node.js
- 使用 Luban 编辑器工具时需要 .NET 8 或兼容的更高版本

框架的 Runtime 代码可以进入 Player。构建窗口、自动化命令和测试工具只在 Unity Editor 中编译

## 方式一：直接打开仓库

1. 克隆或下载仓库
2. 使用 Unity Hub 打开仓库中的 `ShengGameFrame/` 目录
3. 等待 Package Manager 完成依赖恢复
4. 确认 Console 没有编译错误
5. 运行 EditMode 测试

框架包位于：

```text
ShengGameFrame/Packages/com.sheng.game-framework
```

这是一个嵌入式 UPM 包，不需要复制到 `Assets`

## 方式二：通过本地磁盘接入其他项目

1. 打开目标项目的 `Window > Package Manager`
2. 点击左上角 `+`
3. 选择 `Add package from disk...`
4. 选择框架目录下的 `package.json`

```text
ShengGameFrame/Packages/com.sheng.game-framework/package.json
```

目标项目还需要在 `Packages/manifest.json` 中提供 UnityAgentBridge 的 Git 来源：

```json
{
  "dependencies": {
    "com.unityagentbridge.server": "https://github.com/nitzanwilnai/unity-agent-bridge.git?path=/UnityPackage#0d7f6fb3249540c02f632f8d7cd343ac900b61e9"
  }
}
```

不要覆盖目标项目已有的 `dependencies`，只增加缺少的条目

## 方式三：从 Git 仓库安装

仓库上传 GitHub 并创建稳定标签后，可以在目标项目的 `manifest.json` 中引用包目录：

```json
{
  "dependencies": {
    "com.sheng.game-framework": "https://github.com/<账号>/ShengGameFrame.git?path=/ShengGameFrame/Packages/com.sheng.game-framework#<版本标签>",
    "com.unityagentbridge.server": "https://github.com/nitzanwilnai/unity-agent-bridge.git?path=/UnityPackage#0d7f6fb3249540c02f632f8d7cd343ac900b61e9"
  }
}
```

`<账号>` 和 `<版本标签>` 必须替换为实际 GitHub 信息。正式项目建议引用固定标签或提交，不要长期跟随不稳定分支

## 程序集引用

如果业务代码没有自己的 asmdef，Unity 会自动引用框架 Runtime 程序集

如果业务代码使用 asmdef，请添加引用：

```text
Sheng.GameFramework.Runtime
```

仅编辑器工具需要引用：

```text
Sheng.GameFramework.Editor
```

业务 Runtime 程序集不能引用 Editor 程序集

## 首次验证

### 编译

打开项目并等待右下角编译结束，Console 不应出现错误

### 测试

打开：

```text
Window > General > Test Runner
```

选择 `EditMode`，点击 `Run All`

### 编辑器入口

框架加载成功后，Unity 顶部会出现：

```text
Sheng Game Framework
```

主要入口包括：

```text
Sheng Game Framework > Build > 多平台构建工具
Sheng Game Framework > AssetBundles > Open Asset Bundle Browser
Sheng Game Framework > Data > Luban 配置工具
```

## 运行时初始化

`AssetManager`、`PoolManager` 和 `UIManager` 都是跨场景单例。首次访问 `Instance` 时会查找已有对象，找不到则自动创建，因此基础用法不要求在启动场景预放管理器

```csharp
using Sheng.GameFramework.Assets;
using Sheng.GameFramework.Pooling;
using Sheng.GameFramework.UI;

AssetManager.Instance.InitializeAsync(success =>
{
    if (success)
    {
        PoolManager.Instance.InitializePool(
            PoolKey.FromName("Bullet"),
            bulletPrefab,
            initialCapacity: 16,
            maxCapacity: 64);
        UIManager.Instance.OpenAsync<HomePanel>();
    }
});
```

示例中的 `HomePanel` 需要按 [UI 模块](UI_System.md) 完成声明和资源配置

## 下一步

- 使用普通服务或 Unity 管理器：[单例模块](Singletons.md)
- 构建并加载资源：[AssetManager 模块](AssetManager.md)
- 初始化并复用对象：[PoolManager 模块](PoolManager.md)
- 打开第一个面板：[UI 模块](UI_System.md)
- 编写角色状态：[状态机模块](StateMachine.md)
- 编写 AI 决策：[行为树模块](BehaviorTree.md)
- 保存玩家数据和读取配置：[JsonManager](JsonManager.md)
- 从 C# 配置类生成 Excel 和 JSON：[Luban 配置工具](Luban_Tool.md)
