# JsonManager

[返回首页](../README.md)

`JsonManager` 负责运行时 JSON 序列化、存档读写和配置读取，命名空间为 `Sheng.GameFramework.Json`

它使用 `Newtonsoft.Json`，可以处理字典、字符串枚举、中文文本、字段重命名和泛型集合

## 职责边界

| 数据 | 目录 | JsonManager 的行为 |
| --- | --- | --- |
| 玩家存档和本地设置 | `Application.persistentDataPath` | 可读、可写、可删除、可备份 |
| Luban 配置 | `Assets/StreamingAssets/Config/Luban` | Player 中只读 |
| Excel 和 Luban 工具 | `Config/Luban` 与 `Library` | 不由 JsonManager 管理 |

`JsonManager` 不负责定义配置表结构，也不会在运行时启动 Luban

## 获取实例

`JsonManager` 是跨场景单例，首次访问时自动创建

```csharp
using Sheng.GameFramework.Json;

JsonManager json = JsonManager.Instance;
```

## 保存和读取

```csharp
using System.Collections.Generic;
using Sheng.GameFramework.Json;

public sealed class PlayerSaveData
{
    public string PlayerName;
    public int Level;
    public Dictionary<string, int> Inventory;
}

PlayerSaveData save = new PlayerSaveData
{
    PlayerName = "Player01",
    Level = 8,
    Inventory = new Dictionary<string, int>
    {
        { "Coin", 120 }
    }
};

JsonManager.Instance.SaveData(save, "slot_01", "Saves");

PlayerSaveData loaded = JsonManager.Instance.LoadData<PlayerSaveData>(
    "slot_01",
    "Saves");
```

实际文件为：

```text
Application.persistentDataPath/Saves/slot_01.json
```

文件名不写 `.json` 时会自动补扩展名

## 详细结果

需要区分文件不存在、路径非法和 JSON 损坏时，使用详细结果 API

```csharp
JsonReadResult<PlayerSaveData> result =
    JsonManager.Instance.LoadDataDetailed<PlayerSaveData>(
        "slot_01",
        "Saves");

if (!result.Success)
{
    UnityEngine.Debug.LogError(
        $"读取失败 {result.ErrorCode} {result.ErrorMessage}");
}
```

`JsonErrorCode` 包含：

- `InvalidPath`
- `NotFound`
- `SerializeFailed`
- `DeserializeFailed`
- `ReadFailed`
- `WriteFailed`
- `DeleteFailed`
- `Cancelled`

## 异步读写

```csharp
JsonWriteResult write = await JsonManager.Instance.SaveDataAsync(
    save,
    "slot_01",
    "Saves",
    cancellationToken);

JsonReadResult<PlayerSaveData> read =
    await JsonManager.Instance.LoadDataAsync<PlayerSaveData>(
        "slot_01",
        "Saves",
        cancellationToken);
```

文件读写在后台任务中执行，调用方仍应避免同时高频覆盖同一存档

## 原子覆盖和备份

默认配置：

```csharp
JsonManager.Instance.PrettyPrint = true;
JsonManager.Instance.KeepBackup = true;
```

保存时先写临时文件，再替换正式文件。正式文件已经存在且开启备份时，旧版本保存为同目录下的 `.bak` 文件

关闭备份：

```csharp
JsonManager.Instance.KeepBackup = false;
```

## 删除和列出存档

```csharp
bool exists = JsonManager.Instance.Exists("slot_01", "Saves");
IReadOnlyList<string> slots =
    JsonManager.Instance.GetAllDataNames("Saves");
bool deleted = JsonManager.Instance.DeleteData("slot_01", "Saves");
```

`GetAllDataNames` 只扫描指定目录的第一层 JSON 文件，并按名称排序

## 读取 Luban 配置

Luban 默认把表输出为 JSON 数组

```csharp
using System.Collections.Generic;
using Sheng.GameFramework.Json;

JsonReadResult<List<WeaponConfig>> result =
    await JsonManager.Instance.LoadLubanTableAsync<WeaponConfig>(
        "tbweapon");

if (result.Success)
{
    List<WeaponConfig> weapons = result.Value;
}
```

默认读取：

```text
Assets/StreamingAssets/Config/Luban/tbweapon.json
```

Android 和 WebGL 会自动使用 `UnityWebRequest` 读取 `StreamingAssets`，桌面平台使用本地文件读取

## Luban 字段特性

运行时序列化会识别：

```csharp
[LubanColumn("display_name")]
public string DisplayName;

[LubanIgnore]
public string RuntimeCache;
```

JSON 中字段名为 `display_name`，`RuntimeCache` 不会写入或读取

## 路径安全

- 存档 API 只接受相对路径
- 路径不能逃出 `persistentDataPath`
- 配置读取不能逃出所选数据根目录
- 不接受绝对文件路径

这些限制用于防止业务传入的文件名覆盖项目或系统中的其他文件

## 主要 API

| API | 用途 |
| --- | --- |
| `SerializeData` | 对象转 JSON 字符串 |
| `TryDeserializeData` | JSON 字符串转对象 |
| `SaveData` | 保存并返回是否成功 |
| `SaveDataDetailed` | 保存并返回详细错误 |
| `SaveDataAsync` | 异步保存 |
| `LoadData` | 读取对象，失败返回默认值 |
| `TryLoadData` | 尝试读取对象 |
| `LoadDataDetailed` | 读取并返回详细错误 |
| `LoadDataAsync` | 异步读取存档 |
| `LoadStreamingDataAsync` | 异步读取 StreamingAssets JSON |
| `LoadLubanTableAsync` | 读取 Luban JSON 数组 |
| `Exists` | 判断存档是否存在 |
| `DeleteData` | 删除正式文件和备份 |
| `GetAllDataNames` | 获取目录中的存档名 |
