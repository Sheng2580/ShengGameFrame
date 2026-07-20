using System;
using System.Collections.Generic;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 单个对象池调试信息
    /// </summary>
    [Serializable]
    public sealed class PoolDebugInfo
    {
        public PoolKey PoolKey;
        public string Key;
        public PoolState State;
        public PoolLifetime Lifetime;
        public string Source;
        public string OwnerScene;
        public int CountAll;
        public int CountActive;
        public int CountInactive;
        public int MaxCapacity;
    }

    /// <summary>
    /// PoolManager 当前状态快照
    /// </summary>
    [Serializable]
    public sealed class PoolManagerDebugSnapshot
    {
        public int InitializingCount;
        public readonly List<PoolDebugInfo> Pools = new List<PoolDebugInfo>();
    }
}
