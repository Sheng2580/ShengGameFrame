using System;
using UnityEngine;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// Dispose 时自动归还 GameObject
    /// </summary>
    public sealed class PooledHandle : IDisposable
    {
        private PoolManager _owner;
        private int _rentVersion;

        internal PooledHandle(PoolManager owner, PooledObject marker)
        {
            _owner = owner;
            Marker = marker;
            _rentVersion = marker.RentVersion;
        }

        internal PooledObject Marker { get; private set; }
        public GameObject Instance => Marker != null ? Marker.gameObject : null;
        public bool IsReleased { get; private set; }
        public bool IsValid => !IsReleased
                               && Marker != null
                               && Marker.IsRented
                               && Marker.RentVersion == _rentVersion;

        public T Get<T>() where T : Component
        {
            return Instance != null ? Instance.GetComponent<T>() : null;
        }

        public GameObject ReleaseOwnership()
        {
            if (IsReleased)
            {
                return null;
            }

            GameObject instance = Instance;
            IsReleased = true;
            _owner = null;
            Marker = null;
            _rentVersion = 0;
            return instance;
        }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            PoolManager owner = _owner;
            PooledObject marker = Marker;
            int rentVersion = _rentVersion;
            _owner = null;
            Marker = null;
            _rentVersion = 0;
            owner?.ReturnInternal(marker, rentVersion, false);
        }
    }
}
