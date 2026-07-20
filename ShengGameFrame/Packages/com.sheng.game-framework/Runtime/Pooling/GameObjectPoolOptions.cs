using System;
using UnityEngine;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 对象池场景生命周期
    /// </summary>
    public enum PoolLifetime
    {
        Scene,
        Persistent
    }

    /// <summary>
    /// 对象池运行状态
    /// </summary>
    public enum PoolState
    {
        Initializing,
        Ready,
        Disposing,
        Disposed
    }

    /// <summary>
    /// GameObject 池初始化配置
    /// </summary>
    [Serializable]
    public sealed class GameObjectPoolOptions
    {
        [SerializeField, Min(0)] private int initialCapacity;
        [SerializeField] private int maxCapacity = -1;
        [SerializeField, Min(1)] private int prewarmPerFrame = 4;
        [SerializeField] private PoolLifetime lifetime = PoolLifetime.Scene;
        [SerializeField] private bool resetTransform = true;
        [SerializeField] private bool resetPhysics = true;
        [SerializeField] private bool stopEffectsOnReturn = true;
        [SerializeField] private bool collectionChecks = true;

        public int InitialCapacity
        {
            get => initialCapacity;
            set => initialCapacity = Mathf.Max(0, value);
        }

        public int MaxCapacity
        {
            get => maxCapacity;
            set => maxCapacity = value;
        }

        public int PrewarmPerFrame
        {
            get => prewarmPerFrame;
            set => prewarmPerFrame = Mathf.Max(1, value);
        }

        public PoolLifetime Lifetime
        {
            get => lifetime;
            set => lifetime = value;
        }

        public bool ResetTransform
        {
            get => resetTransform;
            set => resetTransform = value;
        }

        public bool ResetPhysics
        {
            get => resetPhysics;
            set => resetPhysics = value;
        }

        public bool StopEffectsOnReturn
        {
            get => stopEffectsOnReturn;
            set => stopEffectsOnReturn = value;
        }

        public bool CollectionChecks
        {
            get => collectionChecks;
            set => collectionChecks = value;
        }

        public GameObjectPoolOptions Clone()
        {
            return new GameObjectPoolOptions
            {
                InitialCapacity = InitialCapacity,
                MaxCapacity = MaxCapacity,
                PrewarmPerFrame = PrewarmPerFrame,
                Lifetime = Lifetime,
                ResetTransform = ResetTransform,
                ResetPhysics = ResetPhysics,
                StopEffectsOnReturn = StopEffectsOnReturn,
                CollectionChecks = CollectionChecks
            };
        }

        public void Validate()
        {
            if (maxCapacity == 0 || maxCapacity < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxCapacity),
                    "最大容量必须大于零或等于 -1");
            }

            if (maxCapacity != -1 && initialCapacity > maxCapacity)
            {
                throw new ArgumentException("初始化容量不能超过最大容量");
            }

            initialCapacity = Mathf.Max(0, initialCapacity);
            prewarmPerFrame = Mathf.Max(1, prewarmPerFrame);
        }

        internal bool IsEquivalentTo(GameObjectPoolOptions other)
        {
            return other != null
                   && InitialCapacity == other.InitialCapacity
                   && MaxCapacity == other.MaxCapacity
                   && PrewarmPerFrame == other.PrewarmPerFrame
                   && Lifetime == other.Lifetime
                   && ResetTransform == other.ResetTransform
                   && ResetPhysics == other.ResetPhysics
                   && StopEffectsOnReturn == other.StopEffectsOnReturn
                   && CollectionChecks == other.CollectionChecks;
        }
    }
}
