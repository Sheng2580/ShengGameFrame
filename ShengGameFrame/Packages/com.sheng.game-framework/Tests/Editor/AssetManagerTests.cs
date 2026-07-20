using System;
using System.IO;
using NUnit.Framework;
using Sheng.GameFramework.Assets;
using Sheng.GameFramework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Tests
{
    public sealed class AssetManagerTests
    {
        private const string TestFolder = "Assets/ShengGameFrameworkTests";
        private const string TestAssetPath = TestFolder + "/ReferenceTexture.asset";
        private const string TestPrefabPath = TestFolder + "/ReferencePrefab.prefab";
        private const string TestBundleName = "framework-tests/resources";

        private AssetManager _manager;
        private string _temporaryBuildRoot;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "ShengGameFrameworkTests");
            }

            Texture2D texture = new Texture2D(2, 2)
            {
                name = "ReferenceTexture"
            };
            AssetDatabase.CreateAsset(texture, TestAssetPath);
            AssetImporter importer = AssetImporter.GetAtPath(TestAssetPath);
            importer.SetAssetBundleNameAndVariant(TestBundleName, string.Empty);
            importer.SaveAndReimport();

            GameObject prefabObject = new GameObject("ReferencePrefab");
            PrefabUtility.SaveAsPrefabAsset(prefabObject, TestPrefabPath);
            Object.DestroyImmediate(prefabObject);
            AssetImporter prefabImporter = AssetImporter.GetAtPath(TestPrefabPath);
            prefabImporter.SetAssetBundleNameAndVariant(TestBundleName, string.Empty);
            prefabImporter.SaveAndReimport();

            _manager = AssetManager.Instance;
            _manager.Settings.LoadMode = AssetLoadMode.EditorDatabase;
            _manager.Settings.EnableDebugLogs = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                _manager.UnloadAll(false);
                Object.DestroyImmediate(_manager.gameObject);
            }

            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.RemoveAssetBundleName(TestBundleName, true);
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(_temporaryBuildRoot)
                && Directory.Exists(_temporaryBuildRoot))
            {
                Directory.Delete(_temporaryBuildRoot, true);
            }
        }

        [Test]
        public void LoadAsset_ReusesAssetAndReleasesAtZeroReferences()
        {
            Texture2D first = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture");
            Texture2D second = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.AreSame(first, second);
            Assert.AreEqual(2, _manager.GetAssetReferenceCount<Texture2D>(
                TestBundleName,
                "ReferenceTexture"));

            Assert.IsTrue(_manager.ReleaseAsset(first));
            Assert.AreEqual(1, _manager.GetAssetReferenceCount<Texture2D>(
                TestBundleName,
                "ReferenceTexture"));

            Assert.IsTrue(_manager.ReleaseAsset(second));
            Assert.AreEqual(0, _manager.LoadedAssetCount);
        }

        [Test]
        public void KeepLoaded_RemainsCachedUntilExplicitCleanup()
        {
            Texture2D texture = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture",
                AssetCachePolicy.KeepLoaded);

            _manager.ReleaseAsset(texture);

            Assert.AreEqual(1, _manager.LoadedAssetCount);
            Assert.AreEqual(1, _manager.ClearUnusedAssets(true));
            Assert.AreEqual(0, _manager.LoadedAssetCount);
        }

        [Test]
        public void Settings_ClampLoadLimitsToAtLeastOne()
        {
            AssetManagerSettings settings = new AssetManagerSettings
            {
                MaxConcurrentLoads = 0,
                MaxLoadsPerFrame = -5
            };

            Assert.AreEqual(1, settings.MaxConcurrentLoads);
            Assert.AreEqual(1, settings.MaxLoadsPerFrame);
        }

        [Test]
        public void AutoLoadMode_UsesEditorDatabaseInEditor()
        {
            Assert.AreEqual(
                AssetLoadMode.EditorDatabase,
                AssetManager.ResolveLoadMode(AssetLoadMode.Auto));
        }

        [Test]
        public void BundleName_NormalizesNestedPathsAndRejectsTraversal()
        {
            Assert.AreEqual(
                "ui/common",
                AssetBundlePath.NormalizeBundleName("/ui\\common/"));
            Assert.AreEqual(
                string.Empty,
                AssetBundlePath.NormalizeBundleName("ui/../secret"));
        }

        [Test]
        public void AssetBundleMode_LoadsAndUnloadsReferenceCountedBundle()
        {
            string platformName = GameBuildPipeline.ResolvePlatformName(
                GameBuildPipeline.EditorTarget);
            _temporaryBuildRoot = Path.Combine(
                Path.GetTempPath(),
                "ShengGameFrameworkTests",
                Guid.NewGuid().ToString("N"));
            string bundleRoot = Path.Combine(_temporaryBuildRoot, platformName);
            Directory.CreateDirectory(bundleRoot);

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                bundleRoot,
                BuildAssetBundleOptions.ChunkBasedCompression,
                GameBuildPipeline.EditorTarget);
            Assert.NotNull(manifest);

            AssetManagerSettings settings = new AssetManagerSettings
            {
                LoadMode = AssetLoadMode.AssetBundle,
                UnloadBundlesWhenUnused = true
            };
            _manager.Configure(settings, bundleRoot, platformName);

            Texture2D texture = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture");

            Assert.NotNull(texture);
            Assert.AreEqual(1, _manager.GetBundleReferenceCount(TestBundleName));
            Assert.IsTrue(_manager.IsBundleLoaded(TestBundleName));

            _manager.ReleaseAsset(texture);

            Assert.AreEqual(0, _manager.GetBundleReferenceCount(TestBundleName));
            Assert.IsFalse(_manager.IsBundleLoaded(TestBundleName));
        }

        [Test]
        public void LoadAssetAsync_ReturnsAssetWithoutExposingHandle()
        {
            Texture2D loaded = null;
            bool failed = false;

            _manager.LoadAssetAsync<Texture2D>(
                TestBundleName,
                "ReferenceTexture",
                asset => loaded = asset,
                () => failed = true);

            Assert.NotNull(loaded);
            Assert.IsFalse(failed);
            Assert.AreEqual(1, _manager.GetAssetReferenceCount<Texture2D>(
                TestBundleName,
                "ReferenceTexture"));
            Assert.IsTrue(_manager.ReleaseAsset(loaded));
        }

        [Test]
        public void LoadAssetAsync_WithoutGenericPreloadsGameObject()
        {
            bool completed = false;

            _manager.LoadAssetAsync(
                TestBundleName,
                "ReferencePrefab",
                () => completed = true);

            Assert.IsTrue(completed);
            Assert.AreEqual(1, _manager.GetAssetReferenceCount<GameObject>(
                TestBundleName,
                "ReferencePrefab"));
            Assert.IsTrue(_manager.ReleaseAsset<GameObject>(
                TestBundleName,
                "ReferencePrefab"));
        }

        [Test]
        public void LoadAssetAsync_OnFailureOnlyInvokesFailedCallback()
        {
            bool completed = false;
            bool failed = false;

            LogAssert.Expect(
                LogType.Error,
                $"[AssetManager] Editor 中找不到资源 {TestBundleName}/MissingTexture");
            LogAssert.Expect(
                LogType.Error,
                $"[AssetManager] 加载失败 {TestBundleName}/MissingTexture 类型 Texture2D");

            _manager.LoadAssetAsync<Texture2D>(
                TestBundleName,
                "MissingTexture",
                asset => completed = true,
                () => failed = true);

            Assert.IsFalse(completed);
            Assert.IsTrue(failed);
        }

        [Test]
        public void InstantiateAsync_ReleaseInstanceReleasesPrefabReference()
        {
            GameObject instance = null;

            _manager.InstantiateAsync(
                TestBundleName,
                "ReferencePrefab",
                loadedInstance => instance = loadedInstance);

            Assert.NotNull(instance);
            Assert.AreEqual(1, _manager.GetAssetReferenceCount<GameObject>(
                TestBundleName,
                "ReferencePrefab"));

            Assert.IsTrue(_manager.ReleaseInstance(instance));

            Assert.AreEqual(0, _manager.GetAssetReferenceCount<GameObject>(
                TestBundleName,
                "ReferencePrefab"));
        }
    }
}
