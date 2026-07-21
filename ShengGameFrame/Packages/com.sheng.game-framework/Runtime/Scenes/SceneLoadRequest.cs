using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sheng.GameFramework.Scenes
{
    /// <summary>
    /// 场景加载请求状态
    /// </summary>
    public enum SceneLoadStatus
    {
        Queued,
        Loading,
        Activating,
        Succeeded,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 场景加载请求及其只读运行状态
    /// </summary>
    public sealed class SceneLoadRequest
    {
        internal SceneLoadRequest(
            int id,
            string sceneName,
            SceneLoadOptions options,
            Action<Scene> completed,
            Action<float> progressChanged,
            Action<string> failed)
        {
            Id = id;
            SceneName = sceneName;
            Options = options;
            CompletedCallback = completed;
            ProgressCallback = progressChanged;
            FailedCallback = failed;
        }

        public int Id { get; }
        public string SceneName { get; }
        public LoadSceneMode Mode => Options.Mode;
        public SceneLoadStatus Status { get; private set; }
        public float Progress { get; private set; }
        public string Error { get; private set; } = string.Empty;
        public bool IsDone => Status == SceneLoadStatus.Succeeded
                              || Status == SceneLoadStatus.Failed
                              || Status == SceneLoadStatus.Cancelled;
        public bool Succeeded => Status == SceneLoadStatus.Succeeded;

        internal SceneLoadOptions Options { get; }
        internal Action<Scene> CompletedCallback { get; }
        internal Action<float> ProgressCallback { get; }
        internal Action<string> FailedCallback { get; }

        internal void MarkLoading()
        {
            Status = SceneLoadStatus.Loading;
            Error = string.Empty;
        }

        internal bool ReportProgress(float value)
        {
            if (IsDone)
            {
                return false;
            }

            float normalized = Mathf.Clamp01(value);
            if (normalized <= Progress)
            {
                return false;
            }

            Progress = normalized;
            return true;
        }

        internal void MarkActivating()
        {
            if (!IsDone)
            {
                Status = SceneLoadStatus.Activating;
            }
        }

        internal void MarkSucceeded()
        {
            Status = SceneLoadStatus.Succeeded;
            Progress = 1f;
            Error = string.Empty;
        }

        internal void MarkFailed(string error)
        {
            Status = SceneLoadStatus.Failed;
            Error = error ?? string.Empty;
        }

        internal void MarkCancelled()
        {
            Status = SceneLoadStatus.Cancelled;
            Error = "场景加载请求已取消";
        }
    }
}
