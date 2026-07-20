using System;
using UnityEditor;

namespace Sheng.GameFramework.Editor.AgentBridge
{
    /// <summary>
    /// UnityAgentBridge 可直接调用的框架命令入口
    /// </summary>
    public static class FrameworkAgentCommands
    {
        public static string GetCommandCatalog()
        {
            AgentCommandCatalog catalog = new AgentCommandCatalog();
            AddCommand(catalog, nameof(GetProjectSnapshot), "读取项目、场景、平台和 AB 概况");
            AddCommand(catalog, nameof(ValidateProject), "检查构建场景、Prefab 和丢失脚本");
            AddCommand(catalog, nameof(RunAllDiagnostics), "运行项目验证并读取运行时快照");
            AddCommand(catalog, nameof(DumpSceneHierarchy), "导出当前场景层级和组件");
            AddCommand(catalog, nameof(DumpUIHierarchy), "导出当前场景 UI 布局参数");
            AddCommand(catalog, nameof(GetRuntimeSnapshot), "读取 Play Mode 性能与对象概况");
            AddCommand(catalog, nameof(GetEditorState), "读取编辑器状态");
            AddCommand(catalog, nameof(EnterPlayMode), "请求进入 Play Mode");
            AddCommand(catalog, nameof(ExitPlayMode), "请求退出 Play Mode");
            AddCommand(catalog, nameof(CaptureGameView), "在 Play Mode 截取 Game View");
            AddCommand(catalog, nameof(GetScreenshotStatus), "读取截图任务状态");
            AddCommand(catalog, nameof(StartEditModeTests), "启动全部 EditMode 测试");
            AddCommand(catalog, nameof(GetEditModeTestStatus), "读取 EditMode 测试结果");
            AddCommand(catalog, nameof(StartEditorAssetBundleBuild), "启动编辑器 AB 构建");
            AddCommand(catalog, nameof(StartAndroidAssetBundleBuild), "启动 Android AB 构建");
            AddCommand(catalog, nameof(StartWindowsAssetBundleBuild), "启动 Windows AB 构建");
            AddCommand(catalog, nameof(StartAndroidPackageBuild), "启动 Android 完整包构建");
            AddCommand(catalog, nameof(StartWindowsPackageBuild), "启动 Windows 完整包构建");
            AddCommand(catalog, nameof(GetTaskStatus), "读取最近的构建任务状态");
            AddCommand(catalog, nameof(ResetTaskStatus), "重置已结束的构建任务状态");
            return FrameworkAgentJson.Serialize(catalog);
        }

        public static string GetProjectSnapshot()
        {
            return FrameworkAgentJson.Serialize(
                FrameworkAgentDiagnostics.CreateProjectSnapshot());
        }

        public static string ValidateProject()
        {
            return FrameworkAgentJson.Serialize(
                FrameworkAgentDiagnostics.ValidateProject());
        }

        public static string RunAllDiagnostics()
        {
            AgentCombinedDiagnostics diagnostics = new AgentCombinedDiagnostics
            {
                project = FrameworkAgentDiagnostics.CreateProjectSnapshot(),
                validation = FrameworkAgentDiagnostics.ValidateProject(),
                runtime = FrameworkAgentDiagnostics.CreateRuntimeSnapshot()
            };
            return FrameworkAgentJson.Serialize(diagnostics);
        }

        public static string DumpSceneHierarchy()
        {
            return FrameworkAgentJson.Serialize(
                FrameworkAgentDiagnostics.CreateSceneHierarchy());
        }

        public static string DumpUIHierarchy()
        {
            return FrameworkAgentJson.Serialize(
                FrameworkAgentDiagnostics.CreateUIHierarchy());
        }

        public static string GetRuntimeSnapshot()
        {
            return FrameworkAgentJson.Serialize(
                FrameworkAgentDiagnostics.CreateRuntimeSnapshot());
        }

        public static string GetEditorState()
        {
            return FrameworkAgentJson.Serialize(
                FrameworkAgentDiagnostics.CreateEditorState());
        }

        public static string EnterPlayMode()
        {
            AgentCommandResult result = new AgentCommandResult
            {
                command = nameof(EnterPlayMode),
                success = !EditorApplication.isCompiling,
                status = EditorApplication.isPlaying ? "AlreadyPlaying" : "Queued",
                message = EditorApplication.isCompiling
                    ? "编译期间不能进入 Play Mode"
                    : "已请求进入 Play Mode"
            };

            if (result.success && !EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }

            return FrameworkAgentJson.Serialize(result);
        }

        public static string ExitPlayMode()
        {
            AgentCommandResult result = new AgentCommandResult
            {
                command = nameof(ExitPlayMode),
                success = true,
                status = EditorApplication.isPlaying ? "Queued" : "AlreadyStopped",
                message = "已请求退出 Play Mode"
            };

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            return FrameworkAgentJson.Serialize(result);
        }

        public static string CaptureGameView()
        {
            return FrameworkAgentScreenshot.Capture();
        }

        public static string GetScreenshotStatus()
        {
            return FrameworkAgentScreenshot.GetStatus();
        }

        public static string StartEditModeTests()
        {
            return FrameworkAgentTestRunner.StartEditModeTests();
        }

        public static string GetEditModeTestStatus()
        {
            return FrameworkAgentTestRunner.GetStatus();
        }

        public static string StartEditorAssetBundleBuild()
        {
            return FrameworkAgentTaskRunner.Start(
                nameof(StartEditorAssetBundleBuild),
                GameBuildPipeline.BuildEditorAssetBundles,
                GameBuildPipeline.GetAssetBundleOutputPath(GameBuildPipeline.EditorTarget));
        }

        public static string StartAndroidAssetBundleBuild()
        {
            return FrameworkAgentTaskRunner.Start(
                nameof(StartAndroidAssetBundleBuild),
                GameBuildPipeline.BuildAndroidAssetBundles,
                GameBuildPipeline.GetAssetBundleOutputPath(GameBuildPipeline.AndroidTarget));
        }

        public static string StartWindowsAssetBundleBuild()
        {
            return FrameworkAgentTaskRunner.Start(
                nameof(StartWindowsAssetBundleBuild),
                GameBuildPipeline.BuildWindowsAssetBundles,
                GameBuildPipeline.GetAssetBundleOutputPath(GameBuildPipeline.WindowsTarget));
        }

        public static string StartAndroidPackageBuild()
        {
            return FrameworkAgentTaskRunner.Start(
                nameof(StartAndroidPackageBuild),
                () => GameBuildPipeline.BuildAndroidPackage(),
                GameBuildPipeline.GetPlayerOutputPath(GameBuildPipeline.AndroidTarget));
        }

        public static string StartWindowsPackageBuild()
        {
            return FrameworkAgentTaskRunner.Start(
                nameof(StartWindowsPackageBuild),
                () => GameBuildPipeline.BuildWindowsPackage(),
                GameBuildPipeline.GetPlayerOutputPath(GameBuildPipeline.WindowsTarget));
        }

        public static string GetTaskStatus()
        {
            return FrameworkAgentTaskRunner.GetStatus();
        }

        public static string ResetTaskStatus()
        {
            return FrameworkAgentTaskRunner.ResetStatus();
        }

        private static void AddCommand(
            AgentCommandCatalog catalog,
            string method,
            string description)
        {
            catalog.commands.Add(new AgentCommandDescriptor
            {
                method = typeof(FrameworkAgentCommands).FullName + "." + method + "()",
                description = description
            });
        }
    }
}
