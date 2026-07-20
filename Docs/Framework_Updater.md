# 框架更新器

[返回首页](../README.md)

命名空间：

```csharp
using Sheng.GameFramework.Editor.Updater;
```

更新器只在 Unity Editor 中运行，不会进入 Player

## 支持范围

更新器只处理通过官方 Git 地址安装的框架：

```text
https://github.com/Sheng2580/ShengGameFrame.git?path=/ShengGameFrame/Packages/com.sheng.game-framework#<提交号>
```

以下安装方式不会被自动覆盖：

- 仓库内的嵌入式开发包
- `file:` 本地磁盘包
- 其他账号的 Fork
- 手动复制到 `Assets` 的源码

这样可以避免更新器破坏本地开发内容或第三方修改

## 自动检查

框架默认每天通过公开 Atom 提交订阅源检查 GitHub `main` 最新提交

订阅源不使用 GitHub REST API，因此不会消耗匿名 API 每小时 60 次的调用额度

发现更新后会显示：

```text
当前提交 a534d09
最新提交 1234567
```

只有点击“立即更新”后才会修改项目，不会静默安装新代码

自动检查开关：

```text
Sheng Game Framework > Framework > 每天自动检查
```

关闭后仍可使用手动检查

## 手动检查

打开：

```text
Sheng Game Framework > Framework > 检查更新
```

更新器会执行以下流程：

1. 读取 `Packages/manifest.json`
2. 确认框架来自官方 Git 仓库
3. 从 `packages-lock.json` 读取当前实际提交
4. 请求 GitHub `main` 的公开 Atom 提交订阅源
5. 显示当前和最新提交供用户确认
6. 只替换 `com.sheng.game-framework` 对应的提交号
7. 调用 Package Manager 重新解析依赖

更新后 Unity 会重新下载包并触发脚本编译

## 第一次启用

旧版本框架本身没有更新器，因此需要手动更新到 `0.1.1` 或更高版本一次。完成这次更新后，后续版本才会自动检查

第一次仍然通过 `Packages/manifest.json` 修改提交号，或者在 Package Manager 中移除旧包后使用最新 Git 地址重新安装

## 网络与失败处理

- GitHub 请求超时为 15 秒
- 更新检查不依赖 GitHub REST API 匿名调用额度
- 自动检查失败只写入 Console 警告，不阻塞编辑器启动
- 手动检查失败会显示原因
- GitHub 返回非法提交号时不会修改项目
- `manifest.json` 不存在、格式损坏或依赖来源不受支持时不会修改项目

更新完成后应等待 Console 编译结束，再运行 EditMode 测试确认项目兼容性
