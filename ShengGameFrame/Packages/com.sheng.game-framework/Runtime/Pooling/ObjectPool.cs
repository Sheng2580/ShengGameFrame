using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 基于引用身份和 LIFO 空闲栈的泛型对象池
    /// </summary>
    public sealed class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(T left, T right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(T value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        private readonly Func<T> _create;
        private readonly Action<T> _onRent;
        private readonly Action<T> _onReturn;
        private readonly Action<T> _onDestroy;
        private readonly bool _collectionChecks;
        private readonly Stack<T> _inactiveStack = new Stack<T>();
        private readonly HashSet<T> _all =
            new HashSet<T>(ReferenceComparer.Instance);
        private readonly HashSet<T> _active =
            new HashSet<T>(ReferenceComparer.Instance);
        private readonly HashSet<T> _inactive =
            new HashSet<T>(ReferenceComparer.Instance);

        public ObjectPool(
            Func<T> create,
            int initialCapacity = 0,
            int maxCapacity = -1,
            bool collectionChecks = true,
            Action<T> onRent = null,
            Action<T> onReturn = null,
            Action<T> onDestroy = null)
        {
            ValidateCapacity(initialCapacity, maxCapacity);
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onRent = onRent;
            _onReturn = onReturn;
            _onDestroy = onDestroy;
            _collectionChecks = collectionChecks;
            MaxCapacity = maxCapacity;
            Prewarm(initialCapacity);
        }

        public int CountAll => _all.Count;
        public int CountActive => _active.Count;
        public int CountInactive => _inactive.Count;
        public int MaxCapacity { get; private set; }
        public bool IsDisposed { get; private set; }

        public T Rent()
        {
            EnsureNotDisposed();
            T item = PopInactive();
            if (item == null)
            {
                if (MaxCapacity != -1 && CountAll >= MaxCapacity)
                {
                    return null;
                }

                item = CreateItem();
            }

            _active.Add(item);
            try
            {
                _onRent?.Invoke(item);
                return item;
            }
            catch
            {
                DestroyAfterCallbackFailure(item);
                throw;
            }
        }

        public PoolLease<T> RentLease()
        {
            T item = Rent();
            return item != null ? new PoolLease<T>(this, item) : null;
        }

        public bool TryRent(out T item)
        {
            item = Rent();
            return item != null;
        }

        public bool Return(T item)
        {
            EnsureNotDisposed();
            if (item == null || !_all.Contains(item))
            {
                return RejectInvalidReturn("对象不属于当前对象池");
            }

            if (!_active.Remove(item))
            {
                return RejectInvalidReturn("对象已经归还或当前不是活动状态");
            }

            try
            {
                _onReturn?.Invoke(item);
            }
            catch
            {
                DestroyAfterCallbackFailure(item);
                throw;
            }

            if (MaxCapacity != -1 && CountAll > MaxCapacity)
            {
                DestroyItem(item, true);
                return true;
            }

            _inactive.Add(item);
            _inactiveStack.Push(item);
            return true;
        }

        public int Prewarm(int count)
        {
            EnsureNotDisposed();
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "预热数量不能小于零");
            }

            int createdCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (MaxCapacity != -1 && CountAll >= MaxCapacity)
                {
                    break;
                }

                T item = CreateItem();
                try
                {
                    _onReturn?.Invoke(item);
                }
                catch
                {
                    DestroyAfterCallbackFailure(item);
                    throw;
                }

                _inactive.Add(item);
                _inactiveStack.Push(item);
                createdCount++;
            }

            return createdCount;
        }

        public void SetMaxCapacity(int maxCapacity)
        {
            EnsureNotDisposed();
            ValidateMaxCapacity(maxCapacity);
            MaxCapacity = maxCapacity;
            if (MaxCapacity == -1)
            {
                return;
            }

            while (CountAll > MaxCapacity && CountInactive > 0)
            {
                T item = PopInactive();
                if (item == null)
                {
                    break;
                }

                DestroyItem(item, true);
            }
        }

        public bool Remove(T item, bool invokeDestroy = false)
        {
            if (item == null || !_all.Remove(item))
            {
                return false;
            }

            _active.Remove(item);
            _inactive.Remove(item);
            if (invokeDestroy)
            {
                _onDestroy?.Invoke(item);
            }

            return true;
        }

        internal int RemoveWhere(Predicate<T> predicate, bool invokeDestroy = false)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            T[] items = new T[_all.Count];
            _all.CopyTo(items);
            int removedCount = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (predicate.Invoke(items[i]) && Remove(items[i], invokeDestroy))
                {
                    removedCount++;
                }
            }

            return removedCount;
        }

        public void Clear(bool destroyActive = false)
        {
            if (IsDisposed)
            {
                return;
            }

            T[] inactiveItems = new T[_inactive.Count];
            _inactive.CopyTo(inactiveItems);
            for (int i = 0; i < inactiveItems.Length; i++)
            {
                DestroyItem(inactiveItems[i], true);
            }

            _inactiveStack.Clear();
            if (!destroyActive)
            {
                return;
            }

            T[] activeItems = new T[_active.Count];
            _active.CopyTo(activeItems);
            for (int i = 0; i < activeItems.Length; i++)
            {
                DestroyItem(activeItems[i], true);
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            Clear(true);
            IsDisposed = true;
        }

        private T CreateItem()
        {
            T item = _create.Invoke();
            if (item == null)
            {
                throw new InvalidOperationException("对象池创建委托返回了 null");
            }

            if (!_all.Add(item))
            {
                throw new InvalidOperationException("对象池创建委托返回了已经存在的对象");
            }

            return item;
        }

        private T PopInactive()
        {
            while (_inactiveStack.Count > 0)
            {
                T item = _inactiveStack.Pop();
                if (item != null && _inactive.Remove(item))
                {
                    return item;
                }
            }

            return null;
        }

        private void DestroyItem(T item, bool invokeDestroy)
        {
            if (item == null || !_all.Remove(item))
            {
                return;
            }

            _active.Remove(item);
            _inactive.Remove(item);
            if (invokeDestroy)
            {
                _onDestroy?.Invoke(item);
            }
        }

        private void DestroyAfterCallbackFailure(T item)
        {
            try
            {
                DestroyItem(item, true);
            }
            catch
            {
            }
        }

        private bool RejectInvalidReturn(string message)
        {
            if (_collectionChecks)
            {
                throw new InvalidOperationException(message);
            }

            return false;
        }

        private void EnsureNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ObjectPool<T>));
            }
        }

        private static void ValidateCapacity(int initialCapacity, int maxCapacity)
        {
            ValidateMaxCapacity(maxCapacity);
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialCapacity),
                    "初始化容量不能小于零");
            }

            if (maxCapacity != -1 && initialCapacity > maxCapacity)
            {
                throw new ArgumentException("初始化容量不能超过最大容量");
            }
        }

        private static void ValidateMaxCapacity(int maxCapacity)
        {
            if (maxCapacity == 0 || maxCapacity < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxCapacity),
                    "最大容量必须大于零或等于 -1");
            }
        }
    }
}
