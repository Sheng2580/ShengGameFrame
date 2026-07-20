using Sheng.GameFramework.Pooling;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor
{
    /// <summary>
    /// PoolManager 运行状态调试面板
    /// </summary>
    [CustomEditor(typeof(PoolManager))]
    public sealed class PoolManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            PoolManager manager = (PoolManager)target;
            PoolManagerDebugSnapshot snapshot = manager.GetDebugSnapshot();

            DrawSummary(snapshot);
            DrawPools(manager, snapshot);
            DrawActions(manager);

            if (Application.isPlaying || snapshot.InitializingCount > 0)
            {
                Repaint();
            }
        }

        private static void DrawSummary(PoolManagerDebugSnapshot snapshot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("池数量", snapshot.Pools.Count.ToString());
                EditorGUILayout.LabelField("初始化中", snapshot.InitializingCount.ToString());
            }
        }

        private static void DrawPools(
            PoolManager manager,
            PoolManagerDebugSnapshot snapshot)
        {
            if (snapshot.Pools.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有已注册的对象池", MessageType.Info);
                return;
            }

            for (int i = 0; i < snapshot.Pools.Count; i++)
            {
                PoolDebugInfo pool = snapshot.Pools[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(pool.Key, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "状态与生命周期",
                        $"{pool.State} / {pool.Lifetime}");
                    EditorGUILayout.LabelField("资源", pool.Source);
                    EditorGUILayout.LabelField("所属场景", pool.OwnerScene);
                    EditorGUILayout.LabelField(
                        "对象数量",
                        $"总计 {pool.CountAll}  使用中 {pool.CountActive}  空闲 {pool.CountInactive}");
                    EditorGUILayout.LabelField(
                        "最大容量",
                        pool.MaxCapacity == -1
                            ? "无限"
                            : pool.MaxCapacity.ToString());

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = pool.State == PoolState.Ready;
                        if (GUILayout.Button("清空空闲对象"))
                        {
                            manager.ClearPool(pool.PoolKey);
                        }

                        GUI.enabled = pool.State != PoolState.Disposed;
                        if (GUILayout.Button("安全删除"))
                        {
                            manager.DeletePool(pool.PoolKey, false);
                        }

                        if (GUILayout.Button("强制删除"))
                        {
                            manager.DeletePool(pool.PoolKey, true);
                        }

                        GUI.enabled = true;
                    }
                }
            }
        }

        private static void DrawActions(PoolManager manager)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("清空全部空闲对象"))
                {
                    manager.ClearAllPools();
                }

                if (GUILayout.Button("安全删除全部"))
                {
                    manager.DeleteAllPools(false);
                }

                if (GUILayout.Button("强制删除全部"))
                {
                    manager.DeleteAllPools(true);
                }
            }
        }
    }
}
