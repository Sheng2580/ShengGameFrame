namespace Sheng.GameFramework.Core
{
    /// <summary>
    /// 切换场景时不会销毁的 MonoBehaviour 单例
    /// </summary>
    public abstract class PersistentMonoSingleton<T> : MonoSingleton<T>
        where T : PersistentMonoSingleton<T>
    {
        protected sealed override bool PersistAcrossScenes => true;
    }
}
