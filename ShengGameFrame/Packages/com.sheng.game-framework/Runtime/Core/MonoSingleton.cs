using UnityEngine;

namespace Sheng.GameFramework.Core
{
    /// <summary>
    /// 场景级 MonoBehaviour 单例 未找到实例时自动创建
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;
        private static bool _applicationQuitting;

        static MonoSingleton()
        {
            SingletonRuntimeReset.ResetRequested += ResetStatics;
        }

        public static T Instance
        {
            get
            {
                if (_applicationQuitting)
                {
                    return null;
                }

                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindObjectOfType<T>(true);
                if (_instance != null)
                {
                    return _instance;
                }

                GameObject singletonObject = new GameObject($"[{typeof(T).Name}]");
                _instance = singletonObject.AddComponent<T>();
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual bool PersistAcrossScenes => false;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[{typeof(T).Name}] Duplicate singleton destroyed.", this);
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            gameObject.name = $"[{typeof(T).Name}]";

            if (PersistAcrossScenes)
            {
                if (transform.parent != null)
                {
                    transform.SetParent(null);
                }

                DontDestroyOnLoad(gameObject);
            }

            OnSingletonAwake();
        }

        protected virtual void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            OnSingletonDestroyed();
            _instance = null;
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        protected virtual void OnSingletonAwake()
        {
        }

        protected virtual void OnSingletonDestroyed()
        {
        }

        private static void ResetStatics()
        {
            _instance = null;
            _applicationQuitting = false;
        }
    }
}
