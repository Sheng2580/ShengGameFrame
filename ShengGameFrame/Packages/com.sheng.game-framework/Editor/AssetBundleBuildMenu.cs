using UnityEditor;

namespace Sheng.GameFramework.Editor
{
    public static class AssetBundleBuildMenu
    {
        private const string MenuRoot = "Sheng Game Framework/AssetBundles/";

        [MenuItem(MenuRoot + "Build Active Target")]
        public static void BuildActiveTarget()
        {
            GameBuildPipeline.BuildAssetBundles(
                EditorUserBuildSettings.activeBuildTarget,
                true);
        }

        [MenuItem(MenuRoot + "Open Output Folder")]
        public static void OpenOutputFolder()
        {
            GameBuildPipeline.OpenAssetBundleOutputFolder(
                EditorUserBuildSettings.activeBuildTarget);
        }
    }
}
