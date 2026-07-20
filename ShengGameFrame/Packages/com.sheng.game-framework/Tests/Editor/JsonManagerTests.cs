using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Sheng.GameFramework.Config;
using Sheng.GameFramework.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Tests
{
    public sealed class JsonManagerTests
    {
        public enum TestRank
        {
            Normal,
            Elite
        }

        public sealed class TestSaveData
        {
            public string PlayerName;
            public TestRank Rank;
            public Dictionary<string, int> Values;

            [LubanColumn("display_name")]
            public string DisplayName;

            [LubanIgnore]
            public string RuntimeOnly;
        }

        public sealed class TestTableData
        {
            public int Id;
            public string Name;
        }

        private JsonManager _manager;
        private string _persistentDirectory;
        private string _streamingDirectory;

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            _persistentDirectory = "ShengGameFrameworkJsonTests/" + id;
            _streamingDirectory = "ShengGameFrameworkJsonTests/" + id;
            _manager = JsonManager.Instance;
            _manager.PrettyPrint = true;
            _manager.KeepBackup = true;
        }

        [TearDown]
        public void TearDown()
        {
            DeleteDirectory(Path.Combine(
                Application.persistentDataPath,
                _persistentDirectory));
            DeleteDirectory(Path.Combine(
                Application.streamingAssetsPath,
                _streamingDirectory));
            AssetDatabase.DeleteAsset(
                "Assets/StreamingAssets/ShengGameFrameworkJsonTests");

            if (_manager != null)
            {
                Object.DestroyImmediate(_manager.gameObject);
            }
        }

        [Test]
        public void SerializeAndDeserialize_SupportsChineseEnumDictionaryAndAttributes()
        {
            TestSaveData source = CreateSaveData("玩家一号", 12);

            string json = _manager.SerializeData(source, true);
            bool success = _manager.TryDeserializeData(
                json,
                out TestSaveData restored,
                out string error);

            Assert.IsTrue(success, error);
            StringAssert.Contains("玩家一号", json);
            StringAssert.Contains("\"Elite\"", json);
            StringAssert.Contains("\"display_name\"", json);
            StringAssert.DoesNotContain("RuntimeOnly", json);
            Assert.AreEqual("玩家一号", restored.PlayerName);
            Assert.AreEqual(TestRank.Elite, restored.Rank);
            Assert.AreEqual(12, restored.Values["score"]);
        }

        [Test]
        public void SaveAndLoad_WritesJsonAndRestoresData()
        {
            JsonWriteResult write = _manager.SaveDataDetailed(
                CreateSaveData("存档玩家", 27),
                "slot_01",
                _persistentDirectory);

            JsonReadResult<TestSaveData> read = _manager.LoadDataDetailed<TestSaveData>(
                "slot_01",
                _persistentDirectory);

            Assert.IsTrue(write.Success, write.ErrorMessage);
            Assert.IsTrue(File.Exists(write.Path));
            Assert.IsTrue(read.Success, read.ErrorMessage);
            Assert.AreEqual("存档玩家", read.Value.PlayerName);
            Assert.AreEqual(27, read.Value.Values["score"]);
        }

        [Test]
        public void SaveTwice_WhenBackupEnabled_PreservesPreviousVersion()
        {
            JsonWriteResult first = _manager.SaveDataDetailed(
                CreateSaveData("旧存档", 1),
                "slot_backup",
                _persistentDirectory);
            JsonWriteResult second = _manager.SaveDataDetailed(
                CreateSaveData("新存档", 2),
                "slot_backup",
                _persistentDirectory);

            string backupJson = File.ReadAllText(second.Path + ".bak");
            bool success = _manager.TryDeserializeData(
                backupJson,
                out TestSaveData backup,
                out string error);

            Assert.IsTrue(first.Success, first.ErrorMessage);
            Assert.IsTrue(second.Success, second.ErrorMessage);
            Assert.IsTrue(success, error);
            Assert.AreEqual("旧存档", backup.PlayerName);
        }

        [Test]
        public void PersistentOperations_RejectPathsOutsideDataRoot()
        {
            JsonWriteResult write = _manager.SaveDataDetailed(
                CreateSaveData("非法路径", 0),
                "../../../../outside",
                _persistentDirectory);
            JsonReadResult<TestSaveData> read =
                _manager.LoadDataDetailed<TestSaveData>(
                    "slot",
                    "../../outside");

            Assert.IsFalse(write.Success);
            Assert.AreEqual(JsonErrorCode.InvalidPath, write.ErrorCode);
            Assert.IsFalse(read.Success);
            Assert.AreEqual(JsonErrorCode.InvalidPath, read.ErrorCode);
        }

        [Test]
        public void LoadMalformedJson_ReturnsDeserializeError()
        {
            JsonWriteResult write = _manager.SaveDataDetailed(
                CreateSaveData("损坏前", 3),
                "broken",
                _persistentDirectory);
            File.WriteAllText(write.Path, "{ invalid json");

            JsonReadResult<TestSaveData> read =
                _manager.LoadDataDetailed<TestSaveData>(
                    "broken",
                    _persistentDirectory);

            Assert.IsFalse(read.Success);
            Assert.AreEqual(JsonErrorCode.DeserializeFailed, read.ErrorCode);
        }

        [Test]
        public void ListAndDelete_OperateOnlyInsideSelectedDirectory()
        {
            Assert.IsTrue(_manager.SaveData(
                CreateSaveData("B", 2),
                "slot_b",
                _persistentDirectory));
            Assert.IsTrue(_manager.SaveData(
                CreateSaveData("A", 1),
                "slot_a",
                _persistentDirectory));

            IReadOnlyList<string> names =
                _manager.GetAllDataNames(_persistentDirectory);

            CollectionAssert.AreEqual(new[] { "slot_a", "slot_b" }, names);
            Assert.IsTrue(_manager.DeleteData("slot_a", _persistentDirectory));
            Assert.IsFalse(_manager.Exists("slot_a", _persistentDirectory));
            Assert.IsTrue(_manager.Exists("slot_b", _persistentDirectory));
        }

        [UnityTest]
        public IEnumerator LoadLubanTableAsync_ReadsStreamingAssetsArray()
        {
            string directory = Path.Combine(
                Application.streamingAssetsPath,
                _streamingDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "tbtest.json"),
                "[{\"id\":1,\"name\":\"测试配置\"}]");

            var task = _manager.LoadLubanTableAsync<TestTableData>(
                "tbtest",
                _streamingDirectory);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            JsonReadResult<List<TestTableData>> result = task.Result;
            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(1, result.Value.Count);
            Assert.AreEqual("测试配置", result.Value[0].Name);
        }

        private static TestSaveData CreateSaveData(string playerName, int score)
        {
            return new TestSaveData
            {
                PlayerName = playerName,
                Rank = TestRank.Elite,
                DisplayName = "显示名",
                RuntimeOnly = "不应写入",
                Values = new Dictionary<string, int>
                {
                    { "score", score }
                }
            };
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
