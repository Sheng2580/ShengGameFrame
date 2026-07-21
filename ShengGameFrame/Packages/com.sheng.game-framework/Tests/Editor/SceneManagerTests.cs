using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Sheng.GameFramework.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Sheng.GameFramework.Tests
{
    public sealed class SceneManagerTests
    {
        [Test]
        public void Options_ClonePreservesValuesAndIsIndependent()
        {
            SceneLoadOptions options = new SceneLoadOptions
            {
                Mode = LoadSceneMode.Additive,
                SetActiveAfterLoad = true,
                UnloadUnusedAssetsAfterLoad = true,
                MinimumDuration = 1.5f
            };

            SceneLoadOptions clone = options.Clone();
            options.MinimumDuration = 3f;

            Assert.AreEqual(LoadSceneMode.Additive, clone.Mode);
            Assert.IsTrue(clone.SetActiveAfterLoad);
            Assert.IsTrue(clone.UnloadUnusedAssetsAfterLoad);
            Assert.AreEqual(1.5f, clone.MinimumDuration);
        }

        [Test]
        public void Options_RejectsInvalidMinimumDuration()
        {
            SceneLoadOptions options = new SceneLoadOptions
            {
                MinimumDuration = float.PositiveInfinity
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [Test]
        public void Queue_StartsRequestsInFirstInFirstOutOrder()
        {
            SceneLoadQueue queue = new SceneLoadQueue();
            SceneLoadRequest first = CreateRequest(1, "First");
            SceneLoadRequest second = CreateRequest(2, "Second");
            queue.Enqueue(first);
            queue.Enqueue(second);

            Assert.IsTrue(queue.TryBeginNext(out SceneLoadRequest startedFirst));
            Assert.AreSame(first, startedFirst);
            Assert.IsFalse(queue.TryBeginNext(out _));
            Assert.IsTrue(queue.CompleteCurrent(first));
            Assert.IsTrue(queue.TryBeginNext(out SceneLoadRequest startedSecond));
            Assert.AreSame(second, startedSecond);
        }

        [Test]
        public void Queue_OnlyCancelsPendingRequests()
        {
            SceneLoadQueue queue = new SceneLoadQueue();
            SceneLoadRequest active = CreateRequest(1, "Active");
            SceneLoadRequest pending = CreateRequest(2, "Pending");
            queue.Enqueue(active);
            queue.Enqueue(pending);
            queue.TryBeginNext(out _);

            Assert.IsFalse(queue.CancelPending(active));
            Assert.IsTrue(queue.CancelPending(pending));
            Assert.AreEqual(0, queue.PendingCount);
            Assert.AreSame(active, queue.Current);
        }

        [Test]
        public void Request_ProgressDoesNotRegressAndCompletionSetsOne()
        {
            SceneLoadRequest request = CreateRequest(1, "Game");
            request.MarkLoading();

            Assert.IsTrue(request.ReportProgress(0.7f));
            Assert.IsFalse(request.ReportProgress(0.4f));
            Assert.AreEqual(0.7f, request.Progress);

            request.MarkSucceeded();

            Assert.AreEqual(SceneLoadStatus.Succeeded, request.Status);
            Assert.AreEqual(1f, request.Progress);
            Assert.IsTrue(request.IsDone);
            Assert.IsTrue(request.Succeeded);
        }

        private static SceneLoadRequest CreateRequest(int id, string sceneName)
        {
            return new SceneLoadRequest(
                id,
                sceneName,
                new SceneLoadOptions(),
                null,
                null,
                null);
        }
    }

    public sealed class SceneManagerIntegrationTests
    {
        private const string TestFolder = "Assets/ShengGameFrameworkSceneTests";
        private const string BaseScenePath = TestFolder + "/Base.unity";
        private const string AdditiveScenePath = TestFolder + "/Additive.unity";
        private const string BackupKey =
            "Sheng.GameFramework.Tests.SceneManagerIntegration.Backup";

        [Serializable]
        private sealed class BuildSceneBackup
        {
            public string Path;
            public bool Enabled;
        }

        [Serializable]
        private sealed class TestStateBackup
        {
            public string ActiveScenePath;
            public List<BuildSceneBackup> BuildScenes =
                new List<BuildSceneBackup>();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Application.isPlaying)
            {
                yield break;
            }

            RestorePersistedTestState();
            SaveCurrentTestState();
            CreateTestScenes();
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Sheng.GameFramework.Scenes.SceneManager manager =
                Object.FindObjectOfType<Sheng.GameFramework.Scenes.SceneManager>(true);
            if (manager != null)
            {
                Object.Destroy(manager.gameObject);
                yield return null;
            }

            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            RestorePersistedTestState();
        }

        [UnityTest]
        public IEnumerator AdditiveLoad_ReportsProgressActivatesAndUnloads()
        {
            Sheng.GameFramework.Scenes.SceneManager manager =
                Sheng.GameFramework.Scenes.SceneManager.Instance;
            List<float> progressValues = new List<float>();
            int loadedEventCount = 0;
            int unloadedEventCount = 0;
            bool completed = false;
            bool unloadCompleted = false;
            manager.SceneLoaded += (_, mode) =>
            {
                if (mode == LoadSceneMode.Additive)
                {
                    loadedEventCount++;
                }
            };
            manager.SceneUnloaded += scene =>
            {
                if (scene.path == AdditiveScenePath)
                {
                    unloadedEventCount++;
                }
            };

            SceneLoadRequest request = manager.LoadSceneAsync(
                AdditiveScenePath,
                new SceneLoadOptions
                {
                    Mode = LoadSceneMode.Additive,
                    SetActiveAfterLoad = true,
                    MinimumDuration = 0.05f
                },
                _ => completed = true,
                progress => progressValues.Add(progress));

            yield return WaitUntil(() => request.IsDone, 10f);

            Assert.IsTrue(request.Succeeded, request.Error);
            Assert.IsTrue(completed);
            Assert.AreEqual(1f, request.Progress);
            Assert.AreEqual(1, loadedEventCount);
            Assert.AreEqual(
                AdditiveScenePath,
                UnitySceneManager.GetActiveScene().path);
            AssertProgressIsMonotonic(progressValues);

            Assert.IsTrue(manager.UnloadSceneAsync(
                AdditiveScenePath,
                () => unloadCompleted = true));
            yield return WaitUntil(() => unloadCompleted, 10f);

            Assert.AreEqual(1, unloadedEventCount);
            Assert.IsFalse(manager.IsSceneLoaded(AdditiveScenePath));
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), "等待场景操作超时");
        }

        private static void AssertProgressIsMonotonic(List<float> values)
        {
            Assert.IsNotEmpty(values);
            Assert.AreEqual(0f, values[0]);
            Assert.AreEqual(1f, values[values.Count - 1]);
            for (int i = 1; i < values.Count; i++)
            {
                Assert.GreaterOrEqual(values[i], values[i - 1]);
            }
        }

        private void CreateTestScenes()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "ShengGameFrameworkSceneTests");
            }

            Scene baseScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(baseScene, BaseScenePath));

            Scene additiveScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(
                additiveScene,
                AdditiveScenePath));

            List<EditorBuildSettingsScene> buildScenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            buildScenes.Add(new EditorBuildSettingsScene(BaseScenePath, true));
            buildScenes.Add(new EditorBuildSettingsScene(AdditiveScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            EditorSceneManager.OpenScene(BaseScenePath, OpenSceneMode.Single);
        }

        private static void SaveCurrentTestState()
        {
            TestStateBackup backup = new TestStateBackup
            {
                ActiveScenePath = EditorSceneManager.GetActiveScene().path
            };
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                backup.BuildScenes.Add(new BuildSceneBackup
                {
                    Path = buildScenes[i].path,
                    Enabled = buildScenes[i].enabled
                });
            }

            SessionState.SetString(BackupKey, JsonUtility.ToJson(backup));
        }

        private static void RestorePersistedTestState()
        {
            string json = SessionState.GetString(BackupKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            TestStateBackup backup = JsonUtility.FromJson<TestStateBackup>(json);
            List<EditorBuildSettingsScene> buildScenes =
                new List<EditorBuildSettingsScene>();
            if (backup?.BuildScenes != null)
            {
                for (int i = 0; i < backup.BuildScenes.Count; i++)
                {
                    BuildSceneBackup scene = backup.BuildScenes[i];
                    buildScenes.Add(new EditorBuildSettingsScene(
                        scene.Path,
                        scene.Enabled));
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            RestoreOriginalScene(backup?.ActiveScenePath);
            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.Refresh();
            SessionState.EraseString(BackupKey);
        }

        private static void RestoreOriginalScene(string scenePath)
        {
            if (!string.IsNullOrEmpty(scenePath)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                return;
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }
    }
}
