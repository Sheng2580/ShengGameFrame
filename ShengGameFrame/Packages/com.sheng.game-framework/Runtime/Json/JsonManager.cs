using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Sheng.GameFramework.Config;
using Sheng.GameFramework.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Sheng.GameFramework.Json
{
    /// <summary>
    /// 负责 JSON 数据序列化 文件保存和跨平台读取
    /// </summary>
    public sealed class JsonManager : PersistentMonoSingleton<JsonManager>
    {
        private sealed class ConfigColumnContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(
                MemberInfo member,
                MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                LubanColumnAttribute column =
                    member.GetCustomAttribute<LubanColumnAttribute>();
                if (!string.IsNullOrWhiteSpace(column?.Name))
                {
                    property.PropertyName = column.Name.Trim();
                }

                if (member.GetCustomAttribute<LubanIgnoreAttribute>() != null)
                {
                    property.Ignored = true;
                }

                return property;
            }
        }

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly JsonSerializerSettings CompactSettings =
            CreateSerializerSettings(Formatting.None);
        private static readonly JsonSerializerSettings PrettySettings =
            CreateSerializerSettings(Formatting.Indented);

        private readonly object _fileSync = new object();

        [SerializeField] private bool prettyPrint = true;
        [SerializeField] private bool keepBackup = true;
        [SerializeField] private bool enableDebugLogs;

        public bool PrettyPrint
        {
            get => prettyPrint;
            set => prettyPrint = value;
        }

        public bool KeepBackup
        {
            get => keepBackup;
            set => keepBackup = value;
        }

        public bool EnableDebugLogs
        {
            get => enableDebugLogs;
            set => enableDebugLogs = value;
        }

        public string SerializeData<T>(T data, bool formatted = false)
        {
            return JsonConvert.SerializeObject(
                data,
                formatted ? PrettySettings : CompactSettings);
        }

        public bool TryDeserializeData<T>(
            string json,
            out T data,
            out string errorMessage)
        {
            data = default;
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "JSON 内容为空";
                return false;
            }

            try
            {
                data = JsonConvert.DeserializeObject<T>(json, CompactSettings);
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return false;
            }
        }

        public bool SaveData<T>(
            T data,
            string fileName,
            string directory = "")
        {
            JsonWriteResult result = SaveDataDetailed(data, fileName, directory);
            if (!result.Success)
            {
                Debug.LogError($"[JsonManager] 保存失败 {result.ErrorMessage}");
            }

            return result.Success;
        }

        public JsonWriteResult SaveDataDetailed<T>(
            T data,
            string fileName,
            string directory = "")
        {
            if (!TryBuildPersistentPath(
                    fileName,
                    directory,
                    out string path,
                    out string pathError))
            {
                return JsonWriteResult.Failed(
                    string.Empty,
                    JsonErrorCode.InvalidPath,
                    pathError);
            }

            string json;
            try
            {
                json = JsonConvert.SerializeObject(
                    data,
                    prettyPrint ? PrettySettings : CompactSettings);
            }
            catch (Exception exception)
            {
                return JsonWriteResult.Failed(
                    path,
                    JsonErrorCode.SerializeFailed,
                    exception.Message);
            }

            lock (_fileSync)
            {
                string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    string parentDirectory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(parentDirectory))
                    {
                        Directory.CreateDirectory(parentDirectory);
                    }

                    File.WriteAllText(temporaryPath, json, Utf8WithoutBom);
                    ReplaceFile(temporaryPath, path, keepBackup);
                    Log($"保存 {path}");
                    return JsonWriteResult.Succeeded(path);
                }
                catch (Exception exception)
                {
                    return JsonWriteResult.Failed(
                        path,
                        JsonErrorCode.WriteFailed,
                        exception.Message);
                }
                finally
                {
                    TryDeleteFile(temporaryPath);
                }
            }
        }

        public Task<JsonWriteResult> SaveDataAsync<T>(
            T data,
            string fileName,
            string directory = "",
            CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return SaveDataDetailed(data, fileName, directory);
                },
                cancellationToken);
        }

        public T LoadData<T>(string fileName, string directory = "")
        {
            return TryLoadData(fileName, out T data, directory)
                ? data
                : default;
        }

        public bool TryLoadData<T>(
            string fileName,
            out T data,
            string directory = "")
        {
            JsonReadResult<T> result = LoadDataDetailed<T>(fileName, directory);
            data = result.Success ? result.Value : default;
            return result.Success;
        }

        public JsonReadResult<T> LoadDataDetailed<T>(
            string fileName,
            string directory = "")
        {
            if (!TryBuildPersistentPath(
                    fileName,
                    directory,
                    out string path,
                    out string pathError))
            {
                return JsonReadResult<T>.Failed(
                    string.Empty,
                    JsonErrorCode.InvalidPath,
                    pathError);
            }

            return LoadLocalFile<T>(path);
        }

        public Task<JsonReadResult<T>> LoadDataAsync<T>(
            string fileName,
            string directory = "",
            CancellationToken cancellationToken = default)
        {
            return Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return LoadDataDetailed<T>(fileName, directory);
                },
                cancellationToken);
        }

        public async Task<JsonReadResult<T>> LoadStreamingDataAsync<T>(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            JsonReadResult<string> textResult = await ReadTextAsync(
                JsonDataLocation.StreamingAssets,
                relativePath,
                cancellationToken);
            if (!textResult.Success)
            {
                return JsonReadResult<T>.Failed(
                    textResult.Path,
                    textResult.ErrorCode,
                    textResult.ErrorMessage);
            }

            if (!TryDeserializeData(
                    textResult.Value,
                    out T data,
                    out string errorMessage))
            {
                return JsonReadResult<T>.Failed(
                    textResult.Path,
                    JsonErrorCode.DeserializeFailed,
                    errorMessage);
            }

            return JsonReadResult<T>.Succeeded(data, textResult.Path);
        }

        public Task<JsonReadResult<List<T>>> LoadLubanTableAsync<T>(
            string tableName,
            string relativeDirectory = "Config/Luban",
            CancellationToken cancellationToken = default)
        {
            string relativePath = Path.Combine(relativeDirectory, tableName);
            return LoadStreamingDataAsync<List<T>>(relativePath, cancellationToken);
        }

        public async Task<JsonReadResult<string>> ReadTextAsync(
            JsonDataLocation location,
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (!TryBuildReadPath(
                    location,
                    relativePath,
                    out string path,
                    out string pathError))
            {
                return JsonReadResult<string>.Failed(
                    string.Empty,
                    JsonErrorCode.InvalidPath,
                    pathError);
            }

            try
            {
                if (!RequiresUnityWebRequest(path))
                {
                    return await Task.Run(
                        () => LoadLocalText(path),
                        cancellationToken);
                }

                using (UnityWebRequest request = UnityWebRequest.Get(path))
                {
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        JsonErrorCode errorCode = request.responseCode == 404
                            ? JsonErrorCode.NotFound
                            : JsonErrorCode.ReadFailed;
                        return JsonReadResult<string>.Failed(
                            path,
                            errorCode,
                            request.error);
                    }

                    return JsonReadResult<string>.Succeeded(
                        request.downloadHandler.text,
                        path);
                }
            }
            catch (OperationCanceledException)
            {
                return JsonReadResult<string>.Failed(
                    path,
                    JsonErrorCode.Cancelled,
                    "读取已取消");
            }
            catch (Exception exception)
            {
                return JsonReadResult<string>.Failed(
                    path,
                    JsonErrorCode.ReadFailed,
                    exception.Message);
            }
        }

        public bool Exists(string fileName, string directory = "")
        {
            return TryBuildPersistentPath(
                       fileName,
                       directory,
                       out string path,
                       out _)
                   && File.Exists(path);
        }

        public bool DeleteData(string fileName, string directory = "")
        {
            if (!TryBuildPersistentPath(
                    fileName,
                    directory,
                    out string path,
                    out _))
            {
                return false;
            }

            lock (_fileSync)
            {
                try
                {
                    TryDeleteFile(path);
                    TryDeleteFile(path + ".bak");
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[JsonManager] 删除失败 {exception.Message}");
                    return false;
                }
            }
        }

        public IReadOnlyList<string> GetAllDataNames(string directory = "")
        {
            if (!TryBuildPersistentDirectory(
                    directory,
                    out string directoryPath,
                    out _)
                || !Directory.Exists(directoryPath))
            {
                return Array.Empty<string>();
            }

            string[] files = Directory.GetFiles(
                directoryPath,
                "*.json",
                SearchOption.TopDirectoryOnly);
            List<string> names = new List<string>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                names.Add(Path.GetFileNameWithoutExtension(files[i]));
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private JsonReadResult<T> LoadLocalFile<T>(string path)
        {
            JsonReadResult<string> textResult;
            lock (_fileSync)
            {
                textResult = LoadLocalText(path);
            }

            if (!textResult.Success)
            {
                return JsonReadResult<T>.Failed(
                    textResult.Path,
                    textResult.ErrorCode,
                    textResult.ErrorMessage);
            }

            if (!TryDeserializeData(
                    textResult.Value,
                    out T data,
                    out string errorMessage))
            {
                return JsonReadResult<T>.Failed(
                    path,
                    JsonErrorCode.DeserializeFailed,
                    errorMessage);
            }

            Log($"读取 {path}");
            return JsonReadResult<T>.Succeeded(data, path);
        }

        private static JsonReadResult<string> LoadLocalText(string path)
        {
            if (!File.Exists(path))
            {
                return JsonReadResult<string>.Failed(
                    path,
                    JsonErrorCode.NotFound,
                    "JSON 文件不存在");
            }

            try
            {
                return JsonReadResult<string>.Succeeded(
                    File.ReadAllText(path, Utf8WithoutBom),
                    path);
            }
            catch (Exception exception)
            {
                return JsonReadResult<string>.Failed(
                    path,
                    JsonErrorCode.ReadFailed,
                    exception.Message);
            }
        }

        private static JsonSerializerSettings CreateSerializerSettings(
            Formatting formatting)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                ContractResolver = new ConfigColumnContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                Formatting = formatting,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        private static void ReplaceFile(
            string temporaryPath,
            string destinationPath,
            bool preserveBackup)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }

            string backupPath = preserveBackup
                ? destinationPath + ".bak"
                : null;
            try
            {
                if (!string.IsNullOrEmpty(backupPath))
                {
                    TryDeleteFile(backupPath);
                }

                File.Replace(temporaryPath, destinationPath, backupPath);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceFileFallback(
                    temporaryPath,
                    destinationPath,
                    backupPath);
            }
            catch (IOException)
            {
                ReplaceFileFallback(
                    temporaryPath,
                    destinationPath,
                    backupPath);
            }
        }

        private static void ReplaceFileFallback(
            string temporaryPath,
            string destinationPath,
            string backupPath)
        {
            if (!string.IsNullOrEmpty(backupPath))
            {
                File.Copy(destinationPath, backupPath, true);
            }

            File.Delete(destinationPath);
            File.Move(temporaryPath, destinationPath);
        }

        private static bool TryBuildPersistentPath(
            string fileName,
            string directory,
            out string path,
            out string error)
        {
            path = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                error = "文件名不能为空";
                return false;
            }

            string relativeFile = EnsureJsonExtension(fileName.Trim());
            string relativePath = string.IsNullOrWhiteSpace(directory)
                ? relativeFile
                : Path.Combine(directory.Trim(), relativeFile);
            return TryCombineUnderRoot(
                Application.persistentDataPath,
                relativePath,
                out path,
                out error);
        }

        private static bool TryBuildPersistentDirectory(
            string directory,
            out string path,
            out string error)
        {
            string relativePath = string.IsNullOrWhiteSpace(directory)
                ? "."
                : directory.Trim();
            return TryCombineUnderRoot(
                Application.persistentDataPath,
                relativePath,
                out path,
                out error);
        }

        private static bool TryBuildReadPath(
            JsonDataLocation location,
            string relativePath,
            out string path,
            out string error)
        {
            string root = location == JsonDataLocation.PersistentData
                ? Application.persistentDataPath
                : Application.streamingAssetsPath;
            return TryCombineUnderRoot(
                root,
                EnsureJsonExtension(relativePath?.Trim()),
                out path,
                out error);
        }

        private static bool TryCombineUnderRoot(
            string root,
            string relativePath,
            out string path,
            out string error)
        {
            path = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath))
            {
                error = "只允许使用相对路径";
                return false;
            }

            if (root.Contains("://"))
            {
                string normalizedRelativePath = relativePath.Replace('\\', '/');
                string[] segments = normalizedRelativePath.Split('/');
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] == "..")
                    {
                        error = "路径不能超出数据根目录";
                        return false;
                    }
                }

                path = root.TrimEnd('/') + "/" + normalizedRelativePath.TrimStart('/');
                return true;
            }

            try
            {
                string fullRoot = Path.GetFullPath(root);
                string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
                StringComparison comparison = Application.platform
                    == RuntimePlatform.WindowsEditor
                    || Application.platform == RuntimePlatform.WindowsPlayer
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal;
                string rootPrefix = fullRoot.TrimEnd(
                                        Path.DirectorySeparatorChar,
                                        Path.AltDirectorySeparatorChar)
                                    + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootPrefix, comparison)
                    && !string.Equals(fullPath, fullRoot, comparison))
                {
                    error = "路径不能超出数据根目录";
                    return false;
                }

                path = fullPath;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string EnsureJsonExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? path
                : path + ".json";
        }

        private static bool RequiresUnityWebRequest(string path)
        {
            return path.Contains("://")
                   || Application.platform == RuntimePlatform.Android
                   || Application.platform == RuntimePlatform.WebGLPlayer;
        }

        private static void TryDeleteFile(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[JsonManager] {message}");
            }
        }
    }
}
