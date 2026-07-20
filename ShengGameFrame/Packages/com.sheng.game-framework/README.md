# Sheng Game Framework

版本：`0.1.0`

最低 Unity 版本：`2022.3`

这是 Sheng Unity 项目的本地 UPM 基础框架包

## Runtime 模块

| 命名空间 | 能力 |
| --- | --- |
| `Sheng.GameFramework.Core` | C#、场景和跨场景单例 |
| `Sheng.GameFramework.Assets` | Asset/Bundle 加载、引用计数、缓存和卸载 |
| `Sheng.GameFramework.Pooling` | 泛型对象池、GameObject 复用、容量和场景生命周期 |
| `Sheng.GameFramework.UI` | uGUI 分层、面板、模态和安全区 |
| `Sheng.GameFramework.StateMachine` | 泛型状态机和外部逻辑帧驱动 |
| `Sheng.GameFramework.BehaviorTree` | 行为树、构建器和类型安全黑板 |
| `Sheng.GameFramework.Json` | JSON 序列化、存档、备份和 StreamingAssets 读取 |
| `Sheng.GameFramework.Config` | Code First Luban 配置特性 |

## Editor 模块

- 当前编辑器、Android 和 Windows AssetBundle 构建
- Android APK/AAB 和 Windows EXE 完整包构建
- Asset Bundle Browser 自动安装入口
- UnityAgentBridge 项目诊断、测试、截图和构建命令
- C# 配置类生成或同步 Excel，并使用 Luban 生成 JSON

打开构建窗口：

```text
Sheng Game Framework > Build > 多平台构建工具
Sheng Game Framework > Data > Luban 配置工具
```

## 最小示例

异步加载 AB 资源：

```csharp
using Sheng.GameFramework.Assets;

AssetManager.Instance.LoadAssetAsync<UnityEngine.GameObject>(
    "characters",
    "Player",
    handle =>
    {
        if (handle != null)
        {
            try
            {
                UnityEngine.Object.Instantiate(handle.Asset);
            }
            finally
            {
                handle.Dispose();
            }
        }
    });
```

打开 UI 面板：

```csharp
using Sheng.GameFramework.UI;

UIManager.Instance.OpenAsync<HomePanel>();
```

初始化并使用对象池：

```csharp
using Sheng.GameFramework.Pooling;

PoolKey bulletPool = PoolKey.FromName("Bullet");
PoolManager.Instance.InitializePool(bulletPool, bulletPrefab, 16, 64);

PooledHandle bullet = PoolManager.Instance.Rent(bulletPool);
bullet?.Dispose();
```

状态机和行为树都由调用方显式传入时间：

```csharp
stateMachine.Tick(deltaTime);
behaviorTree.Tick(deltaTime);
```

这为固定逻辑帧预留了接口，但包内尚未实现完整帧同步、定点数、回滚或网络层

保存玩家数据：

```csharp
using Sheng.GameFramework.Json;

JsonManager.Instance.SaveData(saveData, "slot_01", "Saves");
PlayerSaveData loaded = JsonManager.Instance.LoadData<PlayerSaveData>(
    "slot_01",
    "Saves");
```

## 依赖

- `com.unity.ugui` `1.0.0`
- `com.unity.nuget.newtonsoft-json` `3.2.1`
- `com.unity.test-framework` `1.1.33`
- `com.unityagentbridge.server` `0.3.1`

通过本地磁盘或 Git 接入其他项目时，目标项目需要为 `com.unityagentbridge.server` 配置可解析的 Git 来源

## 完整文档

使用源码仓库时，从仓库根目录 `README.md` 进入分模块中文文档
