using Sheng.GameFramework.Assets;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor
{
    /// <summary>
    /// AssetManager 运行状态调试面板
    /// </summary>
    [CustomEditor(typeof(AssetManager))]
    public sealed class AssetManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "进入 Play Mode 后显示资源引用 Bundle 引用和加载队列",
                    MessageType.Info);
                return;
            }

            AssetManager manager = (AssetManager)target;
            AssetManagerDebugSnapshot snapshot = manager.GetDebugSnapshot();
            DrawSummary(snapshot);
            DrawAssets(snapshot);
            DrawBundles(snapshot);
            DrawActions(manager);

            if (snapshot.ActiveLoadCount > 0 || snapshot.QueuedLoadCount > 0)
            {
                Repaint();
            }
        }

        private static void DrawSummary(AssetManagerDebugSnapshot snapshot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("加载模式", snapshot.EffectiveLoadMode.ToString());
                EditorGUILayout.LabelField("已缓存资源", snapshot.Assets.Count.ToString());
                EditorGUILayout.LabelField("活动请求", snapshot.ActiveLoadCount.ToString());
                EditorGUILayout.LabelField("排队请求", snapshot.QueuedLoadCount.ToString());
            }
        }

        private static void DrawAssets(AssetManagerDebugSnapshot snapshot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Asset 缓存", EditorStyles.boldLabel);
                if (snapshot.Assets.Count == 0)
                {
                    EditorGUILayout.LabelField("无");
                    return;
                }

                for (int i = 0; i < snapshot.Assets.Count; i++)
                {
                    AssetDebugInfo asset = snapshot.Assets[i];
                    EditorGUILayout.LabelField(
                        $"{asset.BundleName}/{asset.AssetName}",
                        $"{asset.AssetType}  Ref {asset.ReferenceCount}  {asset.CachePolicy}");
                }
            }
        }

        private static void DrawBundles(AssetManagerDebugSnapshot snapshot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("AssetBundle 缓存", EditorStyles.boldLabel);
                if (snapshot.Bundles.Count == 0)
                {
                    EditorGUILayout.LabelField("无");
                    return;
                }

                for (int i = 0; i < snapshot.Bundles.Count; i++)
                {
                    BundleDebugInfo bundle = snapshot.Bundles[i];
                    EditorGUILayout.LabelField(
                        bundle.BundleName,
                        $"Ref {bundle.ReferenceCount}");
                }
            }
        }

        private static void DrawActions(AssetManager manager)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("清理零引用资源"))
                {
                    manager.ClearUnusedAssets(true);
                    manager.UnloadUnusedBundles();
                }

                if (GUILayout.Button("卸载全部"))
                {
                    manager.UnloadAll(false);
                }
            }
        }
    }
}
