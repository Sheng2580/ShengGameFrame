using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// Luban 工具项目设置
    /// </summary>
    [FilePath(
        "ProjectSettings/ShengGameFrameworkLubanSettings.asset",
        FilePathAttribute.Location.ProjectFolder)]
    public sealed class LubanToolSettings : ScriptableSingleton<LubanToolSettings>
    {
        [SerializeField] private string configRoot = "Config/Luban";
        [SerializeField] private string jsonOutputDirectory =
            "Assets/StreamingAssets/Config/Luban";
        [SerializeField] private string target = "client";
        [SerializeField] private string dotnetPathOverride = string.Empty;
        [SerializeField] private bool validationFailAsError = true;

        public string ConfigRoot
        {
            get => NormalizeRelativePath(configRoot, "Config/Luban");
            set => configRoot = NormalizeRelativePath(value, "Config/Luban");
        }

        public string JsonOutputDirectory
        {
            get => NormalizeRelativePath(
                jsonOutputDirectory,
                "Assets/StreamingAssets/Config/Luban");
            set => jsonOutputDirectory = NormalizeRelativePath(
                value,
                "Assets/StreamingAssets/Config/Luban");
        }

        public string Target
        {
            get => string.IsNullOrWhiteSpace(target) ? "client" : target.Trim();
            set => target = string.IsNullOrWhiteSpace(value) ? "client" : value.Trim();
        }

        public string DotnetPathOverride
        {
            get => dotnetPathOverride;
            set => dotnetPathOverride = value?.Trim() ?? string.Empty;
        }

        public bool ValidationFailAsError
        {
            get => validationFailAsError;
            set => validationFailAsError = value;
        }

        public string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                     ?? Application.dataPath;
        public string ConfigRootPath => Path.GetFullPath(
            Path.Combine(ProjectRoot, ConfigRoot));
        public string DataDirectoryPath => Path.Combine(ConfigRootPath, "Datas");
        public string DefinesDirectoryPath => Path.Combine(ConfigRootPath, "Defines");
        public string ConfigFilePath => Path.Combine(ConfigRootPath, "luban.conf");
        public string TableDefinitionsPath => Path.Combine(
            DataDirectoryPath,
            "__tables__.xlsx");
        public string JsonOutputPath => Path.GetFullPath(
            Path.Combine(ProjectRoot, JsonOutputDirectory));
        public string LibraryRootPath => Path.Combine(
            ProjectRoot,
            "Library",
            "ShengGameFramework");
        public string LubanInstallPath => Path.Combine(
            LibraryRootPath,
            "Tools",
            "Luban",
            LubanInstaller.Version,
            "Luban");
        public string LubanDllPath => Path.Combine(LubanInstallPath, "Luban.dll");
        public string TemporaryOutputPath => Path.Combine(
            LibraryRootPath,
            "LubanOutput");
        public string BackupRootPath => Path.Combine(
            LibraryRootPath,
            "LubanBackups");

        public void SaveSettings()
        {
            Save(true);
        }

        private static string NormalizeRelativePath(
            string path,
            string fallback)
        {
            string value = string.IsNullOrWhiteSpace(path)
                ? fallback
                : path.Trim();
            value = value.Replace('\\', '/');
            if (Path.IsPathRooted(value))
            {
                return fallback;
            }

            string[] segments = value.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "..")
                {
                    return fallback;
                }
            }

            return value.TrimStart('/');
        }
    }
}
