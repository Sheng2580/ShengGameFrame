using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Sheng.GameFramework.Editor.AgentBridge
{
    /// <summary>
    /// 提供给 AI 的项目与运行时只读诊断
    /// </summary>
    internal static class FrameworkAgentDiagnostics
    {
        private const int MaximumHierarchyNodes = 2000;
        private const int MaximumIssues = 200;

        public static AgentProjectSnapshot CreateProjectSnapshot()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();
            AgentProjectSnapshot snapshot = new AgentProjectSnapshot
            {
                productName = PlayerSettings.productName,
                unityVersion = Application.unityVersion,
                frameworkVersion = GetPackageVersion("Packages/com.sheng.game-framework/package.json"),
                bridgeVersion = GetPackageVersion("Packages/com.unityagentbridge.server/package.json"),
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                activeScene = activeScene.IsValid() ? activeScene.path : string.Empty,
                activeSceneDirty = activeScene.IsValid() && activeScene.isDirty,
                isPlaying = EditorApplication.isPlaying,
                isCompiling = EditorApplication.isCompiling,
                runInBackground = Application.runInBackground,
                loadedSceneCount = SceneManager.sceneCount,
                assetBundleCount = bundleNames.Length
            };

            snapshot.enabledScenes.AddRange(GameBuildPipeline.GetEnabledScenePaths());
            snapshot.assetBundles.AddRange(bundleNames);
            return snapshot;
        }

        public static AgentValidationReport ValidateProject()
        {
            AgentValidationReport report = new AgentValidationReport();
            ValidateBuildScenes(report);
            ValidateLoadedScenes(report);
            ValidatePrefabs(report);
            report.success = report.errorCount == 0;
            return report;
        }

        public static AgentSceneHierarchy CreateSceneHierarchy()
        {
            Scene scene = SceneManager.GetActiveScene();
            AgentSceneHierarchy hierarchy = new AgentSceneHierarchy
            {
                scene = scene.IsValid() ? scene.path : string.Empty
            };

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return hierarchy;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                AddSceneNode(roots[i].transform, roots[i].name, hierarchy);
                if (hierarchy.truncated)
                {
                    break;
                }
            }

            hierarchy.nodeCount = hierarchy.nodes.Count;
            return hierarchy;
        }

        public static AgentUIHierarchy CreateUIHierarchy()
        {
            Scene scene = SceneManager.GetActiveScene();
            AgentUIHierarchy hierarchy = new AgentUIHierarchy
            {
                scene = scene.IsValid() ? scene.path : string.Empty
            };

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return hierarchy;
            }

            HashSet<int> canvasIds = new HashSet<int>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                RectTransform[] rectTransforms =
                    roots[i].GetComponentsInChildren<RectTransform>(true);
                for (int j = 0; j < rectTransforms.Length; j++)
                {
                    RectTransform rectTransform = rectTransforms[j];
                    Canvas canvas = rectTransform.GetComponentInParent<Canvas>(true);
                    if (canvas == null)
                    {
                        continue;
                    }

                    canvasIds.Add(canvas.GetInstanceID());
                    hierarchy.elements.Add(CreateUIElement(rectTransform, canvas));
                    if (hierarchy.elements.Count >= MaximumHierarchyNodes)
                    {
                        hierarchy.truncated = true;
                        break;
                    }
                }

                if (hierarchy.truncated)
                {
                    break;
                }
            }

            hierarchy.canvasCount = canvasIds.Count;
            hierarchy.elementCount = hierarchy.elements.Count;
            return hierarchy;
        }

        public static AgentRuntimeSnapshot CreateRuntimeSnapshot()
        {
            AgentRuntimeSnapshot snapshot = new AgentRuntimeSnapshot
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                frameCount = Time.frameCount,
                deltaTime = Time.deltaTime,
                framesPerSecond = Time.deltaTime > 0f ? 1f / Time.deltaTime : 0f,
                timeScale = Time.timeScale,
                monoUsedBytes = Profiler.GetMonoUsedSizeLong(),
                totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                totalReservedBytes = Profiler.GetTotalReservedMemoryLong()
            };

            if (!EditorApplication.isPlaying)
            {
                return snapshot;
            }

            snapshot.gameObjectCount = Object.FindObjectsOfType<GameObject>(true).Length;
            snapshot.cameraCount = Object.FindObjectsOfType<Camera>(true).Length;
            snapshot.canvasCount = Object.FindObjectsOfType<Canvas>(true).Length;
            return snapshot;
        }

        public static AgentEditorState CreateEditorState()
        {
            Scene scene = SceneManager.GetActiveScene();
            return new AgentEditorState
            {
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                isUpdating = EditorApplication.isUpdating,
                activeScene = scene.IsValid() ? scene.path : string.Empty,
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
        }

        private static void ValidateBuildScenes(AgentValidationReport report)
        {
            string[] scenePaths = GameBuildPipeline.GetEnabledScenePaths();
            if (scenePaths.Length == 0)
            {
                AddIssue(report, "Error", "BUILD_SCENES_EMPTY", "没有启用的构建场景", string.Empty);
                return;
            }

            for (int i = 0; i < scenePaths.Length; i++)
            {
                string path = scenePaths[i];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    AddIssue(report, "Error", "BUILD_SCENE_MISSING", "构建场景不存在", path);
                }
            }
        }

        private static void ValidateLoadedScenes(AgentValidationReport report)
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    ValidateMissingScripts(
                        roots[i],
                        string.IsNullOrEmpty(scene.path) ? scene.name : scene.path,
                        report);
                }
            }
        }

        private static void ValidatePrefabs(AgentValidationReport report)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            report.scannedPrefabCount = prefabGuids.Length;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    ValidateMissingScripts(prefab, path, report);
                }

                if (report.issues.Count >= MaximumIssues)
                {
                    AddIssue(
                        report,
                        "Warning",
                        "ISSUE_LIMIT_REACHED",
                        "问题数量达到输出上限",
                        string.Empty);
                    return;
                }
            }
        }

        private static void ValidateMissingScripts(
            GameObject root,
            string assetPath,
            AgentValidationReport report)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            report.scannedObjectCount += transforms.Length;
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject gameObject = transforms[i].gameObject;
                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount <= 0)
                {
                    continue;
                }

                AddIssue(
                    report,
                    "Error",
                    "MISSING_SCRIPT",
                    $"对象 {GetTransformPath(gameObject.transform)} 存在 {missingCount} 个丢失脚本",
                    assetPath);
                if (report.issues.Count >= MaximumIssues)
                {
                    return;
                }
            }
        }

        private static void AddSceneNode(
            Transform transform,
            string path,
            AgentSceneHierarchy hierarchy)
        {
            if (hierarchy.nodes.Count >= MaximumHierarchyNodes)
            {
                hierarchy.truncated = true;
                return;
            }

            AgentSceneNode node = new AgentSceneNode
            {
                path = path,
                activeSelf = transform.gameObject.activeSelf,
                activeInHierarchy = transform.gameObject.activeInHierarchy,
                layer = transform.gameObject.layer,
                tag = transform.gameObject.tag,
                position = transform.position,
                rotation = transform.eulerAngles,
                scale = transform.localScale
            };
            AddComponentNames(transform.gameObject, node.components);
            hierarchy.nodes.Add(node);

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                AddSceneNode(child, path + "/" + child.name, hierarchy);
                if (hierarchy.truncated)
                {
                    return;
                }
            }
        }

        private static AgentUIElement CreateUIElement(RectTransform rectTransform, Canvas canvas)
        {
            AgentUIElement element = new AgentUIElement
            {
                path = GetTransformPath(rectTransform),
                canvas = GetTransformPath(canvas.transform),
                active = rectTransform.gameObject.activeInHierarchy,
                anchorMin = rectTransform.anchorMin,
                anchorMax = rectTransform.anchorMax,
                pivot = rectTransform.pivot,
                anchoredPosition = rectTransform.anchoredPosition,
                sizeDelta = rectTransform.sizeDelta,
                localScale = rectTransform.localScale
            };
            AddComponentNames(rectTransform.gameObject, element.components);
            return element;
        }

        private static void AddComponentNames(GameObject gameObject, List<string> names)
        {
            Component[] components = gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                names.Add(component == null ? "<MissingScript>" : component.GetType().FullName);
            }
        }

        private static string GetTransformPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static void AddIssue(
            AgentValidationReport report,
            string severity,
            string code,
            string message,
            string path)
        {
            if (report.issues.Count >= MaximumIssues)
            {
                return;
            }

            report.issues.Add(new AgentIssue
            {
                severity = severity,
                code = code,
                message = message,
                path = path
            });

            if (string.Equals(severity, "Error", StringComparison.Ordinal))
            {
                report.errorCount++;
            }
            else if (string.Equals(severity, "Warning", StringComparison.Ordinal))
            {
                report.warningCount++;
            }
        }

        private static string GetPackageVersion(string packagePath)
        {
            PackageManagerPackageInfo packageInfo =
                PackageManagerPackageInfo.FindForAssetPath(packagePath);
            return packageInfo != null ? packageInfo.version : string.Empty;
        }
    }
}
