using System;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// Dispose 时自动归还泛型对象
    /// </summary>
    public sealed class PoolLease<T> : IDisposable where T : class
    {
        private ObjectPool<T> _pool;

        internal PoolLease(ObjectPool<T> pool, T item)
        {
            _pool = pool;
            Item = item;
        }

        public T Item { get; private set; }
        public bool IsReleased { get; private set; }

        public void Dispose()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            ObjectPool<T> pool = _pool;
            T item = Item;
            _pool = null;
            Item = null;
            if (pool != null && !pool.IsDisposed)
            {
                pool.Return(item);
            }
        }
    }
}
