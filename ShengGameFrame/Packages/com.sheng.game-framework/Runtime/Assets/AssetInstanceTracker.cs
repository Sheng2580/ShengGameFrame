using UnityEngine;

namespace Sheng.GameFramework.Assets
{
    /// <summary>
    /// 在实例被外部销毁时通知 AssetManager 释放资源引用
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AssetInstanceTracker : MonoBehaviour
    {
        private AssetManager _owner;
        private int _instanceId;

        internal void Initialize(AssetManager owner, int instanceId)
        {
            _owner = owner;
            _instanceId = instanceId;
        }

        internal void Detach()
        {
            _owner = null;
            _instanceId = 0;
        }

        private void OnDestroy()
        {
            AssetManager owner = _owner;
            int instanceId = _instanceId;
            Detach();
            owner?.NotifyInstanceDestroyed(instanceId);
        }
    }
}
