using System;

namespace Sheng.GameFramework.UI
{
    /// <summary>
    /// 声明 UI 面板的资源地址和显示规则
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIPanelAttribute : Attribute
    {
        public UIPanelAttribute(string assetName)
        {
            AssetName = assetName;
        }

        public string AssetName { get; }
        public string BundleName { get; set; } = "ui";
        public UIAssetSource AssetSource { get; set; } = UIAssetSource.AssetBundle;
        public UILayer Layer { get; set; } = UILayer.Normal;
        public bool UseSafeArea { get; set; } = true;
        public bool Modal { get; set; }
        public bool CloseOnMaskClick { get; set; }
        public bool CacheOnClose { get; set; } = true;
        public float MaskAlpha { get; set; } = 0.55f;
    }
}
