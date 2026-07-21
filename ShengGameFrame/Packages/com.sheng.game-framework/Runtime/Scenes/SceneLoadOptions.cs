using System;
using UnityEngine.SceneManagement;

namespace Sheng.GameFramework.Scenes
{
    /// <summary>
    /// 场景异步加载选项
    /// </summary>
    [Serializable]
    public sealed class SceneLoadOptions
    {
        public LoadSceneMode Mode { get; set; } = LoadSceneMode.Single;
        public bool SetActiveAfterLoad { get; set; }
        public bool UnloadUnusedAssetsAfterLoad { get; set; }
        public float MinimumDuration { get; set; }

        internal SceneLoadOptions Clone()
        {
            return new SceneLoadOptions
            {
                Mode = Mode,
                SetActiveAfterLoad = SetActiveAfterLoad,
                UnloadUnusedAssetsAfterLoad = UnloadUnusedAssetsAfterLoad,
                MinimumDuration = MinimumDuration
            };
        }

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(LoadSceneMode), Mode))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Mode),
                    "场景加载模式无效");
            }

            if (MinimumDuration < 0f
                || float.IsNaN(MinimumDuration)
                || float.IsInfinity(MinimumDuration))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinimumDuration),
                    "最短加载时间必须是大于等于零的有限数值");
            }
        }
    }
}
