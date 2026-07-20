using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Assets
{
    /// <summary>
    /// AssetManager 使用的底层 AssetBundle 加载器
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    internal sealed class BundleLoader : MonoBehaviour
    {
        private readonly Dictionary<string, AssetBundle> _loadedBundles =
            new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<Action<AssetBundle>>> _loadingBundleCallbacks =
            new Dictionary<string, List<Action<AssetBundle>>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<Action<bool>> _manifestCallbacks = new List<Action<bool>>();

        private AssetBundle _manifestBundle;
        private AssetBundleManifest _manifest;
        private string _bundleRootOverride;
        private string _manifestBundleNameOverride;
        private bool _manifestLoading;
        private bool _isUnloading;

        internal bool IsInitialized => _manifest != null;
        internal string BundleRoot => string.IsNullOrWhiteSpace(_bundleRootOverride)
            ? AssetBundlePath.DefaultBundleRoot
            : _bundleRootOverride;
        internal string ManifestBundleName => string.IsNullOrWhiteSpace(_manifestBundleNameOverride)
            ? AssetBundlePath.DefaultManifestBundleName
            : _manifestBundleNameOverride;

        internal void Configure(string bundleRoot, string manifestBundleName)
        {
            if (_manifestLoading || _manifest != null || _loadedBundles.Count > 0)
            {
                throw new InvalidOperationException("BundleLoader 必须在加载资源前完成配置");
            }

            _bundleRootOverride = bundleRoot?.TrimEnd('/', '\\');
            _manifestBundleNameOverride = NormalizeBundleName(manifestBundleName);
        }

        internal void InitializeAsync(Action<bool> completed = null)
        {
            if (_isUnloading)
            {
                InvokeSafely(completed, false);
                return;
            }

            EnsureManifestLoadedAsync(completed);
        }

        internal Object LoadAsset(string bundleName, string assetName, Type assetType)
        {
            if (!CanLoadSynchronously())
            {
                Debug.LogError("[BundleLoader] 当前平台不支持同步读取 StreamingAssets 请使用异步接口");
                return null;
            }

            if (assetType == null)
            {
                Debug.LogError("[BundleLoader] 资源类型不能为空");
                return null;
            }

            AssetBundle bundle = LoadBundleSync(bundleName);
            if (bundle == null)
            {
                return null;
            }

            Object asset = bundle.LoadAsset(assetName, assetType);
            if (asset == null)
            {
                Debug.LogError($"[BundleLoader] 找不到资源 {bundleName}/{assetName}");
            }

            return asset;
        }

        internal void LoadAssetAsync(
            string bundleName,
            string assetName,
            Type assetType,
            Action<Object> completed)
        {
            if (string.IsNullOrWhiteSpace(bundleName)
                || string.IsNullOrWhiteSpace(assetName)
                || assetType == null)
            {
                Debug.LogError("[BundleLoader] Bundle 名称 资源名称和资源类型不能为空");
                InvokeSafely(completed, null);
                return;
            }

            if (_isUnloading)
            {
                InvokeSafely(completed, null);
                return;
            }

            LoadBundleAsync(bundleName, bundle =>
            {
                if (bundle == null)
                {
                    completed?.Invoke(null);
                    return;
                }

                StartCoroutine(LoadAssetCoroutine(bundle, assetName, assetType, completed));
            });
        }

        internal bool IsBundleLoaded(string bundleName)
        {
            return _loadedBundles.ContainsKey(NormalizeBundleName(bundleName));
        }

        internal void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            if (!_loadedBundles.TryGetValue(normalizedName, out AssetBundle bundle))
            {
                return;
            }

            bundle.Unload(unloadAllLoadedObjects);
            _loadedBundles.Remove(normalizedName);
        }

        internal void UnloadAll(bool unloadAllLoadedObjects = false)
        {
            _isUnloading = true;
            StopAllCoroutines();
            FailPendingCallbacks();

            foreach (KeyValuePair<string, AssetBundle> pair in _loadedBundles)
            {
                pair.Value?.Unload(unloadAllLoadedObjects);
            }

            _loadedBundles.Clear();
            _manifestBundle?.Unload(unloadAllLoadedObjects);
            _manifestBundle = null;
            _manifest = null;
            _manifestLoading = false;
            _isUnloading = false;
        }

        internal string[] GetAllDependencies(string bundleName)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            return _manifest == null || string.IsNullOrEmpty(normalizedName)
                ? Array.Empty<string>()
                : _manifest.GetAllDependencies(normalizedName);
        }

        internal string[] GetLoadedBundleNames()
        {
            string[] names = new string[_loadedBundles.Count];
            _loadedBundles.Keys.CopyTo(names, 0);
            return names;
        }

        private void OnDestroy()
        {
            UnloadAll(false);
        }

        private AssetBundle LoadBundleSync(string bundleName)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                return null;
            }

            if (_loadedBundles.TryGetValue(normalizedName, out AssetBundle cachedBundle))
            {
                return cachedBundle;
            }

            if (!EnsureManifestLoadedSync())
            {
                return null;
            }

            string[] dependencies = _manifest.GetAllDependencies(normalizedName);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (LoadSingleBundleSync(dependencies[i]) == null)
                {
                    return null;
                }
            }

            return LoadSingleBundleSync(normalizedName);
        }

        private AssetBundle LoadSingleBundleSync(string bundleName)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            if (_loadedBundles.TryGetValue(normalizedName, out AssetBundle cachedBundle))
            {
                return cachedBundle;
            }

            string path = GetBundlePath(normalizedName);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Debug.LogError($"[BundleLoader] 加载 Bundle 失败 {path}");
                return null;
            }

            _loadedBundles.Add(normalizedName, bundle);
            return bundle;
        }

        private bool EnsureManifestLoadedSync()
        {
            if (_manifest != null)
            {
                return true;
            }

            string manifestPath = GetBundlePath(ManifestBundleName);
            _manifestBundle = AssetBundle.LoadFromFile(manifestPath);
            if (_manifestBundle == null)
            {
                Debug.LogError($"[BundleLoader] 加载 Manifest Bundle 失败 {manifestPath}");
                return false;
            }

            _manifest = _manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            if (_manifest == null)
            {
                Debug.LogError($"[BundleLoader] Bundle 中缺少 AssetBundleManifest {manifestPath}");
                _manifestBundle.Unload(true);
                _manifestBundle = null;
                return false;
            }

            return true;
        }

        private void LoadBundleAsync(string bundleName, Action<AssetBundle> completed)
        {
            if (_isUnloading)
            {
                InvokeSafely(completed, null);
                return;
            }

            string normalizedName = NormalizeBundleName(bundleName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                completed?.Invoke(null);
                return;
            }

            if (_loadedBundles.TryGetValue(normalizedName, out AssetBundle cachedBundle))
            {
                completed?.Invoke(cachedBundle);
                return;
            }

            if (_loadingBundleCallbacks.TryGetValue(normalizedName, out List<Action<AssetBundle>> callbacks))
            {
                callbacks.Add(completed);
                return;
            }

            _loadingBundleCallbacks.Add(normalizedName, new List<Action<AssetBundle>> { completed });
            StartCoroutine(LoadBundleWithDependenciesCoroutine(normalizedName));
        }

        private IEnumerator LoadBundleWithDependenciesCoroutine(string bundleName)
        {
            bool manifestCompleted = false;
            bool manifestSucceeded = false;
            EnsureManifestLoadedAsync(success =>
            {
                manifestSucceeded = success;
                manifestCompleted = true;
            });
            yield return new WaitUntil(() => manifestCompleted);

            if (!manifestSucceeded)
            {
                CompleteBundleLoad(bundleName, null);
                yield break;
            }

            string[] dependencies = _manifest.GetAllDependencies(bundleName);
            for (int i = 0; i < dependencies.Length; i++)
            {
                bool dependencyCompleted = false;
                AssetBundle dependencyBundle = null;
                LoadBundleAsync(dependencies[i], bundle =>
                {
                    dependencyBundle = bundle;
                    dependencyCompleted = true;
                });
                yield return new WaitUntil(() => dependencyCompleted);

                if (dependencyBundle == null)
                {
                    CompleteBundleLoad(bundleName, null);
                    yield break;
                }
            }

            AssetBundle loadedBundle = null;
            yield return LoadBundleFileCoroutine(bundleName, bundle => loadedBundle = bundle);
            CompleteBundleLoad(bundleName, loadedBundle);
        }

        private void EnsureManifestLoadedAsync(Action<bool> completed)
        {
            if (_manifest != null)
            {
                completed?.Invoke(true);
                return;
            }

            _manifestCallbacks.Add(completed);
            if (_manifestLoading)
            {
                return;
            }

            _manifestLoading = true;
            StartCoroutine(LoadManifestCoroutine());
        }

        private IEnumerator LoadManifestCoroutine()
        {
            AssetBundle loadedManifestBundle = null;
            yield return LoadBundleFileCoroutine(
                ManifestBundleName,
                bundle => loadedManifestBundle = bundle,
                true);

            _manifestBundle = loadedManifestBundle;
            _manifest = _manifestBundle != null
                ? _manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest")
                : null;

            bool success = _manifest != null;
            if (!success)
            {
                Debug.LogError($"[BundleLoader] AssetBundleManifest 加载失败 {GetBundlePath(ManifestBundleName)}");
                _manifestBundle?.Unload(true);
                _manifestBundle = null;
            }

            _manifestLoading = false;
            Action<bool>[] callbacks = _manifestCallbacks.ToArray();
            _manifestCallbacks.Clear();
            for (int i = 0; i < callbacks.Length; i++)
            {
                InvokeSafely(callbacks[i], success);
            }
        }

        private IEnumerator LoadBundleFileCoroutine(
            string bundleName,
            Action<AssetBundle> completed,
            bool isManifestBundle = false)
        {
            string path = GetBundlePath(bundleName);
            AssetBundle bundle = null;

            if (RequiresWebRequest(path))
            {
                using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(path))
                {
                    yield return request.SendWebRequest();
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        bundle = DownloadHandlerAssetBundle.GetContent(request);
                    }
                    else
                    {
                        Debug.LogError($"[BundleLoader] Bundle 请求失败 {path}\n{request.error}");
                    }
                }
            }
            else
            {
                AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
                yield return request;
                bundle = request.assetBundle;
                if (bundle == null)
                {
                    Debug.LogError($"[BundleLoader] 加载 Bundle 失败 {path}");
                }
            }

            if (!isManifestBundle && bundle != null)
            {
                string normalizedName = NormalizeBundleName(bundleName);
                if (_loadedBundles.TryGetValue(normalizedName, out AssetBundle existingBundle))
                {
                    bundle.Unload(false);
                    bundle = existingBundle;
                }
                else
                {
                    _loadedBundles.Add(normalizedName, bundle);
                }
            }

            completed?.Invoke(bundle);
        }

        private static IEnumerator LoadAssetCoroutine(
            AssetBundle bundle,
            string assetName,
            Type assetType,
            Action<Object> completed)
        {
            AssetBundleRequest request = bundle.LoadAssetAsync(assetName, assetType);
            yield return request;
            Object asset = request.asset;
            if (asset == null)
            {
                Debug.LogError($"[BundleLoader] 找不到资源 {bundle.name}/{assetName}");
            }

            InvokeSafely(completed, asset);
        }

        private void CompleteBundleLoad(string bundleName, AssetBundle bundle)
        {
            if (!_loadingBundleCallbacks.TryGetValue(bundleName, out List<Action<AssetBundle>> callbacks))
            {
                return;
            }

            _loadingBundleCallbacks.Remove(bundleName);
            Action<AssetBundle>[] callbackArray = callbacks.ToArray();
            for (int i = 0; i < callbackArray.Length; i++)
            {
                InvokeSafely(callbackArray[i], bundle);
            }
        }

        private void FailPendingCallbacks()
        {
            List<Action<AssetBundle>> pendingBundleCallbacks = new List<Action<AssetBundle>>();
            foreach (KeyValuePair<string, List<Action<AssetBundle>>> pair in _loadingBundleCallbacks)
            {
                pendingBundleCallbacks.AddRange(pair.Value);
            }

            _loadingBundleCallbacks.Clear();
            for (int i = 0; i < pendingBundleCallbacks.Count; i++)
            {
                InvokeSafely(pendingBundleCallbacks[i], null);
            }

            Action<bool>[] manifestCallbacks = _manifestCallbacks.ToArray();
            _manifestCallbacks.Clear();
            for (int i = 0; i < manifestCallbacks.Length; i++)
            {
                InvokeSafely(manifestCallbacks[i], false);
            }
        }

        private string GetBundlePath(string bundleName)
        {
            return AssetBundlePath.Join(BundleRoot, NormalizeBundleName(bundleName));
        }

        private static string NormalizeBundleName(string bundleName)
        {
            return AssetBundlePath.NormalizeBundleName(bundleName);
        }

        private static bool RequiresWebRequest(string path)
        {
            return path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase)
                   || path.Contains("://");
        }

        private static bool CanLoadSynchronously()
        {
            return Application.platform != RuntimePlatform.Android
                   && Application.platform != RuntimePlatform.WebGLPlayer;
        }

        private static void InvokeSafely<T>(Action<T> callback, T value)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback.Invoke(value);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
