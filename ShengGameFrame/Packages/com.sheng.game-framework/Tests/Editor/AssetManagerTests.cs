using System;
using System.IO;
using NUnit.Framework;
using Sheng.GameFramework.Assets;
using Sheng.GameFramework.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Tests
{
    public sealed class AssetManagerTests
    {
        private const string TestFolder = "Assets/ShengGameFrameworkTests";
        private const string TestAssetPath = TestFolder + "/ReferenceTexture.asset";
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
            AssetHandle<Texture2D> first = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture");
            AssetHandle<Texture2D> second = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.AreSame(first.Asset, second.Asset);
            Assert.AreEqual(2, _manager.GetAssetReferenceCount<Texture2D>(
                TestBundleName,
                "ReferenceTexture"));

            first.Dispose();
            Assert.AreEqual(1, _manager.GetAssetReferenceCount<Texture2D>(
                TestBundleName,
                "ReferenceTexture"));

            second.Dispose();
            Assert.AreEqual(0, _manager.LoadedAssetCount);
        }

        [Test]
        public void KeepLoaded_RemainsCachedUntilExplicitCleanup()
        {
            AssetHandle<Texture2D> handle = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture",
                AssetCachePolicy.KeepLoaded);

            handle.Dispose();

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

            AssetHandle<Texture2D> handle = _manager.LoadAsset<Texture2D>(
                TestBundleName,
                "ReferenceTexture");

            Assert.NotNull(handle);
            Assert.AreEqual(1, _manager.GetBundleReferenceCount(TestBundleName));
            Assert.IsTrue(_manager.IsBundleLoaded(TestBundleName));

            handle.Dispose();

            Assert.AreEqual(0, _manager.GetBundleReferenceCount(TestBundleName));
            Assert.IsFalse(_manager.IsBundleLoaded(TestBundleName));
        }
    }
}
