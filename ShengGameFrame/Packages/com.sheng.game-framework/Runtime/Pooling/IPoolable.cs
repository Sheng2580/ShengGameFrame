namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// GameObject 对象池生命周期
    /// </summary>
    public interface IPoolable
    {
        void OnPoolCreated();
        void OnRentFromPool();
        void OnReturnToPool();
        void OnPoolDestroyed();
    }
}
