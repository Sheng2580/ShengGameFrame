using System;
using System.Collections;
using System.Collections.Generic;
using Sheng.GameFramework.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Sheng.GameFramework.Scenes
{
    /// <summary>
    /// 统一管理场景异步加载 卸载和生命周期事件
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneManager : PersistentMonoSingleton<SceneManager>
    {
        private readonly SceneLoadQueue _loadQueue = new SceneLoadQueue();
        private readonly List<SceneLoadRequest> _cancelBuffer =
            new List<SceneLoadRequest>();
        private readonly HashSet<int> _unloadingSceneHandles =
            new HashSet<int>();

        private Coroutine _loadCoroutine;
        private AsyncOperation _activeOperation;
        private int _nextRequestId;
        private bool _isShuttingDown;

        public event Action<SceneLoadRequest> LoadStarted;
        public event Action<SceneLoadRequest, float> LoadProgressChanged;
        public event Action<SceneLoadRequest, Scene> LoadCompleted;
        public event Action<SceneLoadRequest, string> LoadFailed;
        public event Action<SceneLoadRequest> LoadCancelled;
        public event Action<Scene, LoadSceneMode> SceneLoaded;
        public event Action<Scene> SceneUnloaded;
        public event Action<Scene, Scene> ActiveSceneChanged;

        public bool IsLoading => _loadQueue.Current != null;
        public int PendingLoadCount => _loadQueue.PendingCount;
        public SceneLoadRequest CurrentRequest => _loadQueue.Current;
        public float Progress => CurrentRequest?.Progress ?? 0f;
        public string TargetSceneName => CurrentRequest?.SceneName ?? string.Empty;

        protected override void OnSingletonAwake()
        {
            UnitySceneManager.sceneLoaded += OnUnitySceneLoaded;
            UnitySceneManager.sceneUnloaded += OnUnitySceneUnloaded;
            UnitySceneManager.activeSceneChanged += OnUnityActiveSceneChanged;
        }

        protected override void OnSingletonDestroyed()
        {
            _isShuttingDown = true;
            UnitySceneManager.sceneLoaded -= OnUnitySceneLoaded;
            UnitySceneManager.sceneUnloaded -= OnUnitySceneUnloaded;
            UnitySceneManager.activeSceneChanged -= OnUnityActiveSceneChanged;

            if (_loadCoroutine != null)
            {
                if (_activeOperation != null)
                {
                    _activeOperation.allowSceneActivation = true;
                }

                StopCoroutine(_loadCoroutine);
                _loadCoroutine = null;
                _activeOperation = null;
            }

            SceneLoadRequest current = _loadQueue.Current;
            if (current != null)
            {
                current.MarkCancelled();
                _loadQueue.CompleteCurrent(current);
            }

            _cancelBuffer.Clear();
            _loadQueue.ClearPending(_cancelBuffer);
            for (int i = 0; i < _cancelBuffer.Count; i++)
            {
                _cancelBuffer[i].MarkCancelled();
            }

            _cancelBuffer.Clear();
            _unloadingSceneHandles.Clear();
        }

        public SceneLoadRequest LoadSceneAsync(string sceneName)
        {
            return LoadSceneAsync(
                sceneName,
                new SceneLoadOptions(),
                null);
        }

        public SceneLoadRequest LoadSceneAsync(
            string sceneName,
            Action completed)
        {
            return LoadSceneAsync(
                sceneName,
                new SceneLoadOptions(),
                _ => InvokeSafely(completed));
        }

        public SceneLoadRequest LoadSceneAsync(
            string sceneName,
            Action<Scene> completed)
        {
            return LoadSceneAsync(
                sceneName,
                new SceneLoadOptions(),
                completed);
        }

        public SceneLoadRequest LoadSceneAsync(
            string sceneName,
            SceneLoadOptions options,
            Action<Scene> completed = null,
            Action<float> progressChanged = null,
            Action<string> failed = null)
        {
            string normalizedSceneName = sceneName?.Trim() ?? string.Empty;
            SceneLoadOptions copiedOptions = options?.Clone()
                                             ?? new SceneLoadOptions();
            SceneLoadRequest request = new SceneLoadRequest(
                NextRequestId(),
                normalizedSceneName,
                copiedOptions,
                completed,
                progressChanged,
                failed);

            if (string.IsNullOrEmpty(normalizedSceneName))
            {
                FailUnqueuedRequest(request, "场景名称不能为空");
                return request;
            }

            try
            {
                copiedOptions.Validate();
            }
            catch (Exception exception)
            {
                FailUnqueuedRequest(request, exception.Message);
                return request;
            }

            if (!Application.isPlaying)
            {
                FailUnqueuedRequest(request, "异步场景加载只能在运行模式中使用");
                return request;
            }

            if (!Application.CanStreamedLevelBeLoaded(normalizedSceneName))
            {
                FailUnqueuedRequest(
                    request,
                    $"场景未加入 Build Settings 或不可加载 {normalizedSceneName}");
                return request;
            }

            _loadQueue.Enqueue(request);
            StartNextLoad();
            return request;
        }

        public SceneLoadRequest LoadSceneAdditiveAsync(
            string sceneName,
            bool setActiveAfterLoad = false,
            Action<Scene> completed = null)
        {
            return LoadSceneAsync(
                sceneName,
                new SceneLoadOptions
                {
                    Mode = LoadSceneMode.Additive,
                    SetActiveAfterLoad = setActiveAfterLoad
                },
                completed);
        }

        public SceneLoadRequest ReloadActiveSceneAsync(
            Action<Scene> completed = null)
        {
            Scene activeScene = UnitySceneManager.GetActiveScene();
            string sceneIdentifier = string.IsNullOrEmpty(activeScene.path)
                ? activeScene.name
                : activeScene.path;
            return LoadSceneAsync(sceneIdentifier, completed);
        }

        public bool CancelPendingLoad(SceneLoadRequest request)
        {
            if (!_loadQueue.CancelPending(request))
            {
                return false;
            }

            request.MarkCancelled();
            InvokeSafely(LoadCancelled, request);
            return true;
        }

        public int ClearPendingLoads()
        {
            _cancelBuffer.Clear();
            int cancelledCount = _loadQueue.ClearPending(_cancelBuffer);
            for (int i = 0; i < _cancelBuffer.Count; i++)
            {
                SceneLoadRequest request = _cancelBuffer[i];
                request.MarkCancelled();
                InvokeSafely(LoadCancelled, request);
            }

            _cancelBuffer.Clear();
            return cancelledCount;
        }

        public bool UnloadSceneAsync(
            string sceneName,
            Action completed = null,
            Action<string> failed = null)
        {
            if (!Application.isPlaying)
            {
                return ReportUnloadFailure(
                    sceneName,
                    "异步场景卸载只能在运行模式中使用",
                    failed);
            }

            Scene scene = FindLoadedScene(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return ReportUnloadFailure(
                    sceneName,
                    "目标场景未加载",
                    failed);
            }

            if (_unloadingSceneHandles.Contains(scene.handle))
            {
                return ReportUnloadFailure(
                    sceneName,
                    "目标场景正在卸载",
                    failed);
            }

            AsyncOperation operation = UnitySceneManager.UnloadSceneAsync(scene);
            if (operation == null)
            {
                return ReportUnloadFailure(
                    sceneName,
                    "Unity 未能创建场景卸载任务",
                    failed);
            }

            int sceneHandle = scene.handle;
            _unloadingSceneHandles.Add(sceneHandle);
            operation.completed += _ =>
            {
                _unloadingSceneHandles.Remove(sceneHandle);
                InvokeSafely(completed);
            };
            return true;
        }

        public bool SetActiveScene(string sceneName)
        {
            Scene scene = FindLoadedScene(sceneName);
            if (scene.IsValid()
                && scene.isLoaded
                && UnitySceneManager.SetActiveScene(scene))
            {
                return true;
            }

            Debug.LogError($"[SceneManager] 无法设置活动场景 {sceneName}");
            return false;
        }

        public bool IsSceneLoaded(string sceneName)
        {
            Scene scene = FindLoadedScene(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        public Scene GetActiveScene()
        {
            return UnitySceneManager.GetActiveScene();
        }

        private void StartNextLoad()
        {
            if (_isShuttingDown
                || _loadCoroutine != null
                || !_loadQueue.TryBeginNext(out SceneLoadRequest request))
            {
                return;
            }

            request.MarkLoading();
            InvokeSafely(LoadStarted, request);
            NotifyProgress(request, true);
            _loadCoroutine = StartCoroutine(ExecuteLoad(request));
        }

        private IEnumerator ExecuteLoad(SceneLoadRequest request)
        {
            yield return null;

            float startedAt = Time.realtimeSinceStartup;
            AsyncOperation operation = null;
            string loadError = string.Empty;
            try
            {
                operation = UnitySceneManager.LoadSceneAsync(
                    request.SceneName,
                    request.Options.Mode);
            }
            catch (Exception exception)
            {
                loadError = exception.Message;
            }

            if (operation == null)
            {
                string error = string.IsNullOrEmpty(loadError)
                    ? "Unity 未能创建场景加载任务"
                    : $"Unity 创建场景加载任务失败 {loadError}";
                FinishFailedLoad(request, error);
                yield break;
            }

            _activeOperation = operation;
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                if (request.ReportProgress(operation.progress))
                {
                    NotifyProgress(request, false);
                }

                yield return null;
            }

            if (request.ReportProgress(0.9f))
            {
                NotifyProgress(request, false);
            }

            while (Time.realtimeSinceStartup - startedAt
                   < request.Options.MinimumDuration)
            {
                yield return null;
            }

            request.MarkActivating();
            if (request.ReportProgress(0.95f))
            {
                NotifyProgress(request, false);
            }

            operation.allowSceneActivation = true;
            while (!operation.isDone)
            {
                yield return null;
            }

            Scene loadedScene = FindLoadedScene(request.SceneName);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                FinishFailedLoad(request, $"场景加载完成但无法获取场景 {request.SceneName}");
                yield break;
            }

            if (request.Options.Mode == LoadSceneMode.Additive
                && request.Options.SetActiveAfterLoad
                && !UnitySceneManager.SetActiveScene(loadedScene))
            {
                FinishFailedLoad(request, $"场景已加载但无法设为活动场景 {request.SceneName}");
                yield break;
            }

            if (request.Options.UnloadUnusedAssetsAfterLoad)
            {
                yield return Resources.UnloadUnusedAssets();
            }

            FinishSuccessfulLoad(request, loadedScene);
        }

        private void FinishSuccessfulLoad(
            SceneLoadRequest request,
            Scene loadedScene)
        {
            request.MarkSucceeded();
            _activeOperation = null;
            NotifyProgress(request, false);
            InvokeSafely(request.CompletedCallback, loadedScene);
            InvokeSafely(LoadCompleted, request, loadedScene);
            CompleteCurrentAndContinue(request);
        }

        private void FinishFailedLoad(SceneLoadRequest request, string error)
        {
            request.MarkFailed(error);
            _activeOperation = null;
            Debug.LogError($"[SceneManager] {error}");
            InvokeSafely(request.FailedCallback, error);
            InvokeSafely(LoadFailed, request, error);
            CompleteCurrentAndContinue(request);
        }

        private void FailUnqueuedRequest(SceneLoadRequest request, string error)
        {
            request.MarkFailed(error);
            Debug.LogError($"[SceneManager] {error}");
            InvokeSafely(request.FailedCallback, error);
            InvokeSafely(LoadFailed, request, error);
        }

        private void CompleteCurrentAndContinue(SceneLoadRequest request)
        {
            _loadQueue.CompleteCurrent(request);
            _loadCoroutine = null;
            StartNextLoad();
        }

        private void NotifyProgress(SceneLoadRequest request, bool force)
        {
            if (!force && request.ProgressCallback == null
                && LoadProgressChanged == null)
            {
                return;
            }

            InvokeSafely(request.ProgressCallback, request.Progress);
            InvokeSafely(LoadProgressChanged, request, request.Progress);
        }

        private bool ReportUnloadFailure(
            string sceneName,
            string reason,
            Action<string> failed)
        {
            string error = $"{reason} {sceneName}".Trim();
            Debug.LogError($"[SceneManager] {error}");
            InvokeSafely(failed, error);
            return false;
        }

        private static Scene FindLoadedScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return default;
            }

            string normalized = sceneName.Trim();
            Scene scene = UnitySceneManager.GetSceneByPath(normalized);
            return scene.IsValid()
                ? scene
                : UnitySceneManager.GetSceneByName(normalized);
        }

        private int NextRequestId()
        {
            _nextRequestId = _nextRequestId == int.MaxValue
                ? 1
                : _nextRequestId + 1;
            return _nextRequestId;
        }

        private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InvokeSafely(SceneLoaded, scene, mode);
        }

        private void OnUnitySceneUnloaded(Scene scene)
        {
            InvokeSafely(SceneUnloaded, scene);
        }

        private void OnUnityActiveSceneChanged(Scene previous, Scene current)
        {
            InvokeSafely(ActiveSceneChanged, previous, current);
        }

        private static void InvokeSafely(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            Delegate[] listeners = callback.GetInvocationList();
            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action)listeners[i]).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void InvokeSafely<T>(Action<T> callback, T value)
        {
            if (callback == null)
            {
                return;
            }

            Delegate[] listeners = callback.GetInvocationList();
            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action<T>)listeners[i]).Invoke(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void InvokeSafely<T1, T2>(
            Action<T1, T2> callback,
            T1 value1,
            T2 value2)
        {
            if (callback == null)
            {
                return;
            }

            Delegate[] listeners = callback.GetInvocationList();
            for (int i = 0; i < listeners.Length; i++)
            {
                try
                {
                    ((Action<T1, T2>)listeners[i]).Invoke(value1, value2);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
