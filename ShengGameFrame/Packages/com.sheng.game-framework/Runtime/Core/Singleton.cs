namespace Sheng.GameFramework.Core
{
    /// <summary>
    /// 普通 C# 服务使用的线程安全延迟初始化单例
    /// </summary>
    public abstract class Singleton<T> where T : class, new()
    {
        private static readonly object SyncRoot = new object();
        private static T _instance;

        static Singleton()
        {
            SingletonRuntimeReset.ResetRequested += ResetStatics;
        }

        public static T Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                lock (SyncRoot)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }

                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        protected Singleton()
        {
        }

        private static void ResetStatics()
        {
            lock (SyncRoot)
            {
                _instance = null;
            }
        }
    }
}
