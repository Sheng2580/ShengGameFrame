using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor.AgentBridge
{
    /// <summary>
    /// 管理由 AI 请求的 Game View 截图
    /// </summary>
    [InitializeOnLoad]
    internal static class FrameworkAgentScreenshot
    {
        private const string StateRelativePath =
            "Library/ShengGameFramework/Agent/screenshot-state.json";
        private const double TimeoutSeconds = 20d;

        private static AgentScreenshotState _state = LoadState();
        private static double _deadline;

        static FrameworkAgentScreenshot()
        {
        }

        public static string Capture()
        {
            if (!EditorApplication.isPlaying)
            {
                _state = new AgentScreenshotState
                {
                    status = "Rejected",
                    message = "只有 Play Mode 可以截取 Game View",
                    requestedAtUtc = FrameworkAgentJson.UtcNow()
                };
                SaveState();
                return FrameworkAgentJson.Serialize(_state);
            }

            string directory = Path.GetFullPath("AgentArtifacts/Screenshots");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"game-view-{DateTime.Now:yyyyMMdd-HHmmss}.png");

            _state = new AgentScreenshotState
            {
                status = "Queued",
                path = path,
                message = "截图请求已提交",
                requestedAtUtc = FrameworkAgentJson.UtcNow()
            };
            SaveState();

            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update -= PollScreenshot;
            EditorApplication.update += PollScreenshot;
            ScreenCapture.CaptureScreenshot(path);
            return FrameworkAgentJson.Serialize(_state);
        }

        public static string GetStatus()
        {
            return FrameworkAgentJson.Serialize(_state);
        }

        private static void PollScreenshot()
        {
            if (!string.IsNullOrEmpty(_state.path)
                && File.Exists(_state.path)
                && new FileInfo(_state.path).Length > 0)
            {
                _state.status = "Succeeded";
                _state.message = "截图已生成";
                _state.completedAtUtc = FrameworkAgentJson.UtcNow();
                SaveState();
                EditorApplication.update -= PollScreenshot;
                return;
            }

            if (EditorApplication.timeSinceStartup < _deadline)
            {
                return;
            }

            _state.status = "Failed";
            _state.message = "等待截图超时";
            _state.completedAtUtc = FrameworkAgentJson.UtcNow();
            SaveState();
            EditorApplication.update -= PollScreenshot;
        }

        private static AgentScreenshotState LoadState()
        {
            string path = GetStatePath();
            try
            {
                if (File.Exists(path))
                {
                    AgentScreenshotState state =
                        JsonUtility.FromJson<AgentScreenshotState>(File.ReadAllText(path));
                    if (state != null)
                    {
                        if (string.Equals(state.status, "Queued", StringComparison.Ordinal))
                        {
                            state.status = "Interrupted";
                            state.message = "编辑器重载导致截图状态中断";
                        }

                        return state;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FrameworkAgent] 无法读取截图状态 {exception.Message}");
            }

            return new AgentScreenshotState { status = "Idle" };
        }

        private static void SaveState()
        {
            try
            {
                string path = GetStatePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, FrameworkAgentJson.Serialize(_state));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FrameworkAgent] 无法保存截图状态 {exception.Message}");
            }
        }

        private static string GetStatePath()
        {
            return Path.GetFullPath(StateRelativePath);
        }
    }
}
