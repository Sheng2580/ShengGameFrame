using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor.AgentBridge
{
    /// <summary>
    /// 将耗时编辑器操作转换为可查询任务
    /// </summary>
    [InitializeOnLoad]
    internal static class FrameworkAgentTaskRunner
    {
        private const string StateRelativePath =
            "Library/ShengGameFramework/Agent/task-state.json";

        private static AgentTaskState _state = LoadState();
        private static Func<Task<bool>> _pendingAction;

        static FrameworkAgentTaskRunner()
        {
        }

        public static string Start(
            string taskName,
            Func<bool> action,
            string outputPath = "")
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return StartAsync(
                taskName,
                () => Task.FromResult(action.Invoke()),
                outputPath);
        }

        public static string StartAsync(
            string taskName,
            Func<Task<bool>> action,
            string outputPath = "")
        {
            if (_pendingAction != null
                || string.Equals(_state.status, "Queued", StringComparison.Ordinal)
                || string.Equals(_state.status, "Running", StringComparison.Ordinal))
            {
                _state.message = "已有任务正在执行";
                SaveState();
                return FrameworkAgentJson.Serialize(_state);
            }

            _pendingAction = action ?? throw new ArgumentNullException(nameof(action));
            _state = new AgentTaskState
            {
                id = Guid.NewGuid().ToString("N"),
                name = taskName,
                status = "Queued",
                message = "任务已进入队列",
                outputPath = NormalizePath(outputPath),
                startedAtUtc = FrameworkAgentJson.UtcNow(),
                completedAtUtc = string.Empty
            };
            SaveState();
            EditorApplication.update -= ExecutePendingAction;
            EditorApplication.update += ExecutePendingAction;
            return FrameworkAgentJson.Serialize(_state);
        }

        public static string GetStatus()
        {
            return FrameworkAgentJson.Serialize(_state);
        }

        public static string ResetStatus()
        {
            if (_pendingAction != null
                || string.Equals(_state.status, "Queued", StringComparison.Ordinal)
                || string.Equals(_state.status, "Running", StringComparison.Ordinal))
            {
                _state.message = "运行中的任务不能重置";
                SaveState();
                return FrameworkAgentJson.Serialize(_state);
            }

            _state = CreateIdleState();
            SaveState();
            return FrameworkAgentJson.Serialize(_state);
        }

        private static async void ExecutePendingAction()
        {
            EditorApplication.update -= ExecutePendingAction;
            Func<Task<bool>> action = _pendingAction;
            _pendingAction = null;
            if (action == null)
            {
                return;
            }

            _state.status = "Running";
            _state.message = "任务正在执行";
            SaveState();

            try
            {
                bool succeeded = await action.Invoke();
                _state.status = succeeded ? "Succeeded" : "Failed";
                _state.message = succeeded ? "任务执行成功" : "任务返回失败";
            }
            catch (Exception exception)
            {
                _state.status = "Failed";
                _state.message = exception.ToString();
                Debug.LogError($"[FrameworkAgent] 任务执行异常 {_state.name}\n{exception}");
            }

            _state.completedAtUtc = FrameworkAgentJson.UtcNow();
            SaveState();
        }

        private static AgentTaskState LoadState()
        {
            string path = GetStatePath();
            try
            {
                if (File.Exists(path))
                {
                    AgentTaskState state =
                        JsonUtility.FromJson<AgentTaskState>(File.ReadAllText(path));
                    if (state != null)
                    {
                        if (string.Equals(state.status, "Queued", StringComparison.Ordinal)
                            || string.Equals(state.status, "Running", StringComparison.Ordinal))
                        {
                            state.status = "Interrupted";
                            state.message = "编辑器重载导致任务状态中断";
                            state.completedAtUtc = FrameworkAgentJson.UtcNow();
                        }

                        return state;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FrameworkAgent] 无法读取任务状态 {exception.Message}");
            }

            return CreateIdleState();
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
                Debug.LogWarning($"[FrameworkAgent] 无法保存任务状态 {exception.Message}");
            }
        }

        private static AgentTaskState CreateIdleState()
        {
            return new AgentTaskState
            {
                status = "Idle",
                message = "当前没有任务"
            };
        }

        private static string GetStatePath()
        {
            return Path.GetFullPath(StateRelativePath);
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);
        }
    }
}
