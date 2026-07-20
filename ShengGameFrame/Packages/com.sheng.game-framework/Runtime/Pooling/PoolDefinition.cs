using System;
using UnityEngine;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 对象池资源来源
    /// </summary>
    public enum PoolSource
    {
        Prefab,
        Asset
    }

    /// <summary>
    /// 批量初始化使用的对象池定义
    /// </summary>
    public sealed class PoolDefinition
    {
        private PoolDefinition()
        {
        }

        public PoolKey Key { get; private set; }
        public PoolSource Source { get; private set; }
        public GameObject Prefab { get; private set; }
        public string BundleName { get; private set; }
        public string AssetName { get; private set; }
        public GameObjectPoolOptions Options { get; private set; }

        public static PoolDefinition FromPrefab(
            string poolName,
            GameObject prefab,
            int initialCapacity = 0,
            int maxCapacity = -1,
            PoolLifetime lifetime = PoolLifetime.Scene)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            return new PoolDefinition
            {
                Key = PoolKey.FromName(poolName),
                Source = PoolSource.Prefab,
                Prefab = prefab,
                Options = CreateOptions(initialCapacity, maxCapacity, lifetime)
            };
        }

        public static PoolDefinition FromAsset(
            string bundleName,
            string assetName,
            int initialCapacity = 0,
            int maxCapacity = -1,
            PoolLifetime lifetime = PoolLifetime.Scene)
        {
            return new PoolDefinition
            {
                Key = PoolKey.FromAsset(bundleName, assetName),
                Source = PoolSource.Asset,
                BundleName = bundleName,
                AssetName = assetName,
                Options = CreateOptions(initialCapacity, maxCapacity, lifetime)
            };
        }

        public PoolDefinition WithOptions(GameObjectPoolOptions options)
        {
            Options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            return this;
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
    }
}
