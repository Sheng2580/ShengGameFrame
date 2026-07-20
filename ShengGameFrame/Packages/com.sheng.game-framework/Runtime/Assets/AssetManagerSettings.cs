using System;
using UnityEngine;

namespace Sheng.GameFramework.Assets
{
    /// <summary>
    /// 资源加载模式
    /// </summary>
    public enum AssetLoadMode
    {
        Auto,
        AssetBundle,
        EditorDatabase
    }

    /// <summary>
    /// 资源缓存策略
    /// </summary>
    public enum AssetCachePolicy
    {
        ReferenceCounted,
        KeepLoaded
    }

    /// <summary>
    /// AssetManager 运行参数
    /// </summary>
    [Serializable]
    public sealed class AssetManagerSettings
    {
        [SerializeField] private AssetLoadMode loadMode = AssetLoadMode.Auto;
        [SerializeField] private AssetCachePolicy defaultCachePolicy =
            AssetCachePolicy.ReferenceCounted;
        [SerializeField, Min(1)] private int maxConcurrentLoads = 4;
        [SerializeField, Min(1)] private int maxLoadsPerFrame = 2;
        [SerializeField] private bool unloadBundlesWhenUnused = true;
        [SerializeField] private bool enableDebugLogs;

        public AssetLoadMode LoadMode
        {
            get => loadMode;
            set => loadMode = value;
        }

        public AssetCachePolicy DefaultCachePolicy
        {
            get => defaultCachePolicy;
            set => defaultCachePolicy = value;
        }

        public int MaxConcurrentLoads
        {
            get => maxConcurrentLoads;
            set => maxConcurrentLoads = Mathf.Max(1, value);
        }

        public int MaxLoadsPerFrame
        {
            get => maxLoadsPerFrame;
            set => maxLoadsPerFrame = Mathf.Max(1, value);
        }

        public bool UnloadBundlesWhenUnused
        {
            get => unloadBundlesWhenUnused;
            set => unloadBundlesWhenUnused = value;
        }

        public bool EnableDebugLogs
        {
            get => enableDebugLogs;
            set => enableDebugLogs = value;
        }

        public AssetManagerSettings Clone()
        {
            AssetManagerSettings clone = new AssetManagerSettings();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(AssetManagerSettings source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            loadMode = source.loadMode;
            defaultCachePolicy = source.defaultCachePolicy;
            maxConcurrentLoads = Mathf.Max(1, source.maxConcurrentLoads);
            maxLoadsPerFrame = Mathf.Max(1, source.maxLoadsPerFrame);
            unloadBundlesWhenUnused = source.unloadBundlesWhenUnused;
            enableDebugLogs = source.enableDebugLogs;
        }

        internal void Sanitize()
        {
            maxConcurrentLoads = Mathf.Max(1, maxConcurrentLoads);
            maxLoadsPerFrame = Mathf.Max(1, maxLoadsPerFrame);
        }
    }
}
