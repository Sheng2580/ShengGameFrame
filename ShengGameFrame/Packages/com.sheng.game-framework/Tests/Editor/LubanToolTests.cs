using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Sheng.GameFramework.Config;
using Sheng.GameFramework.Editor.Luban;
using Sheng.GameFramework.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sheng.GameFramework.Tests
{
    public sealed class LubanToolTests
    {
        private enum ItemQuality
        {
            Normal,
            Rare
        }

        [LubanTable("test_weapon", Comment = "测试武器")]
        private sealed class WeaponConfig
        {
            [LubanKey]
            public int Id;

            [LubanColumn(Comment = "名称")]
            public string Name;

            public ItemQuality Quality;
            public List<int> Costs;
            public int? UnlockWave;

            [LubanIgnore]
            public string RuntimeCache;
        }

        [LubanTable("test_weapon")]
        private sealed class ExpandedWeaponConfig
        {
            [LubanKey]
            public int Id;

            public string Name;

            [LubanColumn(DefaultValue = "10")]
            public int Damage;
        }

        [LubanTable("migration_test")]
        private sealed class BeforeRenameConfig
        {
            [LubanKey]
            public int Id;

            public string Name;
        }

        [LubanTable("migration_test")]
        private sealed class AfterRenameConfig
        {
            [LubanKey]
            public int Id;

            [LubanColumn("displayName", FormerName = "name")]
            public string DisplayName;
        }

        [LubanTable("validation_test")]
        private sealed class ValidationConfig
        {
            [LubanKey]
            public int Id;

            public ItemQuality Quality;

            [LubanColumn(Required = true)]
            public string Title;
        }

        [LubanTable("missing_key")]
        private sealed class MissingKeyConfig
        {
            public int Id;
        }

        [LubanTable("invalid_output", OutputName = "../outside")]
        private sealed class InvalidOutputConfig
        {
            [LubanKey]
            public int Id;
        }

        [LubanTable("invalid_column")]
        private sealed class InvalidColumnConfig
        {
            [LubanKey]
            public int Id;

            [LubanColumn("bad column")]
            public string Value;
        }

        [LubanTable("generic_config")]
        private sealed class GenericConfig<T>
        {
            [LubanKey]
            public int Id;
        }

        [LubanTable("nullable_list")]
        private sealed class NullableListConfig
        {
            [LubanKey]
            public int Id;

            public List<int?> Values;
        }

        private string _root;
        private string _dataDirectory;
        private string _backupDirectory;
        private JsonManager _jsonManager;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(),
                "ShengGameFrameworkLubanTests",
                Guid.NewGuid().ToString("N"));
            _dataDirectory = Path.Combine(_root, "Datas");
            _backupDirectory = Path.Combine(_root, "Backups");
            Directory.CreateDirectory(_dataDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (_jsonManager != null)
            {
                Object.DestroyImmediate(_jsonManager.gameObject);
            }

            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void Scanner_MapsSupportedFieldsAndAttributes()
        {
            LubanScanResult scan = LubanTableScanner.ScanTypes(
                new[] { typeof(WeaponConfig) });

            Assert.IsTrue(scan.Success, string.Join("\n", scan.Errors));
            Assert.AreEqual(1, scan.Tables.Count);
            LubanTableDescriptor table = scan.Tables[0];
            Assert.AreEqual("test_weapon", table.TableName);
            Assert.AreEqual("tbtest_weapon", table.OutputName);
            Assert.AreEqual(5, table.Columns.Count);
            Assert.AreEqual("int", FindColumn(table, "id").LubanType);
            Assert.AreEqual("string", FindColumn(table, "quality").LubanType);
            Assert.AreEqual("list#sep=(,),int", FindColumn(table, "costs").LubanType);
            Assert.AreEqual("int?", FindColumn(table, "unlockWave").LubanType);
            Assert.IsNull(FindColumnOrNull(table, "runtimeCache"));
        }

        [Test]
        public void Scanner_ReportsMissingKeyAndUnsafeOutputName()
        {
            LubanScanResult scan = LubanTableScanner.ScanTypes(
                new[]
                {
                    typeof(MissingKeyConfig),
                    typeof(InvalidOutputConfig),
                    typeof(InvalidColumnConfig),
                    typeof(GenericConfig<>),
                    typeof(NullableListConfig)
                });

            Assert.IsFalse(scan.Success);
            Assert.AreEqual(5, scan.Errors.Count);
            string errors = string.Join("\n", scan.Errors);
            StringAssert.Contains("必须声明一个 LubanKey", errors);
            StringAssert.Contains("输出名无效", errors);
            StringAssert.Contains("字段列名无效", errors);
            StringAssert.Contains("必须是普通非抽象 C# 类", errors);
            StringAssert.Contains("集合元素不能使用可空类型", errors);
        }

        [Test]
        public void SynchronizeTable_CreatesReadableWorkbook()
        {
            LubanTableDescriptor table = GetDescriptor<WeaponConfig>();

            LubanTableSyncResult sync = LubanWorkbookService.SynchronizeTable(
                table,
                _dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> rows = LubanWorkbookService.ReadWorkbook(sync.FilePath);

            Assert.IsTrue(sync.Success, string.Join("\n", sync.Errors));
            CollectionAssert.AreEqual(
                new[] { "##var", "id", "name", "quality", "costs", "unlockWave" },
                rows[0]);
            CollectionAssert.AreEqual(
                new[] { "##type", "int", "string", "string", "list#sep=(,),int", "int?" },
                rows[1]);
        }

        [Test]
        public void SynchronizeTable_AddingFieldPreservesRowsAndUsesDefault()
        {
            LubanTableDescriptor before = GetDescriptor<WeaponConfig>();
            LubanTableSyncResult first = LubanWorkbookService.SynchronizeTable(
                before,
                _dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> rows = LubanWorkbookService.ReadWorkbook(first.FilePath);
            rows.Add(new List<string> { string.Empty, "1", "旧名称", "Rare", "1,2", "3" });
            LubanWorkbookService.WriteWorkbook(first.FilePath, before.TableName, rows, 4);

            LubanTableDescriptor after = GetDescriptor<ExpandedWeaponConfig>();
            LubanTableSyncResult second = LubanWorkbookService.SynchronizeTable(
                after,
                _dataDirectory,
                _backupDirectory,
                true);
            List<List<string>> updated = LubanWorkbookService.ReadWorkbook(second.FilePath);

            Assert.IsTrue(second.Success, string.Join("\n", second.Errors));
            CollectionAssert.Contains(second.AddedColumns, "damage");
            Assert.AreEqual("1", updated[4][1]);
            Assert.AreEqual("旧名称", updated[4][2]);
            Assert.AreEqual("10", updated[4][3]);
        }

        [Test]
        public void SynchronizeTable_FormerNameMigratesWithoutDeletionConfirmation()
        {
            LubanTableDescriptor before = GetDescriptor<BeforeRenameConfig>();
            LubanTableSyncResult first = LubanWorkbookService.SynchronizeTable(
                before,
                _dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> rows = LubanWorkbookService.ReadWorkbook(first.FilePath);
            rows.Add(new List<string> { string.Empty, "1", "迁移名称" });
            LubanWorkbookService.WriteWorkbook(first.FilePath, before.TableName, rows, 4);

            LubanTableDescriptor after = GetDescriptor<AfterRenameConfig>();
            LubanTableSyncResult second = LubanWorkbookService.SynchronizeTable(
                after,
                _dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> updated = LubanWorkbookService.ReadWorkbook(second.FilePath);

            Assert.IsTrue(second.Success, string.Join("\n", second.Errors));
            Assert.IsFalse(second.RequiresRemovalConfirmation);
            Assert.AreEqual("displayName", updated[0][2]);
            Assert.AreEqual("迁移名称", updated[4][2]);
        }

        [Test]
        public void SynchronizeTable_RemovingFieldRequiresConfirmationBeforeWrite()
        {
            LubanTableDescriptor before = GetDescriptor<BeforeRenameConfig>();
            LubanTableSyncResult first = LubanWorkbookService.SynchronizeTable(
                before,
                _dataDirectory,
                _backupDirectory,
                false);

            LubanTableDescriptor after = GetDescriptor<ValidationConfig>();
            after.TableName = before.TableName;
            after.FileName = before.FileName;
            LubanTableSyncResult second = LubanWorkbookService.SynchronizeTable(
                after,
                _dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> unchanged = LubanWorkbookService.ReadWorkbook(first.FilePath);

            Assert.IsFalse(second.Success);
            Assert.IsTrue(second.RequiresRemovalConfirmation);
            CollectionAssert.Contains(second.RemovedColumns, "name");
            Assert.AreEqual("name", unchanged[0][2]);
        }

        [Test]
        public void ValidateTable_ReportsRequiredEnumAndDuplicateKeyErrors()
        {
            LubanTableDescriptor table = GetDescriptor<ValidationConfig>();
            LubanTableSyncResult sync = LubanWorkbookService.SynchronizeTable(
                table,
                _dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> rows = LubanWorkbookService.ReadWorkbook(sync.FilePath);
            rows.Add(new List<string> { string.Empty, "1", "Unknown", string.Empty });
            rows.Add(new List<string> { string.Empty, "1", "Rare", "标题" });
            LubanWorkbookService.WriteWorkbook(sync.FilePath, table.TableName, rows, 4);

            List<string> errors = LubanWorkbookService.ValidateTable(
                table,
                sync.FilePath);
            string message = string.Join("\n", errors);

            StringAssert.Contains("枚举值无效", message);
            StringAssert.Contains("不能为空", message);
            StringAssert.Contains("主键重复", message);
        }

        [Test]
        public void WriteTableDefinitions_ProducesLubanRegistrationWorkbook()
        {
            LubanTableDescriptor table = GetDescriptor<WeaponConfig>();
            string path = Path.Combine(_dataDirectory, "__tables__.xlsx");

            LubanWorkbookService.WriteTableDefinitions(new[] { table }, path);
            List<List<string>> rows = LubanWorkbookService.ReadWorkbook(path);

            Assert.AreEqual("full_name", rows[0][1]);
            Assert.AreEqual("cfg.TbTestWeapon", rows[3][1]);
            Assert.AreEqual("WeaponConfig", rows[3][2]);
            Assert.AreEqual("#test_weapon.xlsx", rows[3][4]);
            Assert.AreEqual("id", rows[3][5]);
            Assert.AreEqual("tbtest_weapon", rows[3][10]);
        }

        [Test]
        public void ToolSettings_RejectDirectoriesOutsideUnityProject()
        {
            LubanToolSettings settings = LubanToolSettings.instance;
            string originalConfigRoot = settings.ConfigRoot;
            string originalOutput = settings.JsonOutputDirectory;
            try
            {
                settings.ConfigRoot = "../../OutsideConfig";
                settings.JsonOutputDirectory = "../OutsideJson";

                Assert.AreEqual("Config/Luban", settings.ConfigRoot);
                Assert.AreEqual(
                    "Assets/StreamingAssets/Config/Luban",
                    settings.JsonOutputDirectory);
            }
            finally
            {
                settings.ConfigRoot = originalConfigRoot;
                settings.JsonOutputDirectory = originalOutput;
            }
        }

        [Test]
        [Category("Integration")]
        public void InstalledLuban_GeneratesJsonReadableByJsonManager()
        {
            LubanEnvironmentStatus environment = LubanInstaller.GetStatus(
                LubanToolSettings.instance);
            if (!environment.IsReady)
            {
                Assert.Ignore("本机尚未安装 Luban 或 dotnet");
            }

            string configRoot = Path.Combine(_root, "LubanProject");
            string dataDirectory = Path.Combine(configRoot, "Datas");
            string definesDirectory = Path.Combine(configRoot, "Defines");
            string outputDirectory = Path.Combine(_root, "GeneratedJson");
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(definesDirectory);
            File.WriteAllText(
                Path.Combine(configRoot, "luban.conf"),
                CreateLubanConfig());
            File.WriteAllText(
                Path.Combine(definesDirectory, "builtin.xml"),
                "<module name=\"\"></module>");

            LubanTableDescriptor table = GetDescriptor<WeaponConfig>();
            LubanTableSyncResult sync = LubanWorkbookService.SynchronizeTable(
                table,
                dataDirectory,
                _backupDirectory,
                false);
            List<List<string>> rows = LubanWorkbookService.ReadWorkbook(sync.FilePath);
            rows.Add(new List<string>
            {
                string.Empty,
                "7",
                "测试长剑",
                "Rare",
                "10,20",
                "2"
            });
            LubanWorkbookService.WriteWorkbook(sync.FilePath, table.TableName, rows, 4);
            LubanWorkbookService.WriteTableDefinitions(
                new[] { table },
                Path.Combine(dataDirectory, "__tables__.xlsx"));
            Directory.CreateDirectory(outputDirectory);

            LubanProcessResult process = LubanProcessRunner.Run(
                environment.DotnetPath,
                new[]
                {
                    environment.LubanPath,
                    "-t",
                    "client",
                    "-d",
                    "json",
                    "--conf",
                    Path.Combine(configRoot, "luban.conf"),
                    "-x",
                    "outputDataDir=" + outputDirectory,
                    "--validationFailAsError"
                },
                configRoot,
                new Dictionary<string, string>
                {
                    { "DOTNET_ROLL_FORWARD", "Major" },
                    { "DOTNET_CLI_HOME", Path.Combine(_root, ".dotnet") }
                });

            Assert.IsTrue(
                process.Success,
                process.StandardError + "\n" + process.StandardOutput);
            string jsonPath = Path.Combine(outputDirectory, "tbtest_weapon.json");
            Assert.IsTrue(File.Exists(jsonPath));
            _jsonManager = JsonManager.Instance;
            bool parsed = _jsonManager.TryDeserializeData(
                File.ReadAllText(jsonPath, Encoding.UTF8),
                out List<WeaponConfig> values,
                out string error);
            Assert.IsTrue(parsed, error);
            Assert.AreEqual(1, values.Count);
            Assert.AreEqual("测试长剑", values[0].Name);
        }

        private static LubanTableDescriptor GetDescriptor<T>()
        {
            bool success = LubanTableScanner.TryCreateDescriptor(
                typeof(T),
                out LubanTableDescriptor table,
                out string error);
            Assert.IsTrue(success, error);
            return table;
        }

        private static LubanColumnDescriptor FindColumn(
            LubanTableDescriptor table,
            string name)
        {
            LubanColumnDescriptor column = FindColumnOrNull(table, name);
            Assert.NotNull(column, name);
            return column;
        }

        private static LubanColumnDescriptor FindColumnOrNull(
            LubanTableDescriptor table,
            string name)
        {
            return table.Columns.Find(column => column.Name == name);
        }

        private static string CreateLubanConfig()
        {
            return "{\n"
                   + "  \"groups\": [{ \"names\": [\"c\"], \"default\": true }],\n"
                   + "  \"schemaFiles\": [\n"
                   + "    { \"fileName\": \"Defines\", \"type\": \"\" },\n"
                   + "    { \"fileName\": \"Datas/__tables__.xlsx\", \"type\": \"table\" }\n"
                   + "  ],\n"
                   + "  \"dataDir\": \"Datas\",\n"
                   + "  \"targets\": [{ \"name\": \"client\", \"manager\": \"Tables\", \"groups\": [\"c\"], \"topModule\": \"cfg\" }],\n"
                   + "  \"output\": { \"cleanOutputDir\": false },\n"
                   + "  \"xargs\": []\n"
                   + "}\n";
        }
    }
}
