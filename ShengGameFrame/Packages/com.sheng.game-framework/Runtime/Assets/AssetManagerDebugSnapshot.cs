using System;
using System.Collections.Generic;

namespace Sheng.GameFramework.Assets
{
    /// <summary>
    /// 已加载资源调试信息
    /// </summary>
    [Serializable]
    public sealed class AssetDebugInfo
    {
        public string BundleName;
        public string AssetName;
        public string AssetType;
        public int ReferenceCount;
        public AssetCachePolicy CachePolicy;
    }

    /// <summary>
    /// 已加载 Bundle 调试信息
    /// </summary>
    [Serializable]
    public sealed class BundleDebugInfo
    {
        public string BundleName;
        public int ReferenceCount;
    }

    /// <summary>
    /// AssetManager 当前状态快照
    /// </summary>
    [Serializable]
    public sealed class AssetManagerDebugSnapshot
    {
        public AssetLoadMode EffectiveLoadMode;
        public int ActiveLoadCount;
        public int QueuedLoadCount;
        public readonly List<AssetDebugInfo> Assets = new List<AssetDebugInfo>();
        public readonly List<BundleDebugInfo> Bundles = new List<BundleDebugInfo>();
    }
}
