using System;
using Sheng.GameFramework.Assets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// ObjectPool 在 Unity GameObject 上的内部适配层
    /// </summary>
    internal sealed class GameObjectPool
    {
        private readonly PoolManager _owner;
        private readonly GameObject _prefab;
        private readonly AssetHandle<GameObject> _prefabHandle;
        private readonly Transform _root;
        private readonly ObjectPool<PooledObject> _pool;

        internal GameObjectPool(
            PoolManager owner,
            PoolKey key,
            GameObject prefab,
            AssetHandle<GameObject> prefabHandle,
            GameObjectPoolOptions options,
            Transform root,
            int ownerSceneHandle,
            string ownerSceneName,
            string source)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Key = key;
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _prefabHandle = prefabHandle;
            Options = options?.Clone() ?? new GameObjectPoolOptions();
            Options.Validate();
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            OwnerSceneHandle = ownerSceneHandle;
            OwnerSceneName = ownerSceneName;
            Source = source;
            State = PoolState.Initializing;
            _pool = new ObjectPool<PooledObject>(
                CreateInstance,
                0,
                Options.MaxCapacity,
                Options.CollectionChecks,
                null,
                marker => marker.Store(_root, Options),
                marker => marker.DestroyManaged());
        }

        internal PoolKey Key { get; }
        internal GameObjectPoolOptions Options { get; }
        internal PoolState State { get; private set; }
        internal int OwnerSceneHandle { get; }
        internal string OwnerSceneName { get; }
        internal string Source { get; }
        internal int CountAll
        {
            get
            {
                RemoveDestroyedInstances();
                return _pool.CountAll;
            }
        }

        internal int CountActive
        {
            get
            {
                RemoveDestroyedInstances();
                return _pool.CountActive;
            }
        }

        internal int CountInactive
        {
            get
            {
                RemoveDestroyedInstances();
                return _pool.CountInactive;
            }
        }

        internal int Prewarm(int count)
        {
            RemoveDestroyedInstances();
            return State == PoolState.Disposing || State == PoolState.Disposed
                ? 0
                : _pool.Prewarm(count);
        }

        internal void MarkReady()
        {
            if (State == PoolState.Initializing)
            {
                State = PoolState.Ready;
            }
        }

        internal PooledHandle Rent(
            Vector3 position,
            Quaternion rotation,
            Transform parent)
        {
            if (State != PoolState.Ready)
            {
                return null;
            }

            RemoveDestroyedInstances();
            PooledObject marker = _pool.Rent();
            if (marker == null)
            {
                return null;
            }

            marker.Activate(parent, position, rotation, Options);
            return new PooledHandle(_owner, marker);
        }

        internal bool Return(PooledObject marker)
        {
            if (marker == null || State == PoolState.Disposed)
            {
                return false;
            }

            bool returned = _pool.Return(marker);
            if (!returned)
            {
                return false;
            }

            if (State == PoolState.Disposing)
            {
                _pool.Remove(marker, true);
            }

            return true;
        }

        internal void NotifyExternalDestroyed(PooledObject marker)
        {
            _pool.Remove(marker, false);
        }

        internal void Clear()
        {
            RemoveDestroyedInstances();
            if (State == PoolState.Ready)
            {
                _pool.Clear(false);
            }
        }

        internal void SetMaxCapacity(int maxCapacity)
        {
            RemoveDestroyedInstances();
            _pool.SetMaxCapacity(maxCapacity);
            Options.MaxCapacity = maxCapacity;
        }

        internal void BeginDelete(bool force)
        {
            if (State == PoolState.Disposed)
            {
                return;
            }

            RemoveDestroyedInstances();
            State = PoolState.Disposing;
            _pool.Clear(force);
        }

        internal bool CanFinalizeDelete()
        {
            return State == PoolState.Disposing && CountAll == 0;
        }

        internal void FinalizeDelete()
        {
            if (State == PoolState.Disposed)
            {
                return;
            }

            _pool.Dispose();
            _prefabHandle?.Dispose();
            State = PoolState.Disposed;
            if (_root != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(_root.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(_root.gameObject);
                }
            }
        }

        internal bool MatchesPrefab(
            GameObject prefab,
            GameObjectPoolOptions options)
        {
            return _prefab == prefab && Options.IsEquivalentTo(options);
        }

        internal bool MatchesAsset(
            string bundleName,
            string assetName,
            GameObjectPoolOptions options)
        {
            return string.Equals(
                       Source,
                       $"{bundleName}/{assetName}",
                       StringComparison.OrdinalIgnoreCase)
                   && Options.IsEquivalentTo(options);
        }

        internal PoolDebugInfo CreateDebugInfo()
        {
            return new PoolDebugInfo
            {
                PoolKey = Key,
                Key = Key.ToString(),
                State = State,
                Lifetime = Options.Lifetime,
                Source = Source,
                OwnerScene = OwnerSceneName,
                CountAll = CountAll,
                CountActive = CountActive,
                CountInactive = CountInactive,
                MaxCapacity = Options.MaxCapacity
            };
        }

        private PooledObject CreateInstance()
        {
            GameObject instance = Object.Instantiate(_prefab, _root, false);
            instance.name = _prefab.name;
            instance.SetActive(false);
            PooledObject marker = instance.GetComponent<PooledObject>();
            if (marker == null)
            {
                marker = instance.AddComponent<PooledObject>();
            }

            marker.Initialize(_owner, Key);
            return marker;
        }

        private void RemoveDestroyedInstances()
        {
            if (!_pool.IsDisposed)
            {
                _pool.RemoveWhere(marker => marker == null);
            }
        }
    }
}
