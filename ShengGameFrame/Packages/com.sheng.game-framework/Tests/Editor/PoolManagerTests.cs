using NUnit.Framework;
using Sheng.GameFramework.Assets;
using Sheng.GameFramework.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Tests
{
    public sealed class PoolableProbe : MonoBehaviour, IPoolable
    {
        public int CreatedCount { get; private set; }
        public int RentCount { get; private set; }
        public int ReturnCount { get; private set; }
        public int DestroyedCount { get; private set; }

        public void OnPoolCreated()
        {
            CreatedCount++;
        }

        public void OnRentFromPool()
        {
            RentCount++;
        }

        public void OnReturnToPool()
        {
            ReturnCount++;
        }

        public void OnPoolDestroyed()
        {
            DestroyedCount++;
        }
    }

    public sealed class PoolManagerTests
    {
        private const string TestFolder = "Assets/ShengGameFrameworkPoolTests";
        private const string TestPrefabPath = TestFolder + "/PoolAsset.prefab";
        private const string TestBundleName = "framework-tests/pooling";

        private PoolManager _manager;
        private AssetManager _assetManager;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("PoolPrefab");
            _prefab.AddComponent<PoolableProbe>();
            _manager = PoolManager.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                Object.DestroyImmediate(_manager.gameObject);
            }

            if (_assetManager != null)
            {
                _assetManager.UnloadAll(false);
                Object.DestroyImmediate(_assetManager.gameObject);
            }

            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
            }

            AssetDatabase.DeleteAsset(TestFolder);
            AssetDatabase.RemoveAssetBundleName(TestBundleName, true);
            AssetDatabase.Refresh();
        }

        [Test]
        public void InitializePool_PrewarmsAndExposesSnapshot()
        {
            PoolKey key = PoolKey.FromName("Bullet");

            Assert.IsTrue(_manager.InitializePool(key, _prefab, 2, 4));

            PoolManagerDebugSnapshot snapshot = _manager.GetDebugSnapshot();
            Assert.AreEqual(1, snapshot.Pools.Count);
            Assert.AreEqual(key, snapshot.Pools[0].PoolKey);
            Assert.AreEqual(PoolState.Ready, snapshot.Pools[0].State);
            Assert.AreEqual(2, snapshot.Pools[0].CountAll);
            Assert.AreEqual(2, snapshot.Pools[0].CountInactive);
            Assert.AreEqual(4, snapshot.Pools[0].MaxCapacity);
        }

        [Test]
        public void RentAndReturn_ReusesInstanceAndInvokesCallbacks()
        {
            PoolKey key = PoolKey.FromName("Effect");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 1, 2));
            PooledHandle first = _manager.Rent(key);
            GameObject firstInstance = first.Instance;
            PoolableProbe probe = first.Get<PoolableProbe>();

            Assert.AreEqual(1, probe.CreatedCount);
            Assert.AreEqual(1, probe.RentCount);
            Assert.AreEqual(1, probe.ReturnCount);
            first.Dispose();
            Assert.AreEqual(2, probe.ReturnCount);

            PooledHandle second = _manager.Rent(key);
            Assert.AreSame(firstInstance, second.Instance);
            Assert.AreEqual(2, probe.RentCount);
            second.Dispose();
        }

        [Test]
        public void Rent_AtCapacityReturnsNull()
        {
            PoolKey key = PoolKey.FromName("Limited");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 0, 1));
            PooledHandle first = _manager.Rent(key);

            LogAssert.Expect(
                LogType.Warning,
                "[PoolManager] 对象池达到最大容量 custom://Limited");
            PooledHandle second = _manager.Rent(key);

            Assert.NotNull(first);
            Assert.IsNull(second);
            first.Dispose();
        }

        [Test]
        public void ComponentApi_RentsAndReturnsWithoutKeyOnReturn()
        {
            PoolKey key = PoolKey.FromName("Component");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 0, 2));

            PoolableProbe probe = _manager.Rent<PoolableProbe>(key);

            Assert.NotNull(probe);
            Assert.IsTrue(_manager.Return(probe));
            Assert.AreEqual(1, _manager.GetDebugSnapshot().Pools[0].CountInactive);
        }

        [Test]
        public void StaleHandle_CannotReturnNewRentGeneration()
        {
            PoolKey key = PoolKey.FromName("Generation");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 1, 1));
            PooledHandle staleHandle = _manager.Rent(key);
            GameObject instance = staleHandle.Instance;
            Assert.IsTrue(_manager.Return(instance));
            PooledHandle currentHandle = _manager.Rent(key);

            staleHandle.Dispose();

            Assert.IsTrue(currentHandle.IsValid);
            Assert.AreEqual(1, _manager.GetDebugSnapshot().Pools[0].CountActive);
            currentHandle.Dispose();
        }

        [Test]
        public void ClearPool_DestroysInactiveAndRetainsRegistration()
        {
            PoolKey key = PoolKey.FromName("Clear");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 2, 3));
            PooledHandle active = _manager.Rent(key);

            Assert.IsTrue(_manager.ClearPool(key));

            PoolDebugInfo info = _manager.GetDebugSnapshot().Pools[0];
            Assert.IsTrue(_manager.IsPoolReady(key));
            Assert.AreEqual(1, info.CountAll);
            Assert.AreEqual(1, info.CountActive);
            Assert.AreEqual(0, info.CountInactive);
            active.Dispose();
        }

        [Test]
        public void SafeDelete_WaitsForActiveInstanceToReturn()
        {
            PoolKey key = PoolKey.FromName("SafeDelete");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 0, 2));
            PooledHandle active = _manager.Rent(key);
            bool? completed = null;

            Assert.IsTrue(_manager.DeletePool(key, false, success => completed = success));
            Assert.IsNull(completed);
            Assert.AreEqual(PoolState.Disposing, _manager.GetPoolState(key));

            active.Dispose();

            Assert.AreEqual(true, completed);
            Assert.IsNull(_manager.GetPoolState(key));
        }

        [Test]
        public void ForceDelete_DestroysActiveInstancesImmediately()
        {
            PoolKey key = PoolKey.FromName("ForceDelete");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 0, 2));
            PooledHandle active = _manager.Rent(key);
            bool? completed = null;

            Assert.IsTrue(_manager.DeletePool(key, true, success => completed = success));

            Assert.AreEqual(true, completed);
            Assert.IsFalse(active.IsValid);
            Assert.IsNull(_manager.GetPoolState(key));
            active.Dispose();
        }

        [Test]
        public void ExternalDestroy_UpdatesPoolCounts()
        {
            PoolKey key = PoolKey.FromName("ExternalDestroy");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 0, 2));
            PooledHandle active = _manager.Rent(key);

            Object.DestroyImmediate(active.Instance);

            PoolDebugInfo info = _manager.GetDebugSnapshot().Pools[0];
            Assert.AreEqual(0, info.CountAll);
            Assert.AreEqual(0, info.CountActive);
            active.Dispose();
        }

        [Test]
        public void ClearPool_IgnoresExternallyDestroyedInactiveInstance()
        {
            PoolKey key = PoolKey.FromName("InactiveDestroy");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 1, 2));
            PooledHandle handle = _manager.Rent(key);
            GameObject instance = handle.Instance;
            handle.Dispose();

            Object.DestroyImmediate(instance);

            Assert.IsTrue(_manager.ClearPool(key));
            PoolDebugInfo info = _manager.GetDebugSnapshot().Pools[0];
            Assert.AreEqual(0, info.CountAll);
            Assert.AreEqual(0, info.CountInactive);
        }

        [Test]
        public void SafeDelete_ExternalDestroyCompletesDeletion()
        {
            PoolKey key = PoolKey.FromName("ExternalDelete");
            Assert.IsTrue(_manager.InitializePool(key, _prefab, 0, 2));
            PooledHandle active = _manager.Rent(key);
            bool? completed = null;
            Assert.IsTrue(_manager.DeletePool(
                key,
                false,
                success => completed = success));

            Object.DestroyImmediate(active.Instance);
            PoolManagerDebugSnapshot snapshot = _manager.GetDebugSnapshot();

            Assert.AreEqual(true, completed);
            Assert.AreEqual(0, snapshot.Pools.Count);
            Assert.IsNull(_manager.GetPoolState(key));
            active.Dispose();
        }

        [Test]
        public void LifetimeDeletion_DistinguishesSceneAndPersistentPools()
        {
            PoolKey sceneKey = PoolKey.FromName("ScenePool");
            PoolKey persistentKey = PoolKey.FromName("PersistentPool");
            Assert.IsTrue(_manager.InitializePool(
                sceneKey,
                _prefab,
                0,
                2,
                PoolLifetime.Scene));
            Assert.IsTrue(_manager.InitializePool(
                persistentKey,
                _prefab,
                0,
                2,
                PoolLifetime.Persistent));

            Assert.AreEqual(1, _manager.DeletePersistentPools());
            Assert.IsTrue(_manager.IsPoolReady(sceneKey));
            Assert.IsFalse(_manager.IsPoolReady(persistentKey));

            Assert.AreEqual(
                1,
                _manager.DeleteScenePools(SceneManager.GetActiveScene()));
            Assert.IsFalse(_manager.IsPoolReady(sceneKey));
        }

        [Test]
        public void AssetBackedPool_HoldsOneHandleUntilDelete()
        {
            CreateAssetPrefab();
            _assetManager = AssetManager.Instance;
            _assetManager.Settings.LoadMode = AssetLoadMode.EditorDatabase;
            _assetManager.Settings.EnableDebugLogs = false;
            bool? initialized = null;

            PoolKey key = _manager.InitializePoolAsync(
                TestBundleName,
                "PoolAsset",
                2,
                3,
                PoolLifetime.Persistent,
                success => initialized = success);

            Assert.AreEqual(true, initialized);
            Assert.IsTrue(_manager.IsPoolReady(key));
            Assert.AreEqual(
                1,
                _assetManager.GetAssetReferenceCount<GameObject>(
                    TestBundleName,
                    "PoolAsset"));

            Assert.IsTrue(_manager.DeletePool(key, true));
            Assert.AreEqual(
                0,
                _assetManager.GetAssetReferenceCount<GameObject>(
                    TestBundleName,
                    "PoolAsset"));
        }

        private static void CreateAssetPrefab()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.CreateFolder("Assets", "ShengGameFrameworkPoolTests");
            }

            GameObject source = new GameObject("PoolAsset");
            PrefabUtility.SaveAsPrefabAsset(source, TestPrefabPath);
            Object.DestroyImmediate(source);

            AssetImporter importer = AssetImporter.GetAtPath(TestPrefabPath);
            importer.SetAssetBundleNameAndVariant(TestBundleName, string.Empty);
            importer.SaveAndReimport();
        }
    }
}
