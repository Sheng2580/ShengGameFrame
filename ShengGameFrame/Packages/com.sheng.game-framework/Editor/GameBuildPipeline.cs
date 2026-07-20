using System;
using System.Collections.Generic;
using System.IO;
using Sheng.GameFramework.Assets;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sheng.GameFramework.Editor
{
    /// <summary>
    /// 游戏多平台构建流程
    /// </summary>
    public static class GameBuildPipeline
    {
        public const BuildTarget AndroidTarget = BuildTarget.Android;
        public const BuildTarget WindowsTarget = BuildTarget.StandaloneWindows64;

        public static BuildTarget EditorTarget
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        return BuildTarget.StandaloneWindows64;
                    case RuntimePlatform.LinuxEditor:
                        return BuildTarget.StandaloneLinux64;
                    case RuntimePlatform.OSXEditor:
                    default:
                        return BuildTarget.StandaloneOSX;
                }
            }
        }

        private const string StreamingAssetsRoot = "Assets/StreamingAssets/" +
                                                   AssetBundlePath.StreamingAssetsFolderName;
        private const string PlayerBuildRoot = "Builds";

        public static bool BuildEditorAssetBundles()
        {
            return BuildAssetBundles(EditorTarget, true);
        }

        public static bool BuildAndroidAssetBundles()
        {
            return BuildAssetBundles(AndroidTarget, true);
        }

        public static bool BuildWindowsAssetBundles()
        {
            return BuildAssetBundles(WindowsTarget, true);
        }

        public static bool BuildAndroidPackage(bool developmentBuild = false)
        {
            return BuildCompletePackage(AndroidTarget, developmentBuild);
        }

        public static bool BuildWindowsPackage(bool developmentBuild = false)
        {
            return BuildCompletePackage(WindowsTarget, developmentBuild);
        }

        /// <summary>
        /// 清理并重新生成指定平台的全部 AssetBundle
        /// </summary>
        public static bool BuildAssetBundles(BuildTarget target, bool cleanOutput)
        {
            if (!ValidateBuildTarget(target))
            {
                return false;
            }

            string outputPath = GetAssetBundleOutputPath(target);
            try
            {
                if (cleanOutput && Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, true);
                }

                Directory.CreateDirectory(outputPath);
                if (AssetDatabase.GetAllAssetBundleNames().Length == 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[GameBuild] 当前没有设置 AB 资源 {target} 空构建完成");
                    return true;
                }

                AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                    outputPath,
                    BuildAssetBundleOptions.ChunkBasedCompression,
                    target);

                AssetDatabase.Refresh();
                if (manifest == null)
                {
                    Debug.LogError($"[GameBuild] AB 构建失败 {target}");
                    return false;
                }

                Debug.Log($"[GameBuild] AB 构建完成 {Path.GetFullPath(outputPath)}");
                return true;
            }
            catch (Exception exception)
            {
                AssetDatabase.Refresh();
                Debug.LogError($"[GameBuild] AB 构建异常 {target}\n{exception}");
                return false;
            }
        }

        /// <summary>
        /// 切换平台并按重建 AB 后生成 Player 的顺序构建完整包
        /// </summary>
        public static bool BuildCompletePackage(
            BuildTarget target,
            bool developmentBuild = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling)
            {
                Debug.LogError("[GameBuild] 播放或编译期间不能执行完整打包");
                return false;
            }

            if (!ValidateBuildTarget(target))
            {
                return false;
            }

            string[] scenePaths = GetEnabledScenePaths();
            if (scenePaths.Length == 0)
            {
                Debug.LogError("[GameBuild] Build Settings 中没有启用的场景");
                return false;
            }

            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target
                && !EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target))
            {
                Debug.LogError($"[GameBuild] 无法切换构建平台 {target}");
                return false;
            }

            AssetDatabase.SaveAssets();
            if (!CleanAllAssetBundleOutputs())
            {
                Debug.LogError("[GameBuild] 无法清理旧 AB 已停止完整打包");
                return false;
            }

            if (!BuildAssetBundles(target, true))
            {
                Debug.LogError("[GameBuild] AB 构建失败 已停止完整打包");
                return false;
            }

            string playerOutputPath = GetPlayerOutputPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(playerOutputPath));

            BuildPlayerOptions playerOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = playerOutputPath,
                target = target,
                options = developmentBuild
                    ? BuildOptions.Development
                    : BuildOptions.None
            };

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(playerOptions);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameBuild] Player 构建异常 {target}\n{exception}");
                return false;
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"[GameBuild] Player 构建失败 {target} " +
                    $"错误 {summary.totalErrors} 警告 {summary.totalWarnings}");
                return false;
            }

            Debug.Log(
                $"[GameBuild] 完整包构建完成 {Path.GetFullPath(playerOutputPath)} " +
                $"大小 {summary.totalSize} 耗时 {summary.totalTime}");
            return true;
        }

        public static string GetAssetBundleOutputPath(BuildTarget target)
        {
            return $"{StreamingAssetsRoot}/{ResolvePlatformName(target)}";
        }

        public static string GetPlayerOutputDirectory(BuildTarget target)
        {
            return $"{PlayerBuildRoot}/{ResolvePlayerFolderName(target)}";
        }

        public static string GetPlayerOutputPath(BuildTarget target)
        {
            string productName = SanitizeFileName(PlayerSettings.productName);
            string extension;
            switch (target)
            {
                case BuildTarget.Android:
                    extension = EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
                    break;
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    extension = ".exe";
                    break;
                case BuildTarget.StandaloneOSX:
                    extension = ".app";
                    break;
                default:
                    extension = string.Empty;
                    break;
            }

            return $"{GetPlayerOutputDirectory(target)}/{productName}{extension}";
        }

        public static string[] GetEnabledScenePaths()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            List<string> paths = new List<string>(scenes.Length);
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                if (scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                {
                    paths.Add(scene.path);
                }
            }

            return paths.ToArray();
        }

        public static void OpenAssetBundleOutputFolder(BuildTarget target)
        {
            string outputPath = Path.GetFullPath(GetAssetBundleOutputPath(target));
            Directory.CreateDirectory(outputPath);
            EditorUtility.RevealInFinder(outputPath);
        }

        public static void OpenPlayerOutputFolder(BuildTarget target)
        {
            string outputPath = Path.GetFullPath(GetPlayerOutputDirectory(target));
            Directory.CreateDirectory(outputPath);
            EditorUtility.RevealInFinder(outputPath);
        }

        /// <summary>
        /// 清理全部平台 AB 防止完整包混入其他平台资源
        /// </summary>
        public static bool CleanAllAssetBundleOutputs()
        {
            try
            {
                if (Directory.Exists(StreamingAssetsRoot))
                {
                    Directory.Delete(StreamingAssetsRoot, true);
                }

                Directory.CreateDirectory(StreamingAssetsRoot);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                AssetDatabase.Refresh();
                Debug.LogError($"[GameBuild] 清理旧 AB 异常\n{exception}");
                return false;
            }
        }

        public static string ResolvePlatformName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "StandaloneWindows64";
                case BuildTarget.StandaloneLinux64:
                    return "StandaloneLinux64";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.StandaloneOSX:
                default:
                    return "StandaloneOSX";
            }
        }

        private static bool ValidateBuildTarget(BuildTarget target)
        {
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
            if (targetGroup == BuildTargetGroup.Unknown
                || !BuildPipeline.IsBuildTargetSupported(targetGroup, target))
            {
                Debug.LogError($"[GameBuild] 当前 Unity 未安装目标平台模块 {target}");
                return false;
            }

            return true;
        }

        private static string ResolvePlayerFolderName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows64";
                case BuildTarget.StandaloneOSX:
                    return "macOS";
                default:
                    return ResolvePlatformName(target);
            }
        }

        private static string SanitizeFileName(string fileName)
        {
            string result = string.IsNullOrWhiteSpace(fileName) ? "Game" : fileName;
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                result = result.Replace(invalidCharacters[i], '_');
            }

            return result;
        }
    }
}
