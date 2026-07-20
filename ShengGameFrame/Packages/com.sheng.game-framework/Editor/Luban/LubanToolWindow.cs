using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Sheng.GameFramework.Editor.Luban
{
    /// <summary>
    /// C# 配置类到 Luban JSON 的编辑器工具
    /// </summary>
    public sealed class LubanToolWindow : EditorWindow
    {
        private readonly List<string> _logs = new List<string>();
        private readonly object _logSync = new object();

        private LubanScanResult _scanResult;
        private LubanEnvironmentStatus _environmentStatus;
        private Vector2 _tableScroll;
        private Vector2 _logScroll;
        private bool _isBusy;

        [MenuItem("Sheng Game Framework/Data/Luban 配置工具")]
        public static void Open()
        {
            LubanToolWindow window = GetWindow<LubanToolWindow>();
            window.titleContent = new GUIContent("Luban 配置工具");
            window.minSize = new Vector2(680f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshState();
        }

        private void OnGUI()
        {
            DrawEnvironment();
            EditorGUILayout.Space(6f);
            DrawSettings();
            EditorGUILayout.Space(6f);
            DrawActions();
            EditorGUILayout.Space(6f);
            DrawTables();
            EditorGUILayout.Space(6f);
            DrawLogs();
        }

        private void OnInspectorUpdate()
        {
            if (_isBusy)
            {
                Repaint();
            }
        }

        private void DrawEnvironment()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("环境状态", EditorStyles.boldLabel);
                DrawStatusLine(
                    "dotnet",
                    !string.IsNullOrEmpty(_environmentStatus?.DotnetPath),
                    _environmentStatus?.DotnetPath ?? "未找到");
                DrawStatusLine(
                    "Luban",
                    _environmentStatus?.IsLubanInstalled == true,
                    _environmentStatus?.IsLubanInstalled == true
                        ? $"{LubanInstaller.Version}  {_environmentStatus.LubanPath}"
                        : "未安装");
                DrawStatusLine(
                    "配置类",
                    _scanResult?.Success == true,
                    _scanResult == null
                        ? "尚未扫描"
                        : $"{_scanResult.Tables.Count} 张表  {_scanResult.Errors.Count} 个错误");
            }
        }

        private void DrawSettings()
        {
            LubanToolSettings settings = LubanToolSettings.instance;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("项目设置", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                string configRoot = EditorGUILayout.TextField(
                    "Luban 配置目录",
                    settings.ConfigRoot);
                string jsonOutput = EditorGUILayout.TextField(
                    "JSON 输出目录",
                    settings.JsonOutputDirectory);
                string target = EditorGUILayout.TextField(
                    "生成目标",
                    settings.Target);
                string dotnetOverride = EditorGUILayout.TextField(
                    "dotnet 自定义路径",
                    settings.DotnetPathOverride);
                bool validationAsError = EditorGUILayout.Toggle(
                    "校验失败视为错误",
                    settings.ValidationFailAsError);
                if (EditorGUI.EndChangeCheck())
                {
                    settings.ConfigRoot = configRoot;
                    settings.JsonOutputDirectory = jsonOutput;
                    settings.Target = target;
                    settings.DotnetPathOverride = dotnetOverride;
                    settings.ValidationFailAsError = validationAsError;
                    settings.SaveSettings();
                    RefreshState();
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(_isBusy);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("安装 Luban", GUILayout.Height(28f)))
                    {
                        InstallLuban();
                    }

                    if (GUILayout.Button("初始化配置目录", GUILayout.Height(28f)))
                    {
                        LogResult(LubanProjectService.InitializeProject());
                        RefreshState();
                    }

                    if (GUILayout.Button("扫描 C# 配置类", GUILayout.Height(28f)))
                    {
                        RefreshState();
                        AppendLog(
                            $"扫描完成 {_scanResult.Tables.Count} 张表 {_scanResult.Errors.Count} 个错误");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("从 C# 创建或同步表", GUILayout.Height(32f)))
                    {
                        SynchronizeTables();
                    }

                    if (GUILayout.Button("校验 Luban 表", GUILayout.Height(32f)))
                    {
                        RunOperationAsync(LubanProjectService.ValidateWithLubanAsync);
                    }

                    if (GUILayout.Button("Luban 表转 JSON", GUILayout.Height(32f)))
                    {
                        RunOperationAsync(LubanProjectService.GenerateJsonAsync);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开表文件夹"))
                    {
                        RevealDirectory(LubanToolSettings.instance.DataDirectoryPath);
                    }

                    if (GUILayout.Button("打开 JSON 文件夹"))
                    {
                        RevealDirectory(LubanToolSettings.instance.JsonOutputPath);
                    }

                    if (GUILayout.Button("清空日志"))
                    {
                        lock (_logSync)
                        {
                            _logs.Clear();
                        }
                    }
                }

                EditorGUI.EndDisabledGroup();
                if (_isBusy)
                {
                    EditorGUILayout.HelpBox("任务执行中", MessageType.Info);
                }
            }
        }

        private void DrawTables()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("配置表", EditorStyles.boldLabel);
                _tableScroll = EditorGUILayout.BeginScrollView(
                    _tableScroll,
                    GUILayout.MinHeight(120f),
                    GUILayout.MaxHeight(220f));
                if (_scanResult == null || _scanResult.Tables.Count == 0)
                {
                    EditorGUILayout.LabelField("没有找到带 LubanTable 特性的配置类");
                }
                else
                {
                    string dataDirectory = LubanToolSettings.instance.DataDirectoryPath;
                    for (int i = 0; i < _scanResult.Tables.Count; i++)
                    {
                        LubanTableDescriptor table = _scanResult.Tables[i];
                        string filePath = Path.Combine(dataDirectory, table.FileName);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                table.TableName,
                                GUILayout.Width(150f));
                            EditorGUILayout.LabelField(
                                table.ModelType.FullName,
                                GUILayout.MinWidth(260f));
                            GUILayout.Label(
                                File.Exists(filePath) ? "已创建" : "未创建",
                                File.Exists(filePath)
                                    ? EditorStyles.miniBoldLabel
                                    : EditorStyles.miniLabel,
                                GUILayout.Width(60f));
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
                if (_scanResult != null)
                {
                    for (int i = 0; i < _scanResult.Errors.Count; i++)
                    {
                        EditorGUILayout.HelpBox(
                            _scanResult.Errors[i],
                            MessageType.Error);
                    }
                }
            }
        }

        private void DrawLogs()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("执行日志", EditorStyles.boldLabel);
                _logScroll = EditorGUILayout.BeginScrollView(
                    _logScroll,
                    GUILayout.MinHeight(120f));
                string[] logs;
                lock (_logSync)
                {
                    logs = _logs.ToArray();
                }

                for (int i = 0; i < logs.Length; i++)
                {
                    EditorGUILayout.SelectableLabel(
                        logs[i],
                        EditorStyles.wordWrappedLabel,
                        GUILayout.MinHeight(18f));
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private async void InstallLuban()
        {
            _isBusy = true;
            Repaint();
            try
            {
                LubanInstallResult result = await LubanInstaller.InstallAsync(
                    LubanToolSettings.instance,
                    AppendLog);
                AppendLog(result.Message);
                if (!result.Success)
                {
                    Debug.LogError($"[Luban] {result.Message}");
                }
            }
            catch (Exception exception)
            {
                AppendLog(exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _isBusy = false;
                RefreshState();
                Repaint();
            }
        }

        private void SynchronizeTables()
        {
            LubanOperationResult result =
                LubanProjectService.SynchronizeAllTables(false);
            if (result.RequiresRemovalConfirmation)
            {
                string details = string.Join("\n", result.Errors);
                bool confirmed = EditorUtility.DisplayDialog(
                    "确认删除配置列",
                    details + "\n\n删除前会自动备份 Excel",
                    "删除并同步",
                    "取消");
                if (confirmed)
                {
                    result = LubanProjectService.SynchronizeAllTables(true);
                }
            }

            LogResult(result);
            RefreshState();
        }

        private async void RunOperationAsync(
            Func<Task<LubanOperationResult>> operation)
        {
            _isBusy = true;
            Repaint();
            try
            {
                LubanOperationResult result = await operation.Invoke();
                LogResult(result);
            }
            catch (Exception exception)
            {
                AppendLog(exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _isBusy = false;
                RefreshState();
                Repaint();
            }
        }

        private void LogResult(LubanOperationResult result)
        {
            if (result == null)
            {
                return;
            }

            AppendLog(result.Message);
            if (!string.IsNullOrWhiteSpace(result.Command))
            {
                AppendLog(result.Command);
            }

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                AppendLog(result.StandardOutput.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                AppendLog(result.StandardError.Trim());
            }

            for (int i = 0; i < result.Errors.Count; i++)
            {
                AppendLog("错误 " + result.Errors[i]);
            }

            if (!result.Success && !result.RequiresRemovalConfirmation)
            {
                Debug.LogError($"[Luban] {result.Message}\n{string.Join("\n", result.Errors)}");
            }
        }

        private void RefreshState()
        {
            _scanResult = LubanTableScanner.ScanProject();
            _environmentStatus = LubanInstaller.GetStatus(
                LubanToolSettings.instance);
            Repaint();
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (_logSync)
            {
                _logs.Add(
                    $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}");
                if (_logs.Count > 300)
                {
                    _logs.RemoveRange(0, _logs.Count - 300);
                }
            }
        }

        private static void RevealDirectory(string path)
        {
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static void DrawStatusLine(
            string label,
            bool success,
            string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(80f));
                GUIStyle statusStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold
                };
                statusStyle.normal.textColor = success
                    ? new Color(0.15f, 0.55f, 0.25f)
                    : new Color(0.75f, 0.25f, 0.2f);
                EditorGUILayout.LabelField(
                    success ? "正常" : "异常",
                    statusStyle,
                    GUILayout.Width(48f));
                EditorGUILayout.SelectableLabel(
                    value ?? string.Empty,
                    EditorStyles.miniLabel,
                    GUILayout.Height(18f));
            }
        }
    }
}
