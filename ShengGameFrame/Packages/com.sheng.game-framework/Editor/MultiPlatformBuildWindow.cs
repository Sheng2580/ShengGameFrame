using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor
{
    /// <summary>
    /// Android 与 Windows PC 多平台构建窗口
    /// </summary>
    public sealed class MultiPlatformBuildWindow : EditorWindow
    {
        private const string MenuPath = "Sheng Game Framework/Build/多平台构建工具";
        private static readonly Vector2 MinimumWindowSize = new Vector2(660f, 510f);

        private bool _developmentBuild;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            MultiPlatformBuildWindow window = GetWindow<MultiPlatformBuildWindow>();
            window.titleContent = new GUIContent("多平台构建");
            window.EnsureWindowSize();
            window.Show();
        }

        [MenuItem("Sheng Game Framework/Build/AssetBundles/Android")]
        public static void BuildAndroidAssetBundles()
        {
            GameBuildPipeline.BuildAndroidAssetBundles();
        }

        [MenuItem("Sheng Game Framework/Build/AssetBundles/当前编辑器")]
        public static void BuildEditorAssetBundles()
        {
            GameBuildPipeline.BuildEditorAssetBundles();
        }

        [MenuItem("Sheng Game Framework/Build/AssetBundles/Windows PC")]
        public static void BuildWindowsAssetBundles()
        {
            GameBuildPipeline.BuildWindowsAssetBundles();
        }

        [MenuItem("Sheng Game Framework/Build/完整包/Android")]
        public static void BuildAndroidPackage()
        {
            GameBuildPipeline.BuildAndroidPackage();
        }

        [MenuItem("Sheng Game Framework/Build/完整包/Windows PC")]
        public static void BuildWindowsPackage()
        {
            GameBuildPipeline.BuildWindowsPackage();
        }

        private void OnEnable()
        {
            EnsureWindowSize();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("多平台构建", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "完整打包会先清理并重新生成目标平台 AB，AB 失败时不会继续生成 Player",
                MessageType.Info);

            DrawProjectSettings();
            EditorGUILayout.Space(8f);
            DrawEditorSection();
            EditorGUILayout.Space(8f);
            DrawAndroidSection();
            EditorGUILayout.Space(8f);
            DrawWindowsSection();
        }

        private void DrawProjectSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("项目", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("产品名称", PlayerSettings.productName);
                EditorGUILayout.LabelField(
                    "启用场景",
                    GameBuildPipeline.GetEnabledScenePaths().Length.ToString());
                _developmentBuild = EditorGUILayout.Toggle(
                    "开发模式",
                    _developmentBuild);
                EditorUserBuildSettings.buildAppBundle = EditorGUILayout.Toggle(
                    "Android 使用 AAB",
                    EditorUserBuildSettings.buildAppBundle);
            }
        }

        private void DrawEditorSection()
        {
            BuildTarget editorTarget = GameBuildPipeline.EditorTarget;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("当前 Unity 编辑器", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("AB 平台", editorTarget.ToString());
                EditorGUILayout.LabelField(
                    "AB 输出",
                    GameBuildPipeline.GetAssetBundleOutputPath(editorTarget));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重新打编辑器 AB", GUILayout.Height(28f)))
                    {
                        GameBuildPipeline.BuildEditorAssetBundles();
                    }

                    if (GUILayout.Button("打开编辑器 AB 目录", GUILayout.Height(28f)))
                    {
                        GameBuildPipeline.OpenAssetBundleOutputFolder(editorTarget);
                    }
                }
            }
        }

        private void DrawAndroidSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Android", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "AB 输出",
                    GameBuildPipeline.GetAssetBundleOutputPath(GameBuildPipeline.AndroidTarget));
                EditorGUILayout.LabelField(
                    "完整包输出",
                    GameBuildPipeline.GetPlayerOutputPath(GameBuildPipeline.AndroidTarget));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("仅重新打 AB", GUILayout.Height(28f)))
                    {
                        GameBuildPipeline.BuildAndroidAssetBundles();
                    }

                    if (GUILayout.Button("完整打包", GUILayout.Height(28f)))
                    {
                        GameBuildPipeline.BuildAndroidPackage(_developmentBuild);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开 AB 目录"))
                    {
                        GameBuildPipeline.OpenAssetBundleOutputFolder(
                            GameBuildPipeline.AndroidTarget);
                    }

                    if (GUILayout.Button("打开完整包目录"))
                    {
                        GameBuildPipeline.OpenPlayerOutputFolder(
                            GameBuildPipeline.AndroidTarget);
                    }
                }
            }
        }

        private void DrawWindowsSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Windows PC", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "AB 输出",
                    GameBuildPipeline.GetAssetBundleOutputPath(GameBuildPipeline.WindowsTarget));
                EditorGUILayout.LabelField(
                    "完整包输出",
                    GameBuildPipeline.GetPlayerOutputPath(GameBuildPipeline.WindowsTarget));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("仅重新打 AB", GUILayout.Height(28f)))
                    {
                        GameBuildPipeline.BuildWindowsAssetBundles();
                    }

                    if (GUILayout.Button("完整打包", GUILayout.Height(28f)))
                    {
                        GameBuildPipeline.BuildWindowsPackage(_developmentBuild);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开 AB 目录"))
                    {
                        GameBuildPipeline.OpenAssetBundleOutputFolder(
                            GameBuildPipeline.WindowsTarget);
                    }

                    if (GUILayout.Button("打开完整包目录"))
                    {
                        GameBuildPipeline.OpenPlayerOutputFolder(
                            GameBuildPipeline.WindowsTarget);
                    }
                }
            }
        }

        private void EnsureWindowSize()
        {
            minSize = MinimumWindowSize;
            if (position.width >= MinimumWindowSize.x
                && position.height >= MinimumWindowSize.y)
            {
                return;
            }

            Rect windowPosition = position;
            windowPosition.width = Mathf.Max(position.width, MinimumWindowSize.x);
            windowPosition.height = Mathf.Max(position.height, MinimumWindowSize.y);
            position = windowPosition;
        }
    }
}
