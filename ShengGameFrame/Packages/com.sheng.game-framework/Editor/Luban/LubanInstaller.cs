using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// Luban 固定版本安装状态
    /// </summary>
    public sealed class LubanEnvironmentStatus
    {
        public bool IsLubanInstalled;
        public string LubanPath;
        public string DotnetPath;
        public string ErrorMessage;
        public bool IsReady => IsLubanInstalled
                               && !string.IsNullOrEmpty(DotnetPath)
                               && string.IsNullOrEmpty(ErrorMessage);
    }

    /// <summary>
    /// Luban 安装结果
    /// </summary>
    public sealed class LubanInstallResult
    {
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// 下载并校验框架固定版本的 Luban
    /// </summary>
    public static class LubanInstaller
    {
        public const string Version = "4.10.2";
        public const string DownloadUrl =
            "https://github.com/focus-creative-games/luban/releases/download/v4.10.2/Luban.7z";
        public const string ArchiveSha256 =
            "785b53b570c918827d314ef78caa180ca1c55bc252ebc1e921a6dc0760317e8d";

        public static LubanEnvironmentStatus GetStatus(LubanToolSettings settings)
        {
            string dotnetPath = FindDotnet(settings?.DotnetPathOverride);
            LubanEnvironmentStatus status = new LubanEnvironmentStatus
            {
                LubanPath = settings?.LubanDllPath ?? string.Empty,
                DotnetPath = dotnetPath,
                IsLubanInstalled = settings != null
                                   && File.Exists(settings.LubanDllPath)
            };
            if (string.IsNullOrEmpty(dotnetPath))
            {
                status.ErrorMessage = "没有找到 dotnet 请安装 .NET 8 或在设置中指定路径";
            }
            else if (!status.IsLubanInstalled)
            {
                status.ErrorMessage = "Luban 尚未安装";
            }

            return status;
        }

        public static Task<LubanInstallResult> InstallAsync(
            LubanToolSettings settings,
            Action<string> log = null)
        {
            return Task.Run(() => Install(settings, log));
        }

        public static string FindDotnet(string overridePath = null)
        {
            List<string> candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                candidates.Add(overridePath.Trim());
            }

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                candidates.Add("dotnet.exe");
                string programFiles = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles);
                candidates.Add(Path.Combine(programFiles, "dotnet", "dotnet.exe"));
            }
            else
            {
                candidates.Add("/opt/homebrew/bin/dotnet");
                candidates.Add("/usr/local/bin/dotnet");
                candidates.Add("/usr/local/share/dotnet/dotnet");
                candidates.Add("/usr/bin/dotnet");
                candidates.Add(
                    "/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet");
                candidates.Add(
                    "/Applications/Rider.app/Contents/lib/ReSharperHost/macos-x64/dotnet/dotnet");
                candidates.Add("dotnet");
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (candidate.Contains(Path.DirectorySeparatorChar.ToString())
                    || candidate.Contains(Path.AltDirectorySeparatorChar.ToString()))
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    continue;
                }

                try
                {
                    LubanProcessResult result = LubanProcessRunner.Run(
                        candidate,
                        new[] { "--version" },
                        Environment.CurrentDirectory);
                    if (result.Success)
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static LubanInstallResult Install(
            LubanToolSettings settings,
            Action<string> log)
        {
            if (settings == null)
            {
                return new LubanInstallResult
                {
                    Success = false,
                    Message = "Luban 设置不存在"
                };
            }

            string downloadDirectory = Path.Combine(
                settings.LibraryRootPath,
                "Downloads");
            string archivePath = Path.Combine(
                downloadDirectory,
                "Luban." + Version + ".7z");
            string stagingPath = Path.Combine(
                settings.LibraryRootPath,
                "Tools",
                "Luban",
                ".install_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(downloadDirectory);
                if (!File.Exists(archivePath)
                    || !string.Equals(
                        ComputeSha256(archivePath),
                        ArchiveSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    log?.Invoke($"下载 Luban {Version}");
                    using (WebClient client = new WebClient())
                    {
                        client.Headers.Add(
                            HttpRequestHeader.UserAgent,
                            "ShengGameFramework-LubanInstaller");
                        client.DownloadFile(DownloadUrl, archivePath);
                    }
                }
                else
                {
                    log?.Invoke($"使用已校验的 Luban {Version} 安装包");
                }

                string actualHash = ComputeSha256(archivePath);
                if (!string.Equals(
                        actualHash,
                        ArchiveSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new LubanInstallResult
                    {
                        Success = false,
                        Message = $"Luban 安装包校验失败 {actualHash}"
                    };
                }

                Directory.CreateDirectory(stagingPath);
                log?.Invoke("解压 Luban");
                LubanProcessResult extractResult = LubanProcessRunner.Run(
                    ResolveTarExecutable(),
                    new[] { "-xf", archivePath, "-C", stagingPath },
                    settings.ProjectRoot);
                if (!extractResult.Success)
                {
                    return new LubanInstallResult
                    {
                        Success = false,
                        Message = "Luban 解压失败 " + extractResult.StandardError
                    };
                }

                string extractedPath = Path.Combine(stagingPath, "Luban");
                if (!File.Exists(Path.Combine(extractedPath, "Luban.dll")))
                {
                    return new LubanInstallResult
                    {
                        Success = false,
                        Message = "安装包中没有找到 Luban.dll"
                    };
                }

                string versionRoot = Directory.GetParent(settings.LubanInstallPath)?.FullName;
                if (!string.IsNullOrEmpty(versionRoot))
                {
                    Directory.CreateDirectory(versionRoot);
                }

                if (Directory.Exists(settings.LubanInstallPath))
                {
                    Directory.Delete(settings.LubanInstallPath, true);
                }

                Directory.Move(extractedPath, settings.LubanInstallPath);
                log?.Invoke($"Luban 已安装 {settings.LubanDllPath}");
                return new LubanInstallResult
                {
                    Success = true,
                    Message = $"Luban {Version} 安装完成"
                };
            }
            catch (Exception exception)
            {
                return new LubanInstallResult
                {
                    Success = false,
                    Message = exception.Message
                };
            }
            finally
            {
                try
                {
                    if (Directory.Exists(stagingPath))
                    {
                        Directory.Delete(stagingPath, true);
                    }
                }
                catch (Exception exception)
                {
                    log?.Invoke($"清理 Luban 临时目录失败 {exception.Message}");
                }
            }
        }

        private static string ResolveTarExecutable()
        {
            return Application.platform == RuntimePlatform.WindowsEditor
                ? "tar.exe"
                : "/usr/bin/tar";
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
