using System;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 泛型对象池公共接口
    /// </summary>
    public interface IObjectPool<T> : IDisposable where T : class
    {
        int CountAll { get; }
        int CountActive { get; }
        int CountInactive { get; }
        int MaxCapacity { get; }
        bool IsDisposed { get; }

        T Rent();
        bool TryRent(out T item);
        bool Return(T item);
        int Prewarm(int count);
        void SetMaxCapacity(int maxCapacity);
        void Clear(bool destroyActive = false);
    }
}
