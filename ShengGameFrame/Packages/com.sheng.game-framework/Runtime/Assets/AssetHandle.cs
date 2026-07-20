using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Assets
{
    /// <summary>
    /// 可释放的资源句柄
    /// </summary>
    public interface IAssetHandle : IDisposable
    {
        Object AssetObject { get; }
        string BundleName { get; }
        string AssetName { get; }
        bool IsReleased { get; }
    }

    /// <summary>
    /// 持有一次资源引用
    /// </summary>
    public sealed class AssetHandle<T> : IAssetHandle where T : Object
    {
        private AssetManager _owner;
        private string _entryKey;

        internal AssetHandle(
            AssetManager owner,
            string entryKey,
            string bundleName,
            string assetName,
            T asset)
        {
            _owner = owner;
            _entryKey = entryKey;
            BundleName = bundleName;
            AssetName = assetName;
            Asset = asset;
        }

        public T Asset { get; private set; }
        public Object AssetObject => Asset;
        public string BundleName { get; }
        public string AssetName { get; }
        public bool IsReleased { get; private set; }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            AssetManager owner = _owner;
            string entryKey = _entryKey;
            _owner = null;
            _entryKey = null;
            Asset = null;
            owner?.ReleaseEntry(entryKey);
        }
    }

    /// <summary>
    /// 同时管理实例和预制体资源引用
    /// </summary>
    public sealed class AssetInstanceHandle : IDisposable
    {
        private AssetHandle<GameObject> _prefabHandle;

        internal AssetInstanceHandle(
            GameObject instance,
            AssetHandle<GameObject> prefabHandle)
        {
            Instance = instance;
            _prefabHandle = prefabHandle;
        }

        public GameObject Instance { get; private set; }
        public bool IsReleased { get; private set; }

        public void Dispose()
        {
            Release(true);
        }

        public void Release(bool destroyInstance)
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            if (destroyInstance && Instance != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(Instance);
                }
                else
                {
                    Object.DestroyImmediate(Instance);
                }
            }

            Instance = null;
            _prefabHandle?.Dispose();
            _prefabHandle = null;
        }
    }
}
