# Sheng Game Framework 开发约定

## 适用范围

本文件适用于整个仓库

## 代码约定

- 框架代码放在 `ShengGameFrame/Packages/com.sheng.game-framework`
- Runtime 代码不得引用 `UnityEditor`
- Editor 代码放在 `Editor` 程序集中
- 代码注释使用中文，注释结尾不加句号
- 不修改业务验证脚本 `ShengGameFrame/Assets/Script/test.cs`，除非任务明确要求
- 不提交 `Library`、`Temp`、`Logs`、`Builds`、生成的 AB 或 `AgentArtifacts`

## 文档同步要求

每次修改框架时必须检查文档影响

| 代码变化 | 必须同步的文档 |
| --- | --- |
| 新增或删除模块 | 根 `README.md`、新增或删除对应 `Docs` 文档、包内 `README.md` |
| 修改公开 API | 对应模块文档中的 API 和示例 |
| 修改资源加载和卸载 | `Docs/AssetManager.md` 及相关调用示例 |
| 修改对象池容量、生命周期或回收行为 | `Docs/PoolManager.md` 及相关调用示例 |
| 修改默认值或运行行为 | 对应模块文档中的规则、限制和注意事项 |
| 修改菜单或构建输出 | `Docs/Build_Pipeline.md` |
| 修改 Agent 命令 | `Docs/UnityAgentBridge.md` |
| 修改事件签名、参数上限或清理行为 | `Docs/EventManager.md` |
| 修改 JSON 存档、备份或读取规则 | `Docs/JsonManager.md` |
| 修改 Luban 特性、目录、同步或生成规则 | `Docs/Luban_Tool.md` |
| 修改依赖或最低 Unity 版本 | 根 `README.md`、`Docs/Getting_Started.md`、`package.json` |
| 新增测试或改变验证方式 | `Docs/Testing_And_Maintenance.md` |

不要在文档中描述尚未实现的功能。规划中的能力必须明确标记为未实现

## 完成前验证

1. 执行 Unity 编译检查
2. 运行全部 EditMode 测试
3. 检查 Markdown 相对链接
4. 检查文档中的类名、方法名和菜单路径是否仍存在
5. 执行 Git 空白错误检查
