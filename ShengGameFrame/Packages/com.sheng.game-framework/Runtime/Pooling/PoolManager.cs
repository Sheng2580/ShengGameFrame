using System;
using System.Collections;
using System.Collections.Generic;
using Sheng.GameFramework.Assets;
using Sheng.GameFramework.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 管理 GameObject 池的初始化 租用 归还和删除
    /// </summary>
    public sealed class PoolManager : PersistentMonoSingleton<PoolManager>
    {
        private sealed class PendingInitialization
        {
            public PoolKey Key;
            public string BundleName;
            public string AssetName;
            public GameObjectPoolOptions Options;
            public int OwnerSceneHandle;
            public string OwnerSceneName;
            public GameObjectPool Pool;
            public bool Cancelled;
            public readonly List<Action<bool>> Callbacks = new List<Action<bool>>();

            public bool Matches(
                string bundleName,
                string assetName,
                GameObjectPoolOptions options)
            {
                return string.Equals(
                           BundleName,
                           bundleName,
                           StringComparison.OrdinalIgnoreCase)
                       && string.Equals(
                           AssetName,
                           assetName,
                           StringComparison.Ordinal)
                       && Options.IsEquivalentTo(options);
            }
        }

        private readonly Dictionary<PoolKey, GameObjectPool> _pools =
            new Dictionary<PoolKey, GameObjectPool>();
        private readonly Dictionary<PoolKey, PendingInitialization> _pendingInitializations =
            new Dictionary<PoolKey, PendingInitialization>();
        private readonly Dictionary<PoolKey, List<Action<bool>>> _deleteCallbacks =
            new Dictionary<PoolKey, List<Action<bool>>>();

        private Transform _poolsRoot;
        private bool _isShuttingDown;

        public int PoolCount => _pools.Count;
        public int InitializingCount => _pendingInitializations.Count;

        protected override void OnSingletonAwake()
        {
            EnsurePoolsRoot();
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        protected override void OnSingletonDestroyed()
        {
            _isShuttingDown = true;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            CancelAllPendingInitializations();

            GameObjectPool[] pools = new GameObjectPool[_pools.Count];
            _pools.Values.CopyTo(pools, 0);
            _pools.Clear();
            _deleteCallbacks.Clear();
            for (int i = 0; i < pools.Length; i++)
            {
                pools[i].BeginDelete(true);
                pools[i].FinalizeDelete();
            }
        }

        private void Update()
        {
            FinalizeCompletedDeletions();
        }

        public bool InitializePool(
            PoolKey key,
            GameObject prefab,
            int initialCapacity = 0,
            int maxCapacity = -1,
            PoolLifetime lifetime = PoolLifetime.Scene)
        {
            return InitializePool(
                key,
                prefab,
                CreateOptions(initialCapacity, maxCapacity, lifetime));
        }

        public bool InitializePool(
            PoolKey key,
            GameObject prefab,
            GameObjectPoolOptions options)
        {
            if (!ValidateInitialization(key, prefab, options))
            {
                return false;
            }

            if (_pools.TryGetValue(key, out GameObjectPool existingPool))
            {
                if (existingPool.State == PoolState.Ready
                    && existingPool.MatchesPrefab(prefab, options))
                {
                    return true;
                }

                Debug.LogError($"[PoolManager] 对象池已经使用其他配置初始化 {key}");
                return false;
            }

            if (_pendingInitializations.ContainsKey(key))
            {
                Debug.LogError($"[PoolManager] 对象池正在异步初始化 {key}");
                return false;
            }

            Scene ownerScene = SceneManager.GetActiveScene();
            GameObjectPool pool = null;
            try
            {
                pool = CreatePool(
                    key,
                    prefab,
                    null,
                    options,
                    ownerScene.handle,
                    ownerScene.name,
                    prefab.name);
                pool.Prewarm(options.InitialCapacity);
                pool.MarkReady();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (pool != null)
                {
                    _pools.Remove(key);
                    pool.BeginDelete(true);
                    pool.FinalizeDelete();
                }

                return false;
            }
        }

        public PoolKey InitializePoolAsync(
            string bundleName,
            string assetName,
            int initialCapacity = 0,
            int maxCapacity = -1,
            PoolLifetime lifetime = PoolLifetime.Scene,
            Action<bool> completed = null)
        {
            PoolKey key = PoolKey.FromAsset(bundleName, assetName);
            InitializePoolAsync(
                key,
                bundleName,
                assetName,
                CreateOptions(initialCapacity, maxCapacity, lifetime),
                completed);
            return key;
        }

        public void InitializePoolAsync(
            PoolKey key,
            string bundleName,
            string assetName,
            GameObjectPoolOptions options,
            Action<bool> completed = null)
        {
            string normalizedBundleName = AssetBundlePath.NormalizeBundleName(bundleName);
            string normalizedAssetName = assetName?.Trim();
            if (!key.IsValid
                || string.IsNullOrEmpty(normalizedBundleName)
                || string.IsNullOrEmpty(normalizedAssetName)
                || options == null)
            {
                Debug.LogError("[PoolManager] 异步初始化参数不完整");
                InvokeSafely(completed, false);
                return;
            }

            try
            {
                options.Validate();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                InvokeSafely(completed, false);
                return;
            }

            if (_pools.TryGetValue(key, out GameObjectPool existingPool))
            {
                bool matches = existingPool.State == PoolState.Ready
                               && existingPool.MatchesAsset(
                                   normalizedBundleName,
                                   normalizedAssetName,
                                   options);
                if (!matches)
                {
                    Debug.LogError($"[PoolManager] 对象池已经使用其他配置初始化 {key}");
                }

                InvokeSafely(completed, matches);
                return;
            }

            if (_pendingInitializations.TryGetValue(key, out PendingInitialization pending))
            {
                if (!pending.Matches(
                        normalizedBundleName,
                        normalizedAssetName,
                        options))
                {
                    Debug.LogError($"[PoolManager] 对象池正在使用其他配置初始化 {key}");
                    InvokeSafely(completed, false);
                    return;
                }

                if (completed != null)
                {
                    pending.Callbacks.Add(completed);
                }

                return;
            }

            Scene ownerScene = SceneManager.GetActiveScene();
            PendingInitialization newPending = new PendingInitialization
            {
                Key = key,
                BundleName = normalizedBundleName,
                AssetName = normalizedAssetName,
                Options = options.Clone(),
                OwnerSceneHandle = ownerScene.handle,
                OwnerSceneName = ownerScene.name
            };
            if (completed != null)
            {
                newPending.Callbacks.Add(completed);
            }

            _pendingInitializations.Add(key, newPending);
            AssetManager.Instance.LoadAssetAsync<GameObject>(
                normalizedBundleName,
                normalizedAssetName,
                handle => OnPrefabLoaded(newPending, handle));
        }

        public void InitializePoolsAsync(
            IEnumerable<PoolDefinition> definitions,
            Action<bool> completed = null)
        {
            if (definitions == null)
            {
                InvokeSafely(completed, false);
                return;
            }

            List<PoolDefinition> definitionList = new List<PoolDefinition>(definitions);
            if (definitionList.Count == 0)
            {
                InvokeSafely(completed, true);
                return;
            }

            int remaining = definitionList.Count;
            bool allSucceeded = true;
            Action<bool> onOneCompleted = success =>
            {
                allSucceeded &= success;
                remaining--;
                if (remaining == 0)
                {
                    InvokeSafely(completed, allSucceeded);
                }
            };

            for (int i = 0; i < definitionList.Count; i++)
            {
                PoolDefinition definition = definitionList[i];
                if (definition == null)
                {
                    onOneCompleted(false);
                    continue;
                }

                if (definition.Source == PoolSource.Prefab)
                {
                    onOneCompleted(InitializePool(
                        definition.Key,
                        definition.Prefab,
                        definition.Options));
                    continue;
                }

                InitializePoolAsync(
                    definition.Key,
                    definition.BundleName,
                    definition.AssetName,
                    definition.Options,
                    onOneCompleted);
            }
        }

        public PooledHandle Rent(PoolKey key)
        {
            return Rent(key, Vector3.zero, Quaternion.identity, null);
        }

        public PooledHandle Rent(
            PoolKey key,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            if (!_pools.TryGetValue(key, out GameObjectPool pool)
                || pool.State != PoolState.Ready)
            {
                Debug.LogError($"[PoolManager] 对象池尚未初始化完成 {key}");
                return null;
            }

            PooledHandle handle = pool.Rent(position, rotation, parent);
            if (handle == null)
            {
                Debug.LogWarning($"[PoolManager] 对象池达到最大容量 {key}");
            }

            return handle;
        }

        public T Rent<T>(
            PoolKey key)
            where T : Component
        {
            return Rent<T>(key, Vector3.zero, Quaternion.identity, null);
        }

        public T Rent<T>(
            PoolKey key,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
            where T : Component
        {
            PooledHandle handle = Rent(key, position, rotation, parent);
            if (handle == null)
            {
                return null;
            }

            T component = handle.Get<T>();
            if (component == null)
            {
                Debug.LogError($"[PoolManager] 对象缺少组件 {typeof(T).Name} {key}");
                handle.Dispose();
                return null;
            }

            handle.ReleaseOwnership();
            return component;
        }

        public bool TryRent(
            PoolKey key,
            out PooledHandle handle)
        {
            return TryRent(
                key,
                out handle,
                Vector3.zero,
                Quaternion.identity,
                null);
        }

        public bool TryRent(
            PoolKey key,
            out PooledHandle handle,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
        {
            handle = Rent(key, position, rotation, parent);
            return handle != null;
        }

        public bool TryRent<T>(
            PoolKey key,
            out T component)
            where T : Component
        {
            return TryRent(
                key,
                out component,
                Vector3.zero,
                Quaternion.identity,
                null);
        }

        public bool TryRent<T>(
            PoolKey key,
            out T component,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null)
            where T : Component
        {
            component = Rent<T>(key, position, rotation, parent);
            return component != null;
        }

        public bool Return(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            return ReturnInternal(instance.GetComponent<PooledObject>(), -1, true);
        }

        public bool Return(Component component)
        {
            return component != null && Return(component.gameObject);
        }

        public void ReturnAfter(GameObject instance, float delaySeconds)
        {
            if (instance == null)
            {
                return;
            }

            PooledObject marker = instance.GetComponent<PooledObject>();
            if (marker == null || !marker.IsRented)
            {
                Debug.LogWarning("[PoolManager] 延迟归还对象不属于活动对象池", instance);
                return;
            }

            int rentVersion = marker.RentVersion;
            if (delaySeconds <= 0f)
            {
                ReturnInternal(marker, rentVersion, false);
                return;
            }

            StartCoroutine(ReturnAfterCoroutine(marker, rentVersion, delaySeconds));
        }

        public void ReturnAfter(Component component, float delaySeconds)
        {
            if (component != null)
            {
                ReturnAfter(component.gameObject, delaySeconds);
            }
        }

        public int PrewarmPool(PoolKey key, int count)
        {
            if (!_pools.TryGetValue(key, out GameObjectPool pool)
                || pool.State != PoolState.Ready)
            {
                return 0;
            }

            try
            {
                return pool.Prewarm(count);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return 0;
            }
        }

        public void PrewarmPoolAsync(
            PoolKey key,
            int count,
            Action<int> completed = null)
        {
            if (!_pools.TryGetValue(key, out GameObjectPool pool)
                || pool.State != PoolState.Ready
                || count < 0)
            {
                InvokeSafely(completed, 0);
                return;
            }

            if (!Application.isPlaying)
            {
                InvokeSafely(completed, PrewarmPool(key, count));
                return;
            }

            StartCoroutine(PrewarmReadyPoolCoroutine(pool, count, completed));
        }

        public bool ClearPool(PoolKey key)
        {
            if (!_pools.TryGetValue(key, out GameObjectPool pool)
                || pool.State != PoolState.Ready)
            {
                return false;
            }

            pool.Clear();
            return true;
        }

        public void ClearAllPools()
        {
            foreach (GameObjectPool pool in _pools.Values)
            {
                pool.Clear();
            }
        }

        public bool SetMaxCapacity(PoolKey key, int maxCapacity)
        {
            if (!_pools.TryGetValue(key, out GameObjectPool pool)
                || pool.State != PoolState.Ready)
            {
                return false;
            }

            try
            {
                pool.SetMaxCapacity(maxCapacity);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        public bool DeletePool(
            PoolKey key,
            bool force = false,
            Action<bool> completed = null)
        {
            if (_pendingInitializations.TryGetValue(key, out PendingInitialization pending))
            {
                CancelPendingInitialization(pending);
                if (pending.Pool != null)
                {
                    pending.Pool.BeginDelete(force);
                    if (pending.Pool.CanFinalizeDelete())
                    {
                        FinalizePoolDelete(pending.Pool);
                    }
                }

                InvokeSafely(completed, true);
                return true;
            }

            if (!_pools.TryGetValue(key, out GameObjectPool pool))
            {
                InvokeSafely(completed, false);
                return false;
            }

            AddDeleteCallback(key, completed);
            pool.BeginDelete(force);
            if (pool.CanFinalizeDelete())
            {
                FinalizePoolDelete(pool);
            }

            return true;
        }

        public void DeleteAllPools(
            bool force = false,
            Action<bool> completed = null)
        {
            HashSet<PoolKey> keys = new HashSet<PoolKey>(_pools.Keys);
            keys.UnionWith(_pendingInitializations.Keys);
            if (keys.Count == 0)
            {
                InvokeSafely(completed, true);
                return;
            }

            int remaining = keys.Count;
            bool allSucceeded = true;
            foreach (PoolKey key in keys)
            {
                DeletePool(key, force, success =>
                {
                    allSucceeded &= success;
                    remaining--;
                    if (remaining == 0)
                    {
                        InvokeSafely(completed, allSucceeded);
                    }
                });
            }
        }

        public int DeleteScenePools(Scene scene, bool force = true)
        {
            List<PoolKey> keys = new List<PoolKey>();
            foreach (KeyValuePair<PoolKey, GameObjectPool> pair in _pools)
            {
                if (pair.Value.Options.Lifetime == PoolLifetime.Scene
                    && pair.Value.OwnerSceneHandle == scene.handle)
                {
                    keys.Add(pair.Key);
                }
            }

            foreach (KeyValuePair<PoolKey, PendingInitialization> pair
                     in _pendingInitializations)
            {
                if (pair.Value.Options.Lifetime == PoolLifetime.Scene
                    && pair.Value.OwnerSceneHandle == scene.handle
                    && !keys.Contains(pair.Key))
                {
                    keys.Add(pair.Key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                DeletePool(keys[i], force);
            }

            return keys.Count;
        }

        public int DeletePersistentPools(bool force = false)
        {
            List<PoolKey> keys = new List<PoolKey>();
            foreach (KeyValuePair<PoolKey, GameObjectPool> pair in _pools)
            {
                if (pair.Value.Options.Lifetime == PoolLifetime.Persistent)
                {
                    keys.Add(pair.Key);
                }
            }

            foreach (KeyValuePair<PoolKey, PendingInitialization> pair
                     in _pendingInitializations)
            {
                if (pair.Value.Options.Lifetime == PoolLifetime.Persistent
                    && !keys.Contains(pair.Key))
                {
                    keys.Add(pair.Key);
                }
            }

            for (int i = 0; i < keys.Count; i++)
            {
                DeletePool(keys[i], force);
            }

            return keys.Count;
        }

        public bool IsPoolReady(PoolKey key)
        {
            return _pools.TryGetValue(key, out GameObjectPool pool)
                   && pool.State == PoolState.Ready;
        }

        public PoolState? GetPoolState(PoolKey key)
        {
            if (_pools.TryGetValue(key, out GameObjectPool pool))
            {
                return pool.State;
            }

            return _pendingInitializations.ContainsKey(key)
                ? PoolState.Initializing
                : (PoolState?)null;
        }

        public PoolManagerDebugSnapshot GetDebugSnapshot()
        {
            FinalizeCompletedDeletions();
            PoolManagerDebugSnapshot snapshot = new PoolManagerDebugSnapshot
            {
                InitializingCount = _pendingInitializations.Count
            };
            foreach (GameObjectPool pool in _pools.Values)
            {
                snapshot.Pools.Add(pool.CreateDebugInfo());
            }

            snapshot.Pools.Sort((left, right) => string.Compare(
                left.Key,
                right.Key,
                StringComparison.Ordinal));
            return snapshot;
        }

        internal bool ReturnInternal(
            PooledObject marker,
            int expectedRentVersion,
            bool logInvalid)
        {
            if (marker == null
                || !marker.IsRented
                || (expectedRentVersion >= 0
                    && marker.RentVersion != expectedRentVersion))
            {
                if (logInvalid)
                {
                    Debug.LogWarning("[PoolManager] 对象已经归还或不属于活动对象池");
                }

                return false;
            }

            if (!_pools.TryGetValue(marker.PoolKey, out GameObjectPool pool))
            {
                if (logInvalid)
                {
                    Debug.LogWarning($"[PoolManager] 找不到对象所属池 {marker.PoolKey}");
                }

                return false;
            }

            try
            {
                bool returned = pool.Return(marker);
                if (pool.CanFinalizeDelete())
                {
                    FinalizePoolDelete(pool);
                }

                return returned;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, marker);
                return false;
            }
        }

        internal void NotifyInstanceDestroyed(PooledObject marker)
        {
            if (_isShuttingDown || ReferenceEquals(marker, null))
            {
                return;
            }

            if (_pools.TryGetValue(marker.PoolKey, out GameObjectPool pool))
            {
                pool.NotifyExternalDestroyed(marker);
                if (pool.CanFinalizeDelete())
                {
                    FinalizePoolDelete(pool);
                }
            }
        }

        private void OnPrefabLoaded(
            PendingInitialization pending,
            AssetHandle<GameObject> prefabHandle)
        {
            if (this == null
                || pending.Cancelled
                || !_pendingInitializations.TryGetValue(
                    pending.Key,
                    out PendingInitialization current)
                || current != pending)
            {
                prefabHandle?.Dispose();
                return;
            }

            if (prefabHandle == null || prefabHandle.Asset == null)
            {
                prefabHandle?.Dispose();
                CompleteInitialization(pending, false);
                return;
            }

            try
            {
                pending.Pool = CreatePool(
                    pending.Key,
                    prefabHandle.Asset,
                    prefabHandle,
                    pending.Options,
                    pending.OwnerSceneHandle,
                    pending.OwnerSceneName,
                    $"{pending.BundleName}/{pending.AssetName}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                prefabHandle.Dispose();
                CompleteInitialization(pending, false);
                return;
            }

            if (pending.Options.InitialCapacity == 0)
            {
                CompleteInitialization(pending, true);
                return;
            }

            if (!Application.isPlaying)
            {
                pending.Pool.Prewarm(pending.Options.InitialCapacity);
                CompleteInitialization(pending, true);
                return;
            }

            StartCoroutine(PrewarmCoroutine(pending));
        }

        private IEnumerator PrewarmCoroutine(PendingInitialization pending)
        {
            int remaining = pending.Options.InitialCapacity;
            while (remaining > 0)
            {
                if (pending.Cancelled
                    || !_pendingInitializations.TryGetValue(
                        pending.Key,
                        out PendingInitialization current)
                    || current != pending)
                {
                    yield break;
                }

                int batchSize = Mathf.Min(
                    remaining,
                    pending.Options.PrewarmPerFrame);
                int created = pending.Pool.Prewarm(batchSize);
                if (created == 0)
                {
                    CompleteInitialization(pending, false);
                    yield break;
                }

                remaining -= created;
                if (remaining > 0)
                {
                    yield return null;
                }
            }

            CompleteInitialization(pending, true);
        }

        private IEnumerator ReturnAfterCoroutine(
            PooledObject marker,
            int rentVersion,
            float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            if (marker != null
                && marker.IsRented
                && marker.RentVersion == rentVersion)
            {
                ReturnInternal(marker, rentVersion, false);
            }
        }

        private IEnumerator PrewarmReadyPoolCoroutine(
            GameObjectPool pool,
            int count,
            Action<int> completed)
        {
            int remaining = count;
            int totalCreated = 0;
            while (remaining > 0 && pool.State == PoolState.Ready)
            {
                int batchSize = Mathf.Min(remaining, pool.Options.PrewarmPerFrame);
                int created = pool.Prewarm(batchSize);
                totalCreated += created;
                remaining -= created;
                if (created < batchSize)
                {
                    break;
                }

                if (remaining > 0)
                {
                    yield return null;
                }
            }

            InvokeSafely(completed, totalCreated);
        }

        private GameObjectPool CreatePool(
            PoolKey key,
            GameObject prefab,
            AssetHandle<GameObject> prefabHandle,
            GameObjectPoolOptions options,
            int ownerSceneHandle,
            string ownerSceneName,
            string source)
        {
            EnsurePoolsRoot();
            GameObject rootObject = new GameObject(CreatePoolRootName(key));
            rootObject.transform.SetParent(_poolsRoot, false);
            GameObjectPool pool = new GameObjectPool(
                this,
                key,
                prefab,
                prefabHandle,
                options,
                rootObject.transform,
                ownerSceneHandle,
                ownerSceneName,
                source);
            _pools.Add(key, pool);
            return pool;
        }

        private void CompleteInitialization(
            PendingInitialization pending,
            bool success)
        {
            if (!_pendingInitializations.TryGetValue(
                    pending.Key,
                    out PendingInitialization current)
                || current != pending)
            {
                return;
            }

            _pendingInitializations.Remove(pending.Key);
            if (success && pending.Pool != null)
            {
                pending.Pool.MarkReady();
            }
            else if (pending.Pool != null)
            {
                _pools.Remove(pending.Key);
                pending.Pool.BeginDelete(true);
                pending.Pool.FinalizeDelete();
            }

            Action<bool>[] callbacks = pending.Callbacks.ToArray();
            pending.Callbacks.Clear();
            for (int i = 0; i < callbacks.Length; i++)
            {
                InvokeSafely(callbacks[i], success);
            }
        }

        private void CancelPendingInitialization(PendingInitialization pending)
        {
            pending.Cancelled = true;
            _pendingInitializations.Remove(pending.Key);
            Action<bool>[] callbacks = pending.Callbacks.ToArray();
            pending.Callbacks.Clear();
            for (int i = 0; i < callbacks.Length; i++)
            {
                InvokeSafely(callbacks[i], false);
            }
        }

        private void CancelAllPendingInitializations()
        {
            PendingInitialization[] pending =
                new PendingInitialization[_pendingInitializations.Count];
            _pendingInitializations.Values.CopyTo(pending, 0);
            _pendingInitializations.Clear();
            for (int i = 0; i < pending.Length; i++)
            {
                pending[i].Cancelled = true;
                Action<bool>[] callbacks = pending[i].Callbacks.ToArray();
                pending[i].Callbacks.Clear();
                for (int callbackIndex = 0; callbackIndex < callbacks.Length; callbackIndex++)
                {
                    InvokeSafely(callbacks[callbackIndex], false);
                }
            }
        }

        private void AddDeleteCallback(PoolKey key, Action<bool> completed)
        {
            if (completed == null)
            {
                return;
            }

            if (!_deleteCallbacks.TryGetValue(key, out List<Action<bool>> callbacks))
            {
                callbacks = new List<Action<bool>>();
                _deleteCallbacks.Add(key, callbacks);
            }

            callbacks.Add(completed);
        }

        private void FinalizePoolDelete(GameObjectPool pool)
        {
            PoolKey key = pool.Key;
            _pools.Remove(key);
            pool.FinalizeDelete();
            if (!_deleteCallbacks.TryGetValue(key, out List<Action<bool>> callbacks))
            {
                return;
            }

            _deleteCallbacks.Remove(key);
            Action<bool>[] callbackArray = callbacks.ToArray();
            for (int i = 0; i < callbackArray.Length; i++)
            {
                InvokeSafely(callbackArray[i], true);
            }
        }

        private void FinalizeCompletedDeletions()
        {
            if (_pools.Count == 0)
            {
                return;
            }

            List<GameObjectPool> completedPools = null;
            foreach (GameObjectPool pool in _pools.Values)
            {
                if (!pool.CanFinalizeDelete())
                {
                    continue;
                }

                if (completedPools == null)
                {
                    completedPools = new List<GameObjectPool>();
                }

                completedPools.Add(pool);
            }

            if (completedPools == null)
            {
                return;
            }

            for (int i = 0; i < completedPools.Count; i++)
            {
                GameObjectPool pool = completedPools[i];
                if (_pools.TryGetValue(pool.Key, out GameObjectPool current)
                    && ReferenceEquals(current, pool))
                {
                    FinalizePoolDelete(pool);
                }
            }
        }

        private bool ValidateInitialization(
            PoolKey key,
            GameObject prefab,
            GameObjectPoolOptions options)
        {
            if (!key.IsValid || prefab == null || options == null)
            {
                Debug.LogError("[PoolManager] 初始化参数不完整");
                return false;
            }

            try
            {
                options.Validate();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private void EnsurePoolsRoot()
        {
            if (_poolsRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("Pools");
            rootObject.transform.SetParent(transform, false);
            _poolsRoot = rootObject.transform;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            DeleteScenePools(scene, true);
        }

        private static GameObjectPoolOptions CreateOptions(
            int initialCapacity,
            int maxCapacity,
            PoolLifetime lifetime)
        {
            return new GameObjectPoolOptions
            {
                InitialCapacity = initialCapacity,
                MaxCapacity = maxCapacity,
                Lifetime = lifetime
            };
        }

        private static string CreatePoolRootName(PoolKey key)
        {
            string name = key.ToString()
                .Replace("asset://", string.Empty)
                .Replace("custom://", string.Empty)
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_');
            return $"Pool_{name}";
        }

        private static void InvokeSafely(Action<bool> callback, bool value)
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

        private static void InvokeSafely(Action<int> callback, int value)
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
