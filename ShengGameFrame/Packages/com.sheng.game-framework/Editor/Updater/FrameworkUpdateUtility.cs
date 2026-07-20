using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sheng.GameFramework.Editor.Updater
{
    /// <summary>
    /// 框架更新上下文
    /// </summary>
    public sealed class FrameworkUpdateContext
    {
        public string ManifestPath { get; internal set; }
        public string LockPath { get; internal set; }
        public string Dependency { get; internal set; }
        public string InstalledRevision { get; internal set; }
    }

    /// <summary>
    /// 负责识别和更新 UPM Git 依赖
    /// </summary>
    public static class FrameworkUpdateUtility
    {
        public const string PackageName = "com.sheng.game-framework";
        public const string RepositoryName = "Sheng2580/ShengGameFrame";
        public const string LatestCommitApi =
            "https://api.github.com/repos/Sheng2580/ShengGameFrame/commits/main";

        private const string OfficialRepository =
            "github.com/Sheng2580/ShengGameFrame.git";
        private const string OfficialPackagePath =
            "path=/ShengGameFrame/Packages/com.sheng.game-framework";

        public static bool TryCreateContext(
            out FrameworkUpdateContext context,
            out string error)
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string manifestPath = Path.Combine(
                projectRoot,
                "Packages",
                "manifest.json");
            string lockPath = Path.Combine(
                projectRoot,
                "Packages",
                "packages-lock.json");

            if (!TryReadDependency(
                    manifestPath,
                    out string dependency,
                    out error))
            {
                context = null;
                return false;
            }

            if (!IsOfficialGitDependency(dependency))
            {
                context = null;
                error = "当前框架不是从官方 Git 仓库安装，已跳过在线更新";
                return false;
            }

            string installedRevision = ReadLockedRevision(lockPath);
            if (string.IsNullOrEmpty(installedRevision))
            {
                installedRevision = ExtractRevision(dependency);
            }

            context = new FrameworkUpdateContext
            {
                ManifestPath = manifestPath,
                LockPath = lockPath,
                Dependency = dependency,
                InstalledRevision = installedRevision
            };
            error = string.Empty;
            return true;
        }

        public static bool TryReadDependency(
            string manifestPath,
            out string dependency,
            out string error)
        {
            dependency = string.Empty;
            if (string.IsNullOrWhiteSpace(manifestPath)
                || !File.Exists(manifestPath))
            {
                error = "找不到 Packages/manifest.json";
                return false;
            }

            try
            {
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                dependency = manifest["dependencies"]?[PackageName]?.Value<string>();
                if (string.IsNullOrWhiteSpace(dependency))
                {
                    error = "当前项目使用本地嵌入式框架，无需在线更新";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"读取 manifest.json 失败 {exception.Message}";
                return false;
            }
        }

        public static bool IsOfficialGitDependency(string dependency)
        {
            return !string.IsNullOrWhiteSpace(dependency)
                   && dependency.IndexOf(
                       OfficialRepository,
                       StringComparison.OrdinalIgnoreCase) >= 0
                   && dependency.IndexOf(
                       OfficialPackagePath,
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string ExtractRevision(string dependency)
        {
            if (string.IsNullOrWhiteSpace(dependency))
            {
                return string.Empty;
            }

            int fragmentIndex = dependency.LastIndexOf('#');
            return fragmentIndex >= 0 && fragmentIndex < dependency.Length - 1
                ? dependency.Substring(fragmentIndex + 1).Trim()
                : string.Empty;
        }

        public static bool IsSameRevision(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left)
                || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            string normalizedLeft = left.Trim();
            string normalizedRight = right.Trim();
            return normalizedLeft.StartsWith(
                       normalizedRight,
                       StringComparison.OrdinalIgnoreCase)
                   || normalizedRight.StartsWith(
                       normalizedLeft,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryParseLatestRevision(
            string responseJson,
            out string revision,
            out string error)
        {
            revision = string.Empty;
            try
            {
                revision = JObject.Parse(responseJson)["sha"]?.Value<string>()?.Trim();
                if (!IsCommitRevision(revision))
                {
                    error = "GitHub 返回的提交号无效";
                    revision = string.Empty;
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"解析 GitHub 更新信息失败 {exception.Message}";
                return false;
            }
        }

        public static bool TryApplyRevision(
            string manifestPath,
            string revision,
            out string updatedDependency,
            out string error)
        {
            updatedDependency = string.Empty;
            if (!IsCommitRevision(revision))
            {
                error = "提交号格式无效";
                return false;
            }

            if (!TryReadDependency(
                    manifestPath,
                    out string currentDependency,
                    out error))
            {
                return false;
            }

            if (!IsOfficialGitDependency(currentDependency))
            {
                error = "只允许更新官方 Git 仓库安装的框架";
                return false;
            }

            int fragmentIndex = currentDependency.LastIndexOf('#');
            string dependencyRoot = fragmentIndex >= 0
                ? currentDependency.Substring(0, fragmentIndex)
                : currentDependency;
            updatedDependency = $"{dependencyRoot}#{revision}";

            try
            {
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                JObject dependencies = manifest["dependencies"] as JObject;
                if (dependencies == null)
                {
                    error = "manifest.json 缺少 dependencies";
                    updatedDependency = string.Empty;
                    return false;
                }

                dependencies[PackageName] = updatedDependency;
                File.WriteAllText(
                    manifestPath,
                    manifest.ToString(Formatting.Indented) + Environment.NewLine);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = $"写入 manifest.json 失败 {exception.Message}";
                updatedDependency = string.Empty;
                return false;
            }
        }

        public static string ShortRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision))
            {
                return "未知";
            }

            string value = revision.Trim();
            return value.Length > 7 ? value.Substring(0, 7) : value;
        }

        private static string ReadLockedRevision(string lockPath)
        {
            if (string.IsNullOrWhiteSpace(lockPath) || !File.Exists(lockPath))
            {
                return string.Empty;
            }

            try
            {
                JObject lockFile = JObject.Parse(File.ReadAllText(lockPath));
                return lockFile["dependencies"]?[PackageName]?["hash"]
                    ?.Value<string>()
                    ?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsCommitRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision)
                || revision.Length < 7
                || revision.Length > 40)
            {
                return false;
            }

            for (int i = 0; i < revision.Length; i++)
            {
                char value = revision[i];
                bool isHex = value >= '0' && value <= '9'
                             || value >= 'a' && value <= 'f'
                             || value >= 'A' && value <= 'F';
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
