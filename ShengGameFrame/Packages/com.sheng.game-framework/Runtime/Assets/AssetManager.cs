using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Sheng.GameFramework.Core;
using UnityEngine;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sheng.GameFramework.Assets
{
    /// <summary>
    /// 统一管理 Asset 和 AssetBundle 的加载 缓存 引用和卸载
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssetManager : PersistentMonoSingleton<AssetManager>
    {
        private sealed class AssetEntry
        {
            public string Key;
            public string BundleName;
            public string AssetName;
            public Type AssetType;
            public Object Asset;
            public int ReferenceCount;
            public AssetCachePolicy CachePolicy;
            public string[] RetainedBundles = Array.Empty<string>();
        }

        private sealed class PendingAssetLoad
        {
            public int Generation;
            public string Key;
            public string BundleName;
            public string AssetName;
            public Type AssetType;
            public AssetCachePolicy CachePolicy;
            public readonly List<Action<AssetEntry>> Callbacks =
                new List<Action<AssetEntry>>();
        }

        [SerializeField] private AssetManagerSettings settings = new AssetManagerSettings();

        private readonly Dictionary<string, AssetEntry> _assets =
            new Dictionary<string, AssetEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingAssetLoad> _pendingLoads =
            new Dictionary<string, PendingAssetLoad>(StringComparer.Ordinal);
        private readonly Queue<PendingAssetLoad> _loadQueue = new Queue<PendingAssetLoad>();
        private readonly Dictionary<string, int> _bundleReferenceCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, List<IAssetHandle>> _ownedAssetHandles =
            new Dictionary<int, List<IAssetHandle>>();
        private readonly Dictionary<int, AssetInstanceHandle> _ownedInstanceHandles =
            new Dictionary<int, AssetInstanceHandle>();

        private BundleLoader _bundleLoader;
        private int _activeLoadCount;
        private int _generation;
        private int _scheduleFrame = -1;
        private int _loadsStartedThisFrame;
        private bool _isScheduling;
        private bool _isUnloading;

        public AssetManagerSettings Settings => settings;
        public int LoadedAssetCount => _assets.Count;
        public int ActiveLoadCount => _activeLoadCount;
        public int QueuedLoadCount => _loadQueue.Count;
        public AssetLoadMode EffectiveLoadMode => ResolveLoadMode(settings.LoadMode);
        public bool IsInitialized => EffectiveLoadMode == AssetLoadMode.EditorDatabase
                                     || (_bundleLoader != null && _bundleLoader.IsInitialized);
        public string BundleRoot
        {
            get
            {
                EnsureBundleLoader();
                return _bundleLoader.BundleRoot;
            }
        }
        public string ManifestBundleName
        {
            get
            {
                EnsureBundleLoader();
                return _bundleLoader.ManifestBundleName;
            }
        }

        protected override void OnSingletonAwake()
        {
            settings ??= new AssetManagerSettings();
            settings.Sanitize();
            EnsureBundleLoader();
        }

        protected override void OnSingletonDestroyed()
        {
            UnloadAll(false);
        }

        private void Update()
        {
            RefreshFrameBudget();
            TryStartPendingLoads();
        }

        public void Configure(
            AssetManagerSettings newSettings,
            string bundleRoot = null,
            string manifestBundleName = null)
        {
            if (HasStartedLoading())
            {
                throw new InvalidOperationException("AssetManager 必须在加载资源前完成配置");
            }

            if (newSettings != null)
            {
                settings.CopyFrom(newSettings);
            }

            EnsureBundleLoader();
            _bundleLoader.Configure(bundleRoot, manifestBundleName);
        }

        public void ConfigureBundlePath(string bundleRoot, string manifestBundleName)
        {
            Configure(null, bundleRoot, manifestBundleName);
        }

        public void InitializeAsync(Action<bool> completed = null)
        {
            if (_isUnloading)
            {
                InvokeSafely(completed, false);
                return;
            }

            if (EffectiveLoadMode == AssetLoadMode.EditorDatabase)
            {
                InvokeSafely(completed, true);
                return;
            }

            EnsureBundleLoader();
            _bundleLoader.InitializeAsync(completed);
        }

        public T LoadAsset<T>(
            string bundleName,
            string assetName,
            AssetCachePolicy? cachePolicy = null)
            where T : Object
        {
            AssetHandle<T> handle = LoadAssetHandle<T>(
                bundleName,
                assetName,
                cachePolicy);
            if (handle == null || handle.Asset == null)
            {
                handle?.Dispose();
                LogLoadFailure(bundleName, assetName, typeof(T));
                return null;
            }

            T asset = handle.Asset;
            TrackAssetHandle(asset, handle);
            return asset;
        }

        public void LoadAssetAsync<T>(
            string bundleName,
            string assetName,
            Action<T> completed,
            Action failed = null,
            AssetCachePolicy? cachePolicy = null)
            where T : Object
        {
            if (completed == null)
            {
                Debug.LogError("[AssetManager] 异步加载必须提供完成回调");
                return;
            }

            LoadAssetHandleAsync<T>(
                bundleName,
                assetName,
                handle =>
                {
                    if (handle == null || handle.Asset == null)
                    {
                        handle?.Dispose();
                        LogLoadFailure(bundleName, assetName, typeof(T));
                        InvokeSafely(failed);
                        return;
                    }

                    T asset = handle.Asset;
                    TrackAssetHandle(asset, handle);
                    try
                    {
                        completed.Invoke(asset);
                    }
                    catch (Exception exception)
                    {
                        ReleaseTrackedAssetHandle(asset, handle);
                        Debug.LogException(exception);
                    }
                },
                cachePolicy);
        }

        public void LoadAssetAsync(
            string bundleName,
            string assetName,
            Action completed,
            Action failed = null,
            AssetCachePolicy? cachePolicy = null)
        {
            LoadAssetAsync<GameObject>(
                bundleName,
                assetName,
                asset => InvokeSafely(completed),
                failed,
                cachePolicy);
        }

        public GameObject Instantiate(
            string bundleName,
            string assetName,
            Transform parent = null,
            bool worldPositionStays = false,
            AssetCachePolicy? cachePolicy = null)
        {
            AssetHandle<GameObject> prefabHandle = LoadAssetHandle<GameObject>(
                bundleName,
                assetName,
                cachePolicy);
            if (prefabHandle == null || prefabHandle.Asset == null)
            {
                prefabHandle?.Dispose();
                LogLoadFailure(bundleName, assetName, typeof(GameObject));
                return null;
            }

            try
            {
                GameObject instance = CreateInstance(
                    prefabHandle.Asset,
                    parent,
                    worldPositionStays);
                TrackInstance(new AssetInstanceHandle(instance, prefabHandle));
                return instance;
            }
            catch (Exception exception)
            {
                prefabHandle.Dispose();
                Debug.LogException(exception);
                return null;
            }
        }

        public void InstantiateAsync(
            string bundleName,
            string assetName,
            Action<GameObject> completed,
            Transform parent = null,
            bool worldPositionStays = false,
            AssetCachePolicy? cachePolicy = null,
            Action failed = null)
        {
            if (completed == null)
            {
                Debug.LogError("[AssetManager] 异步实例化必须提供完成回调");
                return;
            }

            LoadAssetHandleAsync<GameObject>(
                bundleName,
                assetName,
                prefabHandle =>
                {
                    if (prefabHandle == null || prefabHandle.Asset == null)
                    {
                        prefabHandle?.Dispose();
                        LogLoadFailure(bundleName, assetName, typeof(GameObject));
                        InvokeSafely(failed);
                        return;
                    }

                    GameObject instance = null;
                    try
                    {
                        instance = CreateInstance(
                            prefabHandle.Asset,
                            parent,
                            worldPositionStays);
                        TrackInstance(new AssetInstanceHandle(instance, prefabHandle));
                        completed.Invoke(instance);
                    }
                    catch (Exception exception)
                    {
                        if (instance != null)
                        {
                            ReleaseInstance(instance);
                        }
                        else
                        {
                            prefabHandle.Dispose();
                        }

                        Debug.LogException(exception);
                    }
                },
                cachePolicy);
        }

        public bool ReleaseAsset(Object asset)
        {
            if (asset == null)
            {
                return false;
            }

            int instanceId = asset.GetInstanceID();
            if (!_ownedAssetHandles.TryGetValue(
                    instanceId,
                    out List<IAssetHandle> handles)
                || handles.Count == 0)
            {
                return false;
            }

            int lastIndex = handles.Count - 1;
            IAssetHandle handle = handles[lastIndex];
            handles.RemoveAt(lastIndex);
            if (handles.Count == 0)
            {
                _ownedAssetHandles.Remove(instanceId);
            }

            handle.Dispose();
            return true;
        }

        public bool ReleaseAsset<T>(string bundleName, string assetName)
            where T : Object
        {
            string key = CreateAssetKey(
                NormalizeBundleName(bundleName),
                assetName?.Trim() ?? string.Empty,
                typeof(T));
            return _assets.TryGetValue(key, out AssetEntry entry)
                   && entry.Asset != null
                   && ReleaseAsset(entry.Asset);
        }

        public bool ReleaseInstance(
            GameObject instance,
            bool destroyInstance = true)
        {
            if (instance == null)
            {
                return false;
            }

            int instanceId = instance.GetInstanceID();
            if (!_ownedInstanceHandles.TryGetValue(
                    instanceId,
                    out AssetInstanceHandle handle))
            {
                return false;
            }

            _ownedInstanceHandles.Remove(instanceId);
            AssetInstanceTracker tracker = instance.GetComponent<AssetInstanceTracker>();
            tracker?.Detach();
            handle.Release(destroyInstance);
            return true;
        }

        internal AssetHandle<T> LoadAssetHandle<T>(
            string bundleName,
            string assetName,
            AssetCachePolicy? cachePolicy = null)
            where T : Object
        {
            if (!TryNormalizeRequest(
                    bundleName,
                    assetName,
                    typeof(T),
                    out string normalizedBundleName,
                    out string normalizedAssetName))
            {
                return null;
            }

            string key = CreateAssetKey(normalizedBundleName, normalizedAssetName, typeof(T));
            if (TryGetValidEntry(key, out AssetEntry cachedEntry))
            {
                UpgradeCachePolicy(cachedEntry, cachePolicy);
                return Retain<T>(cachedEntry);
            }

            if (_pendingLoads.ContainsKey(key))
            {
                Debug.LogError($"[AssetManager] 资源正在异步加载 无法同步获取 {normalizedBundleName}/{normalizedAssetName}");
                return null;
            }

            Object asset = LoadRawAsset(
                normalizedBundleName,
                normalizedAssetName,
                typeof(T));
            if (asset == null)
            {
                return null;
            }

            AssetEntry entry = CreateEntry(
                key,
                normalizedBundleName,
                normalizedAssetName,
                typeof(T),
                asset,
                cachePolicy ?? settings.DefaultCachePolicy,
                1);
            return CreateHandle<T>(entry);
        }

        internal void LoadAssetHandleAsync<T>(
            string bundleName,
            string assetName,
            Action<AssetHandle<T>> completed,
            AssetCachePolicy? cachePolicy = null)
            where T : Object
        {
            if (completed == null)
            {
                Debug.LogError("[AssetManager] 异步加载必须提供完成回调以接收资源句柄");
                return;
            }

            if (_isUnloading
                || !TryNormalizeRequest(
                    bundleName,
                    assetName,
                    typeof(T),
                    out string normalizedBundleName,
                    out string normalizedAssetName))
            {
                InvokeSafely(completed, null);
                return;
            }

            string key = CreateAssetKey(normalizedBundleName, normalizedAssetName, typeof(T));
            if (TryGetValidEntry(key, out AssetEntry cachedEntry))
            {
                UpgradeCachePolicy(cachedEntry, cachePolicy);
                DeliverHandle(completed, Retain<T>(cachedEntry));
                return;
            }

            if (_pendingLoads.TryGetValue(key, out PendingAssetLoad pendingLoad))
            {
                UpgradeCachePolicy(pendingLoad, cachePolicy);
                AddPendingCallback(pendingLoad, completed);
                return;
            }

            PendingAssetLoad newLoad = new PendingAssetLoad
            {
                Generation = _generation,
                Key = key,
                BundleName = normalizedBundleName,
                AssetName = normalizedAssetName,
                AssetType = typeof(T),
                CachePolicy = cachePolicy ?? settings.DefaultCachePolicy
            };
            AddPendingCallback(newLoad, completed);
            _pendingLoads.Add(key, newLoad);
            _loadQueue.Enqueue(newLoad);
            TryStartPendingLoads();
        }

        public int GetAssetReferenceCount<T>(string bundleName, string assetName)
            where T : Object
        {
            string key = CreateAssetKey(
                NormalizeBundleName(bundleName),
                assetName?.Trim() ?? string.Empty,
                typeof(T));
            return _assets.TryGetValue(key, out AssetEntry entry)
                ? entry.ReferenceCount
                : 0;
        }

        public int GetBundleReferenceCount(string bundleName)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            return _bundleReferenceCounts.TryGetValue(normalizedName, out int count)
                ? count
                : 0;
        }

        public bool IsBundleLoaded(string bundleName)
        {
            EnsureBundleLoader();
            return _bundleLoader.IsBundleLoaded(bundleName);
        }

        public int ClearUnusedAssets(bool includeKeepLoaded = true)
        {
            List<string> keysToRemove = new List<string>();
            foreach (KeyValuePair<string, AssetEntry> pair in _assets)
            {
                AssetEntry entry = pair.Value;
                if (entry.ReferenceCount == 0
                    && (includeKeepLoaded
                        || entry.CachePolicy == AssetCachePolicy.ReferenceCounted))
                {
                    keysToRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                RemoveEntry(keysToRemove[i]);
            }

            return keysToRemove.Count;
        }

        public int UnloadUnusedBundles()
        {
            EnsureBundleLoader();
            string[] loadedBundleNames = _bundleLoader.GetLoadedBundleNames();
            int unloadedCount = 0;
            for (int i = 0; i < loadedBundleNames.Length; i++)
            {
                string bundleName = loadedBundleNames[i];
                if (GetBundleReferenceCount(bundleName) > 0)
                {
                    continue;
                }

                _bundleLoader.UnloadBundle(bundleName, false);
                unloadedCount++;
            }

            return unloadedCount;
        }

        public bool UnloadBundle(
            string bundleName,
            bool unloadAllLoadedObjects = false)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                return false;
            }

            int referenceCount = GetBundleReferenceCount(normalizedName);
            if (referenceCount > 0)
            {
                Debug.LogWarning($"[AssetManager] Bundle 仍被 {referenceCount} 个资源缓存引用 无法卸载 {normalizedName}");
                return false;
            }

            EnsureBundleLoader();
            if (!_bundleLoader.IsBundleLoaded(normalizedName))
            {
                return false;
            }

            _bundleLoader.UnloadBundle(normalizedName, unloadAllLoadedObjects);
            return true;
        }

        public void UnloadUnusedAssetsAsync(
            Action completed = null,
            bool includeKeepLoaded = true)
        {
            ClearUnusedAssets(includeKeepLoaded);
            UnloadUnusedBundles();
            StartCoroutine(UnloadUnusedAssetsCoroutine(completed));
        }

        public void UnloadAll(bool unloadAllLoadedObjects = false)
        {
            if (_isUnloading)
            {
                return;
            }

            _isUnloading = true;
            _generation++;
            CancelPendingLoads();
            ReleaseOwnedReferences();
            _assets.Clear();
            _bundleReferenceCounts.Clear();
            _activeLoadCount = 0;
            _loadsStartedThisFrame = 0;
            if (_bundleLoader != null)
            {
                _bundleLoader.UnloadAll(unloadAllLoadedObjects);
            }

            _isUnloading = false;
        }

        public AssetManagerDebugSnapshot GetDebugSnapshot()
        {
            AssetManagerDebugSnapshot snapshot = new AssetManagerDebugSnapshot
            {
                EffectiveLoadMode = EffectiveLoadMode,
                ActiveLoadCount = _activeLoadCount,
                QueuedLoadCount = _loadQueue.Count
            };

            foreach (AssetEntry entry in _assets.Values)
            {
                snapshot.Assets.Add(new AssetDebugInfo
                {
                    BundleName = entry.BundleName,
                    AssetName = entry.AssetName,
                    AssetType = entry.AssetType.Name,
                    ReferenceCount = entry.ReferenceCount,
                    CachePolicy = entry.CachePolicy
                });
            }

            snapshot.Assets.Sort((left, right) => string.Compare(
                left.BundleName + "/" + left.AssetName,
                right.BundleName + "/" + right.AssetName,
                StringComparison.Ordinal));

            EnsureBundleLoader();
            string[] loadedBundleNames = _bundleLoader.GetLoadedBundleNames();
            for (int i = 0; i < loadedBundleNames.Length; i++)
            {
                string bundleName = loadedBundleNames[i];
                snapshot.Bundles.Add(new BundleDebugInfo
                {
                    BundleName = bundleName,
                    ReferenceCount = GetBundleReferenceCount(bundleName)
                });
            }

            snapshot.Bundles.Sort((left, right) => string.Compare(
                left.BundleName,
                right.BundleName,
                StringComparison.OrdinalIgnoreCase));
            return snapshot;
        }

        internal void ReleaseEntry(string entryKey)
        {
            if (string.IsNullOrEmpty(entryKey)
                || !_assets.TryGetValue(entryKey, out AssetEntry entry))
            {
                return;
            }

            entry.ReferenceCount = Mathf.Max(0, entry.ReferenceCount - 1);
            Log($"释放资源 {entry.BundleName}/{entry.AssetName} 引用 {entry.ReferenceCount}");
            if (entry.ReferenceCount == 0
                && entry.CachePolicy == AssetCachePolicy.ReferenceCounted)
            {
                RemoveEntry(entryKey);
            }
        }

        internal void NotifyInstanceDestroyed(int instanceId)
        {
            if (!_ownedInstanceHandles.TryGetValue(
                    instanceId,
                    out AssetInstanceHandle handle))
            {
                return;
            }

            _ownedInstanceHandles.Remove(instanceId);
            handle.Release(false);
        }

        public static AssetLoadMode ResolveLoadMode(AssetLoadMode requestedMode)
        {
            if (requestedMode == AssetLoadMode.Auto)
            {
#if UNITY_EDITOR
                return AssetLoadMode.EditorDatabase;
#else
                return AssetLoadMode.AssetBundle;
#endif
            }

            if (requestedMode == AssetLoadMode.EditorDatabase)
            {
#if UNITY_EDITOR
                return AssetLoadMode.EditorDatabase;
#else
                return AssetLoadMode.AssetBundle;
#endif
            }

            return AssetLoadMode.AssetBundle;
        }

        private void EnsureBundleLoader()
        {
            if (_bundleLoader != null)
            {
                return;
            }

            _bundleLoader = GetComponent<BundleLoader>();
            if (_bundleLoader == null)
            {
                _bundleLoader = gameObject.AddComponent<BundleLoader>();
            }

            _bundleLoader.hideFlags |= HideFlags.HideInInspector;
        }

        private bool HasStartedLoading()
        {
            EnsureBundleLoader();
            return _assets.Count > 0
                   || _pendingLoads.Count > 0
                   || _activeLoadCount > 0
                   || _bundleLoader.IsInitialized
                   || _bundleLoader.GetLoadedBundleNames().Length > 0;
        }

        private bool TryNormalizeRequest(
            string bundleName,
            string assetName,
            Type assetType,
            out string normalizedBundleName,
            out string normalizedAssetName)
        {
            normalizedBundleName = NormalizeBundleName(bundleName);
            normalizedAssetName = assetName?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(normalizedBundleName)
                || string.IsNullOrEmpty(normalizedAssetName)
                || assetType == null)
            {
                Debug.LogError("[AssetManager] Bundle 名称 资源名称和资源类型不能为空");
                return false;
            }

            return true;
        }

        private bool TryGetValidEntry(string key, out AssetEntry entry)
        {
            if (_assets.TryGetValue(key, out entry) && entry.Asset != null)
            {
                return true;
            }

            if (entry != null)
            {
                RemoveEntry(key);
            }

            entry = null;
            return false;
        }

        private AssetEntry CreateEntry(
            string key,
            string bundleName,
            string assetName,
            Type assetType,
            Object asset,
            AssetCachePolicy cachePolicy,
            int initialReferenceCount)
        {
            AssetEntry entry = new AssetEntry
            {
                Key = key,
                BundleName = bundleName,
                AssetName = assetName,
                AssetType = assetType,
                Asset = asset,
                ReferenceCount = Mathf.Max(0, initialReferenceCount),
                CachePolicy = cachePolicy
            };

            if (EffectiveLoadMode == AssetLoadMode.AssetBundle)
            {
                entry.RetainedBundles = RetainBundleHierarchy(bundleName);
            }

            _assets.Add(key, entry);
            Log($"缓存资源 {bundleName}/{assetName} 引用 {entry.ReferenceCount}");
            return entry;
        }

        private AssetHandle<T> Retain<T>(AssetEntry entry) where T : Object
        {
            entry.ReferenceCount++;
            Log($"复用资源 {entry.BundleName}/{entry.AssetName} 引用 {entry.ReferenceCount}");
            return CreateHandle<T>(entry);
        }

        private AssetHandle<T> CreateHandle<T>(AssetEntry entry) where T : Object
        {
            return new AssetHandle<T>(
                this,
                entry.Key,
                entry.BundleName,
                entry.AssetName,
                entry.Asset as T);
        }

        private static GameObject CreateInstance(
            GameObject prefab,
            Transform parent,
            bool worldPositionStays)
        {
            GameObject instance = parent == null
                ? Object.Instantiate(prefab)
                : Object.Instantiate(prefab, parent, worldPositionStays);
            instance.name = prefab.name;
            return instance;
        }

        private void TrackAssetHandle(Object asset, IAssetHandle handle)
        {
            int instanceId = asset.GetInstanceID();
            if (!_ownedAssetHandles.TryGetValue(
                    instanceId,
                    out List<IAssetHandle> handles))
            {
                handles = new List<IAssetHandle>();
                _ownedAssetHandles.Add(instanceId, handles);
            }

            handles.Add(handle);
        }

        private void ReleaseTrackedAssetHandle(Object asset, IAssetHandle handle)
        {
            if (asset != null
                && _ownedAssetHandles.TryGetValue(
                    asset.GetInstanceID(),
                    out List<IAssetHandle> handles))
            {
                handles.Remove(handle);
                if (handles.Count == 0)
                {
                    _ownedAssetHandles.Remove(asset.GetInstanceID());
                }
            }

            handle?.Dispose();
        }

        private void TrackInstance(AssetInstanceHandle handle)
        {
            GameObject instance = handle.Instance;
            int instanceId = instance.GetInstanceID();
            _ownedInstanceHandles.Add(instanceId, handle);

            AssetInstanceTracker tracker =
                instance.GetComponent<AssetInstanceTracker>();
            if (tracker == null)
            {
                tracker = instance.AddComponent<AssetInstanceTracker>();
            }

            tracker.hideFlags |= HideFlags.HideInInspector;
            tracker.Initialize(this, instanceId);
        }

        private void ReleaseOwnedReferences()
        {
            AssetInstanceHandle[] instanceHandles =
                new AssetInstanceHandle[_ownedInstanceHandles.Count];
            _ownedInstanceHandles.Values.CopyTo(instanceHandles, 0);
            _ownedInstanceHandles.Clear();
            for (int i = 0; i < instanceHandles.Length; i++)
            {
                GameObject instance = instanceHandles[i].Instance;
                if (instance != null)
                {
                    instance.GetComponent<AssetInstanceTracker>()?.Detach();
                }

                instanceHandles[i].Release(false);
            }

            List<IAssetHandle> assetHandles = new List<IAssetHandle>();
            foreach (List<IAssetHandle> handles in _ownedAssetHandles.Values)
            {
                assetHandles.AddRange(handles);
            }

            _ownedAssetHandles.Clear();
            for (int i = 0; i < assetHandles.Count; i++)
            {
                assetHandles[i].Dispose();
            }
        }

        private static void LogLoadFailure(
            string bundleName,
            string assetName,
            Type assetType)
        {
            Debug.LogError(
                $"[AssetManager] 加载失败 {bundleName}/{assetName} 类型 {assetType?.Name}");
        }

        private void RemoveEntry(string key)
        {
            if (!_assets.TryGetValue(key, out AssetEntry entry))
            {
                return;
            }

            _assets.Remove(key);
            ReleaseBundleHierarchy(entry.RetainedBundles);
            Log($"移除资源缓存 {entry.BundleName}/{entry.AssetName}");
        }

        private Object LoadRawAsset(string bundleName, string assetName, Type assetType)
        {
            if (EffectiveLoadMode == AssetLoadMode.EditorDatabase)
            {
                return LoadEditorAsset(bundleName, assetName, assetType);
            }

            EnsureBundleLoader();
            return _bundleLoader.LoadAsset(bundleName, assetName, assetType);
        }

        private void LoadRawAssetAsync(PendingAssetLoad pendingLoad)
        {
            if (EffectiveLoadMode == AssetLoadMode.EditorDatabase)
            {
                Object editorAsset = LoadEditorAsset(
                    pendingLoad.BundleName,
                    pendingLoad.AssetName,
                    pendingLoad.AssetType);
                CompletePendingLoad(pendingLoad, editorAsset);
                return;
            }

            EnsureBundleLoader();
            _bundleLoader.LoadAssetAsync(
                pendingLoad.BundleName,
                pendingLoad.AssetName,
                pendingLoad.AssetType,
                asset => CompletePendingLoad(pendingLoad, asset));
        }

        private void TryStartPendingLoads()
        {
            if (_isUnloading || _isScheduling || _loadQueue.Count == 0)
            {
                return;
            }

            RefreshFrameBudget();
            _isScheduling = true;
            try
            {
                while (_loadQueue.Count > 0
                       && _activeLoadCount < settings.MaxConcurrentLoads
                       && (!Application.isPlaying
                           || _loadsStartedThisFrame < settings.MaxLoadsPerFrame))
                {
                    PendingAssetLoad pendingLoad = _loadQueue.Dequeue();
                    if (!_pendingLoads.TryGetValue(pendingLoad.Key, out PendingAssetLoad current)
                        || current != pendingLoad)
                    {
                        continue;
                    }

                    _activeLoadCount++;
                    _loadsStartedThisFrame++;
                    LoadRawAssetAsync(pendingLoad);
                }
            }
            finally
            {
                _isScheduling = false;
            }
        }

        private void CompletePendingLoad(PendingAssetLoad pendingLoad, Object asset)
        {
            if (pendingLoad.Generation != _generation
                || !_pendingLoads.TryGetValue(pendingLoad.Key, out PendingAssetLoad current)
                || current != pendingLoad)
            {
                return;
            }

            _pendingLoads.Remove(pendingLoad.Key);
            _activeLoadCount = Mathf.Max(0, _activeLoadCount - 1);

            AssetEntry entry = null;
            if (asset != null)
            {
                entry = CreateEntry(
                    pendingLoad.Key,
                    pendingLoad.BundleName,
                    pendingLoad.AssetName,
                    pendingLoad.AssetType,
                    asset,
                    pendingLoad.CachePolicy,
                    pendingLoad.Callbacks.Count);
            }

            Action<AssetEntry>[] callbacks = pendingLoad.Callbacks.ToArray();
            pendingLoad.Callbacks.Clear();
            for (int i = 0; i < callbacks.Length; i++)
            {
                InvokeSafely(callbacks[i], entry);
            }

            TryStartPendingLoads();
        }

        private void AddPendingCallback<T>(
            PendingAssetLoad pendingLoad,
            Action<AssetHandle<T>> completed)
            where T : Object
        {
            pendingLoad.Callbacks.Add(entry =>
            {
                AssetHandle<T> handle = entry != null ? CreateHandle<T>(entry) : null;
                DeliverHandle(completed, handle);
            });
        }

        private static void DeliverHandle<T>(
            Action<AssetHandle<T>> completed,
            AssetHandle<T> handle)
            where T : Object
        {
            try
            {
                completed.Invoke(handle);
            }
            catch (Exception exception)
            {
                handle?.Dispose();
                Debug.LogException(exception);
            }
        }

        private static void UpgradeCachePolicy(
            AssetEntry entry,
            AssetCachePolicy? requestedPolicy)
        {
            if (requestedPolicy == AssetCachePolicy.KeepLoaded)
            {
                entry.CachePolicy = AssetCachePolicy.KeepLoaded;
            }
        }

        private static void UpgradeCachePolicy(
            PendingAssetLoad pendingLoad,
            AssetCachePolicy? requestedPolicy)
        {
            if (requestedPolicy == AssetCachePolicy.KeepLoaded)
            {
                pendingLoad.CachePolicy = AssetCachePolicy.KeepLoaded;
            }
        }

        private string[] RetainBundleHierarchy(string bundleName)
        {
            EnsureBundleLoader();
            List<string> retainedBundles = new List<string>();
            AddRetainedBundle(retainedBundles, bundleName);
            string[] dependencies = _bundleLoader.GetAllDependencies(bundleName);
            for (int i = 0; i < dependencies.Length; i++)
            {
                AddRetainedBundle(retainedBundles, dependencies[i]);
            }

            return retainedBundles.ToArray();
        }

        private void AddRetainedBundle(List<string> retainedBundles, string bundleName)
        {
            string normalizedName = NormalizeBundleName(bundleName);
            if (string.IsNullOrEmpty(normalizedName)
                || retainedBundles.Exists(value => string.Equals(
                    value,
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            retainedBundles.Add(normalizedName);
            _bundleReferenceCounts.TryGetValue(normalizedName, out int count);
            _bundleReferenceCounts[normalizedName] = count + 1;
        }

        private void ReleaseBundleHierarchy(string[] retainedBundles)
        {
            if (retainedBundles == null)
            {
                return;
            }

            for (int i = 0; i < retainedBundles.Length; i++)
            {
                string bundleName = retainedBundles[i];
                if (!_bundleReferenceCounts.TryGetValue(bundleName, out int count))
                {
                    continue;
                }

                count--;
                if (count > 0)
                {
                    _bundleReferenceCounts[bundleName] = count;
                    continue;
                }

                _bundleReferenceCounts.Remove(bundleName);
                if (settings.UnloadBundlesWhenUnused && _bundleLoader != null)
                {
                    _bundleLoader.UnloadBundle(bundleName, false);
                }
            }
        }

        private void CancelPendingLoads()
        {
            PendingAssetLoad[] pendingLoads = new PendingAssetLoad[_pendingLoads.Count];
            _pendingLoads.Values.CopyTo(pendingLoads, 0);
            _pendingLoads.Clear();
            _loadQueue.Clear();
            for (int i = 0; i < pendingLoads.Length; i++)
            {
                Action<AssetEntry>[] callbacks = pendingLoads[i].Callbacks.ToArray();
                pendingLoads[i].Callbacks.Clear();
                for (int callbackIndex = 0; callbackIndex < callbacks.Length; callbackIndex++)
                {
                    InvokeSafely(callbacks[callbackIndex], null);
                }
            }
        }

        private IEnumerator UnloadUnusedAssetsCoroutine(Action completed)
        {
            AsyncOperation operation = Resources.UnloadUnusedAssets();
            yield return operation;
            InvokeSafely(completed);
        }

        private void RefreshFrameBudget()
        {
            int frame = Time.frameCount;
            if (!Application.isPlaying || frame != _scheduleFrame)
            {
                _scheduleFrame = frame;
                _loadsStartedThisFrame = 0;
            }
        }

        private static string CreateAssetKey(
            string bundleName,
            string assetName,
            Type assetType)
        {
            return $"{bundleName.ToLowerInvariant()}|{assetName}|{assetType.AssemblyQualifiedName}";
        }

        private static string NormalizeBundleName(string bundleName)
        {
            return AssetBundlePath.NormalizeBundleName(bundleName);
        }

        private Object LoadEditorAsset(string bundleName, string assetName, Type assetType)
        {
#if UNITY_EDITOR
            if (assetName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                Object directAsset = AssetDatabase.LoadAssetAtPath(assetName, assetType);
                if (directAsset == null)
                {
                    Debug.LogError($"[AssetManager] Editor 中找不到资源 {assetName}");
                }

                return directAsset;
            }

            string requestedFileName = Path.GetFileName(assetName);
            string requestedNameWithoutExtension = Path.GetFileNameWithoutExtension(assetName);
            string[] assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
            string matchedPath = null;
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                string fileName = Path.GetFileName(path);
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(path);
                if (!string.Equals(fileName, requestedFileName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        nameWithoutExtension,
                        requestedNameWithoutExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (matchedPath != null)
                {
                    Debug.LogError($"[AssetManager] Bundle {bundleName} 中存在同名资源 {assetName} 请使用 Assets 完整路径");
                    return null;
                }

                matchedPath = path;
            }

            if (matchedPath == null)
            {
                Debug.LogError($"[AssetManager] Editor 中找不到资源 {bundleName}/{assetName}");
                return null;
            }

            return AssetDatabase.LoadAssetAtPath(matchedPath, assetType);
#else
            Debug.LogError("[AssetManager] EditorDatabase 模式只能在 Unity Editor 中使用");
            return null;
#endif
        }

        private void Log(string message)
        {
            if (settings.EnableDebugLogs)
            {
                Debug.Log($"[AssetManager] {message}", this);
            }
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

        private static void InvokeSafely(Action callback)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
