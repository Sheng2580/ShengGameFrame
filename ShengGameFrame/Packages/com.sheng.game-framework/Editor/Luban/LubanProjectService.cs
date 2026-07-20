using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// Luban 项目操作结果
    /// </summary>
    public sealed class LubanOperationResult
    {
        public bool Success;
        public bool RequiresRemovalConfirmation;
        public string Message;
        public string Command;
        public string StandardOutput;
        public string StandardError;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> GeneratedFiles = new List<string>();
    }

    [Serializable]
    internal sealed class LubanGenerationManifest
    {
        public string configVersion = "1";
        public string lubanVersion;
        public string schemaHash;
        public string generatedAtUtc;
        public List<LubanGeneratedFile> files = new List<LubanGeneratedFile>();
    }

    [Serializable]
    internal sealed class LubanGeneratedFile
    {
        public string name;
        public long size;
        public string sha256;
    }

    /// <summary>
    /// 初始化 同步 校验和生成 Luban JSON
    /// </summary>
    public static class LubanProjectService
    {
        private const string LubanConfigTemplate =
            "{\n"
            + "  \"groups\": [\n"
            + "    { \"names\": [\"c\"], \"default\": true }\n"
            + "  ],\n"
            + "  \"schemaFiles\": [\n"
            + "    { \"fileName\": \"Defines\", \"type\": \"\" },\n"
            + "    { \"fileName\": \"Datas/__tables__.xlsx\", \"type\": \"table\" }\n"
            + "  ],\n"
            + "  \"dataDir\": \"Datas\",\n"
            + "  \"targets\": [\n"
            + "    { \"name\": \"client\", \"manager\": \"Tables\", \"groups\": [\"c\"], \"topModule\": \"cfg\" }\n"
            + "  ],\n"
            + "  \"output\": { \"cleanOutputDir\": false },\n"
            + "  \"xargs\": []\n"
            + "}\n";

        private const string BuiltinTypesTemplate =
            "<module name=\"\">\n"
            + "    <bean name=\"vector2\" valueType=\"1\" sep=\",\">\n"
            + "        <var name=\"x\" type=\"float\"/>\n"
            + "        <var name=\"y\" type=\"float\"/>\n"
            + "    </bean>\n"
            + "    <bean name=\"vector3\" valueType=\"1\" sep=\",\">\n"
            + "        <var name=\"x\" type=\"float\"/>\n"
            + "        <var name=\"y\" type=\"float\"/>\n"
            + "        <var name=\"z\" type=\"float\"/>\n"
            + "    </bean>\n"
            + "</module>\n";

        public static LubanOperationResult InitializeProject()
        {
            LubanToolSettings settings = LubanToolSettings.instance;
            LubanScanResult scan = LubanTableScanner.ScanProject();
            LubanOperationResult result = new LubanOperationResult();
            if (!scan.Success)
            {
                result.Errors.AddRange(scan.Errors);
                result.Message = "C# 配置类扫描失败";
                return result;
            }

            try
            {
                Directory.CreateDirectory(settings.ConfigRootPath);
                Directory.CreateDirectory(settings.DataDirectoryPath);
                Directory.CreateDirectory(settings.DefinesDirectoryPath);
                Directory.CreateDirectory(settings.JsonOutputPath);

                if (!File.Exists(settings.ConfigFilePath))
                {
                    File.WriteAllText(
                        settings.ConfigFilePath,
                        LubanConfigTemplate,
                        new UTF8Encoding(false));
                }

                string builtinPath = Path.Combine(
                    settings.DefinesDirectoryPath,
                    "builtin.xml");
                if (!File.Exists(builtinPath))
                {
                    File.WriteAllText(
                        builtinPath,
                        BuiltinTypesTemplate,
                        new UTF8Encoding(false));
                }

                LubanWorkbookService.WriteTableDefinitions(
                    scan.Tables,
                    settings.TableDefinitionsPath);
                result.Success = true;
                result.Message = $"Luban 项目已初始化 发现 {scan.Tables.Count} 张配置表";
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                result.Message = "Luban 项目初始化失败";
            }

            return result;
        }

        public static LubanOperationResult SynchronizeAllTables(
            bool removeObsoleteColumns)
        {
            LubanOperationResult initializeResult = InitializeProject();
            if (!initializeResult.Success)
            {
                return initializeResult;
            }

            LubanToolSettings settings = LubanToolSettings.instance;
            LubanScanResult scan = LubanTableScanner.ScanProject();
            LubanOperationResult result = new LubanOperationResult();
            for (int i = 0; i < scan.Tables.Count; i++)
            {
                LubanTableSyncResult analysis = LubanWorkbookService.AnalyzeTable(
                    scan.Tables[i],
                    settings.DataDirectoryPath);
                if (analysis.Errors.Count > 0)
                {
                    result.Errors.AddRange(analysis.Errors);
                }

                if (analysis.RemovedColumns.Count > 0 && !removeObsoleteColumns)
                {
                    result.RequiresRemovalConfirmation = true;
                    result.Errors.Add(
                        $"{analysis.TableName} 将删除字段 {string.Join(", ", analysis.RemovedColumns)}");
                }
            }

            if (result.RequiresRemovalConfirmation || result.Errors.Count > 0)
            {
                result.Message = result.RequiresRemovalConfirmation
                    ? "同步包含删除字段 请确认后继续"
                    : "配置表预检查失败";
                return result;
            }

            for (int i = 0; i < scan.Tables.Count; i++)
            {
                LubanTableSyncResult syncResult =
                    LubanWorkbookService.SynchronizeTable(
                        scan.Tables[i],
                        settings.DataDirectoryPath,
                        settings.BackupRootPath,
                        removeObsoleteColumns);
                if (!syncResult.Success)
                {
                    result.Errors.AddRange(syncResult.Errors);
                }
            }

            if (result.Errors.Count > 0)
            {
                result.Message = "配置表同步失败";
                return result;
            }

            LubanWorkbookService.WriteTableDefinitions(
                scan.Tables,
                settings.TableDefinitionsPath);
            result.Success = true;
            result.Message = $"已同步 {scan.Tables.Count} 张配置表";
            return result;
        }

        public static LubanOperationResult ValidateProjectStructure()
        {
            LubanToolSettings settings = LubanToolSettings.instance;
            LubanScanResult scan = LubanTableScanner.ScanProject();
            LubanOperationResult result = new LubanOperationResult();
            if (!scan.Success)
            {
                result.Errors.AddRange(scan.Errors);
            }

            if (!File.Exists(settings.ConfigFilePath))
            {
                result.Errors.Add("缺少 luban.conf 请先初始化 Luban 项目");
            }

            for (int i = 0; i < scan.Tables.Count; i++)
            {
                LubanTableDescriptor table = scan.Tables[i];
                string tablePath = Path.Combine(
                    settings.DataDirectoryPath,
                    table.FileName);
                result.Errors.AddRange(
                    LubanWorkbookService.ValidateTable(table, tablePath));
            }

            if (scan.Tables.Count == 0)
            {
                result.Errors.Add("没有找到带 LubanTable 特性的配置类");
            }

            result.Success = result.Errors.Count == 0;
            result.Message = result.Success
                ? $"结构校验通过 共 {scan.Tables.Count} 张表"
                : "结构校验失败";
            return result;
        }

        public static Task<LubanOperationResult> ValidateWithLubanAsync()
        {
            return GenerateJsonInternalAsync(false);
        }

        public static Task<LubanOperationResult> GenerateJsonAsync()
        {
            return GenerateJsonInternalAsync(true);
        }

        private static async Task<LubanOperationResult> GenerateJsonInternalAsync(
            bool publish)
        {
            LubanOperationResult validation = ValidateProjectStructure();
            if (!validation.Success)
            {
                return validation;
            }

            LubanToolSettings settings = LubanToolSettings.instance;
            LubanEnvironmentStatus environment = LubanInstaller.GetStatus(settings);
            if (!environment.IsReady)
            {
                return new LubanOperationResult
                {
                    Success = false,
                    Message = environment.ErrorMessage
                };
            }

            LubanScanResult scan = LubanTableScanner.ScanProject();
            string temporaryOutput = settings.TemporaryOutputPath;
            string configRoot = settings.ConfigRootPath;
            string configFile = settings.ConfigFilePath;
            string lubanDll = settings.LubanDllPath;
            string target = settings.Target;
            string dotnetPath = environment.DotnetPath;
            bool validationFailAsError = settings.ValidationFailAsError;
            string outputPath = settings.JsonOutputPath;
            string schemaHash = ComputeSchemaHash(scan.Tables);

            LubanOperationResult result = await Task.Run(() =>
                GenerateJsonCore(
                    dotnetPath,
                    lubanDll,
                    target,
                    configFile,
                    configRoot,
                    temporaryOutput,
                    outputPath,
                    schemaHash,
                    validationFailAsError,
                    publish));
            if (result.Success && publish)
            {
                AssetDatabase.Refresh();
            }

            return result;
        }

        private static LubanOperationResult GenerateJsonCore(
            string dotnetPath,
            string lubanDll,
            string target,
            string configFile,
            string configRoot,
            string temporaryOutput,
            string outputPath,
            string schemaHash,
            bool validationFailAsError,
            bool publish)
        {
            LubanOperationResult result = new LubanOperationResult();
            try
            {
                if (Directory.Exists(temporaryOutput))
                {
                    Directory.Delete(temporaryOutput, true);
                }

                Directory.CreateDirectory(temporaryOutput);
                List<string> arguments = new List<string>
                {
                    lubanDll,
                    "-t",
                    target,
                    "-d",
                    "json",
                    "--conf",
                    configFile,
                    "-x",
                    "outputDataDir=" + temporaryOutput
                };
                if (validationFailAsError)
                {
                    arguments.Add("--validationFailAsError");
                }

                Dictionary<string, string> environment = new Dictionary<string, string>
                {
                    { "DOTNET_ROLL_FORWARD", "Major" },
                    {
                        "DOTNET_CLI_HOME",
                        Path.Combine(temporaryOutput, ".dotnet")
                    }
                };
                LubanProcessResult processResult = LubanProcessRunner.Run(
                    dotnetPath,
                    arguments,
                    configRoot,
                    environment);
                result.Command = processResult.Command;
                result.StandardOutput = processResult.StandardOutput;
                result.StandardError = processResult.StandardError;
                if (!processResult.Success)
                {
                    result.Message = "Luban 执行失败";
                    result.Errors.Add(string.IsNullOrWhiteSpace(processResult.StandardError)
                        ? processResult.StandardOutput
                        : processResult.StandardError);
                    return result;
                }

                string[] jsonFiles = Directory.GetFiles(
                    temporaryOutput,
                    "*.json",
                    SearchOption.TopDirectoryOnly);
                if (jsonFiles.Length == 0)
                {
                    result.Message = "Luban 没有生成 JSON 文件";
                    return result;
                }

                Array.Sort(jsonFiles, StringComparer.Ordinal);
                for (int i = 0; i < jsonFiles.Length; i++)
                {
                    string json = File.ReadAllText(jsonFiles[i], Encoding.UTF8);
                    JToken token = JToken.Parse(json);
                    if (token.Type != JTokenType.Array)
                    {
                        result.Errors.Add(
                            $"{Path.GetFileName(jsonFiles[i])} 顶层必须是数组");
                    }
                }

                if (result.Errors.Count > 0)
                {
                    result.Message = "生成结果校验失败";
                    return result;
                }

                if (publish)
                {
                    PublishJsonFiles(
                        jsonFiles,
                        outputPath,
                        schemaHash);
                }

                for (int i = 0; i < jsonFiles.Length; i++)
                {
                    result.GeneratedFiles.Add(Path.GetFileName(jsonFiles[i]));
                }

                result.Success = true;
                result.Message = publish
                    ? $"已生成 {jsonFiles.Length} 个 JSON 文件"
                    : $"Luban 校验通过 共 {jsonFiles.Length} 个 JSON 文件";
                return result;
            }
            catch (Exception exception)
            {
                result.Message = "Luban 生成失败";
                result.Errors.Add(exception.Message);
                return result;
            }
        }

        private static void PublishJsonFiles(
            IReadOnlyList<string> sourceFiles,
            string outputPath,
            string schemaHash)
        {
            Directory.CreateDirectory(outputPath);
            string[] oldFiles = Directory.GetFiles(
                outputPath,
                "*.json",
                SearchOption.TopDirectoryOnly);
            for (int i = 0; i < oldFiles.Length; i++)
            {
                File.Delete(oldFiles[i]);
            }

            LubanGenerationManifest manifest = new LubanGenerationManifest
            {
                lubanVersion = LubanInstaller.Version,
                schemaHash = schemaHash,
                generatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                string fileName = Path.GetFileName(sourceFiles[i]);
                string destination = Path.Combine(outputPath, fileName);
                File.Copy(sourceFiles[i], destination, true);
                FileInfo fileInfo = new FileInfo(destination);
                manifest.files.Add(new LubanGeneratedFile
                {
                    name = fileName,
                    size = fileInfo.Length,
                    sha256 = ComputeFileSha256(destination)
                });
            }

            string manifestJson = JsonConvert.SerializeObject(
                manifest,
                Formatting.Indented);
            File.WriteAllText(
                Path.Combine(outputPath, "luban_manifest.json"),
                manifestJson,
                new UTF8Encoding(false));
        }

        private static string ComputeSchemaHash(
            IReadOnlyList<LubanTableDescriptor> tables)
        {
            StringBuilder builder = new StringBuilder();
            for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                LubanTableDescriptor table = tables[tableIndex];
                builder.Append(table.ModelType.FullName)
                    .Append('|')
                    .Append(table.TableName)
                    .Append('|')
                    .Append(table.OutputName)
                    .AppendLine();
                for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
                {
                    LubanColumnDescriptor column = table.Columns[columnIndex];
                    builder.Append(column.Name)
                        .Append('|')
                        .Append(column.LubanType)
                        .Append('|')
                        .Append(column.Required)
                        .Append('|')
                        .Append(column.IsKey)
                        .Append('|')
                        .Append(column.DefaultValue)
                        .AppendLine();
                }
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
