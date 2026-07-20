using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Sheng.GameFramework.Editor.AgentBridge
{
    /// <summary>
    /// 提供可由 AI 启动和查询的 EditMode 测试
    /// </summary>
    [InitializeOnLoad]
    internal static class FrameworkAgentTestRunner
    {
        private const string StateRelativePath =
            "Library/ShengGameFramework/Agent/test-state.json";
        private const int MaximumFailures = 50;

        private static AgentTestState _state = LoadState();
        private static TestRunnerApi _api;
        private static TestCallbacks _callbacks;

        static FrameworkAgentTestRunner()
        {
        }

        public static string StartEditModeTests()
        {
            if (string.Equals(_state.status, "Queued", StringComparison.Ordinal)
                || string.Equals(_state.status, "Running", StringComparison.Ordinal))
            {
                return FrameworkAgentJson.Serialize(_state);
            }

            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                _state = new AgentTestState
                {
                    status = "Rejected",
                    startedAtUtc = FrameworkAgentJson.UtcNow()
                };
                SaveState();
                return FrameworkAgentJson.Serialize(_state);
            }

            _state = new AgentTestState
            {
                status = "Queued",
                startedAtUtc = FrameworkAgentJson.UtcNow()
            };
            SaveState();

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _callbacks = new TestCallbacks();
            _api.RegisterCallbacks(_callbacks);

            Filter filter = new Filter { testMode = TestMode.EditMode };
            ExecutionSettings settings = new ExecutionSettings(filter);
            _state.jobId = _api.Execute(settings);
            SaveState();
            return FrameworkAgentJson.Serialize(_state);
        }

        public static string GetStatus()
        {
            return FrameworkAgentJson.Serialize(_state);
        }

        private static void CompleteRun(ITestResultAdaptor result)
        {
            _state.status = result.FailCount == 0 ? "Succeeded" : "Failed";
            _state.passedCount = result.PassCount;
            _state.failedCount = result.FailCount;
            _state.skippedCount = result.SkipCount;
            _state.inconclusiveCount = result.InconclusiveCount;
            _state.durationSeconds = result.Duration;
            _state.completedAtUtc = FrameworkAgentJson.UtcNow();
            SaveState();

            EditorApplication.update -= CleanupApi;
            EditorApplication.update += CleanupApi;
        }

        private static void CleanupApi()
        {
            EditorApplication.update -= CleanupApi;
            if (_api != null && _callbacks != null)
            {
                _api.UnregisterCallbacks(_callbacks);
            }

            if (_api != null)
            {
                UnityEngine.Object.DestroyImmediate(_api);
            }

            _api = null;
            _callbacks = null;
        }

        private static AgentTestState LoadState()
        {
            string path = GetStatePath();
            try
            {
                if (File.Exists(path))
                {
                    AgentTestState state =
                        JsonUtility.FromJson<AgentTestState>(File.ReadAllText(path));
                    if (state != null)
                    {
                        if (string.Equals(state.status, "Queued", StringComparison.Ordinal)
                            || string.Equals(state.status, "Running", StringComparison.Ordinal))
                        {
                            state.status = "Interrupted";
                            state.completedAtUtc = FrameworkAgentJson.UtcNow();
                        }

                        return state;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FrameworkAgent] 无法读取测试状态 {exception.Message}");
            }

            return new AgentTestState { status = "Idle" };
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
                Debug.LogWarning($"[FrameworkAgent] 无法保存测试状态 {exception.Message}");
            }
        }

        private static string GetStatePath()
        {
            return Path.GetFullPath(StateRelativePath);
        }

        private sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                _state.status = "Running";
                _state.totalCount = testsToRun.TestCaseCount;
                SaveState();
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                CompleteRun(result);
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite
                    || result.FailCount == 0
                    || _state.failures.Count >= MaximumFailures)
                {
                    return;
                }

                _state.failures.Add(new AgentTestFailure
                {
                    name = result.FullName,
                    message = result.Message,
                    stackTrace = result.StackTrace
                });
                SaveState();
            }
        }
    }
}
