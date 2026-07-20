using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Networking;

namespace Sheng.GameFramework.Editor.Updater
{
    /// <summary>
    /// 检查 GitHub 最新提交并更新 UPM 依赖
    /// </summary>
    [InitializeOnLoad]
    public static class FrameworkUpdateChecker
    {
        private const string MenuRoot = "Sheng Game Framework/Framework/";
        private const string CheckMenu = MenuRoot + "检查更新";
        private const string AutoCheckMenu = MenuRoot + "每天自动检查";
        private const string AutoCheckKey =
            "Sheng.GameFramework.Updater.AutoCheck";
        private const string LastCheckKey =
            "Sheng.GameFramework.Updater.LastCheckUtcTicks";
        private const long CheckIntervalTicks = TimeSpan.TicksPerDay;

        private static UnityWebRequest _request;
        private static FrameworkUpdateContext _context;
        private static bool _interactive;

        static FrameworkUpdateChecker()
        {
            if (!Application.isBatchMode)
            {
                EditorApplication.delayCall += TryAutomaticCheck;
            }
        }

        public static bool AutoCheckEnabled
        {
            get => EditorPrefs.GetBool(AutoCheckKey, true);
            set => EditorPrefs.SetBool(AutoCheckKey, value);
        }

        [MenuItem(CheckMenu, priority = 1)]
        public static void CheckNow()
        {
            BeginCheck(true);
        }

        [MenuItem(AutoCheckMenu, priority = 20)]
        private static void ToggleAutomaticCheck()
        {
            AutoCheckEnabled = !AutoCheckEnabled;
            Menu.SetChecked(AutoCheckMenu, AutoCheckEnabled);
        }

        [MenuItem(AutoCheckMenu, true)]
        private static bool ValidateAutomaticCheck()
        {
            Menu.SetChecked(AutoCheckMenu, AutoCheckEnabled);
            return true;
        }

        private static void TryAutomaticCheck()
        {
            if (!AutoCheckEnabled || !IsAutomaticCheckDue())
            {
                return;
            }

            BeginCheck(false);
        }

        private static bool IsAutomaticCheckDue()
        {
            string storedTicks = EditorPrefs.GetString(LastCheckKey, string.Empty);
            if (!long.TryParse(storedTicks, out long lastCheckTicks))
            {
                return true;
            }

            long elapsedTicks = DateTime.UtcNow.Ticks - lastCheckTicks;
            return elapsedTicks < 0 || elapsedTicks >= CheckIntervalTicks;
        }

        private static void BeginCheck(bool interactive)
        {
            if (_request != null)
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "框架更新",
                        "正在检查更新，请稍候",
                        "确定");
                }

                return;
            }

            if (!FrameworkUpdateUtility.TryCreateContext(
                    out _context,
                    out string contextError))
            {
                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "框架更新",
                        contextError,
                        "确定");
                }

                return;
            }

            _interactive = interactive;
            EditorPrefs.SetString(
                LastCheckKey,
                DateTime.UtcNow.Ticks.ToString());

            _request = UnityWebRequest.Get(
                FrameworkUpdateUtility.LatestCommitFeed);
            _request.SetRequestHeader(
                "Accept",
                "application/atom+xml");
            _request.SetRequestHeader(
                "User-Agent",
                "Sheng-Game-Framework-Updater");
            _request.timeout = 15;
            _request.SendWebRequest();
            EditorApplication.update -= PollRequest;
            EditorApplication.update += PollRequest;
        }

        private static void PollRequest()
        {
            if (_request == null || !_request.isDone)
            {
                return;
            }

            EditorApplication.update -= PollRequest;
            UnityWebRequest request = _request;
            _request = null;

            bool requestSucceeded =
                request.result == UnityWebRequest.Result.Success;
            string response = request.downloadHandler?.text ?? string.Empty;
            string requestError = request.error;
            request.Dispose();

            if (!requestSucceeded)
            {
                ReportFailure($"连接 GitHub 失败 {requestError}");
                return;
            }

            if (!FrameworkUpdateUtility.TryParseLatestRevision(
                    response,
                    out string latestRevision,
                    out string parseError))
            {
                ReportFailure(parseError);
                return;
            }

            if (FrameworkUpdateUtility.IsSameRevision(
                    _context.InstalledRevision,
                    latestRevision))
            {
                if (_interactive)
                {
                    EditorUtility.DisplayDialog(
                        "框架更新",
                        $"当前已经是最新版本 {FrameworkUpdateUtility.ShortRevision(latestRevision)}",
                        "确定");
                }

                return;
            }

            bool updateNow = EditorUtility.DisplayDialog(
                "发现框架更新",
                $"当前提交 {FrameworkUpdateUtility.ShortRevision(_context.InstalledRevision)}\n"
                + $"最新提交 {FrameworkUpdateUtility.ShortRevision(latestRevision)}\n\n"
                + "更新后 Package Manager 会重新下载框架并触发脚本编译",
                "立即更新",
                "稍后");
            if (updateNow)
            {
                ApplyUpdate(latestRevision);
            }
        }

        private static void ApplyUpdate(string revision)
        {
            if (!FrameworkUpdateUtility.TryApplyRevision(
                    _context.ManifestPath,
                    revision,
                    out string updatedDependency,
                    out string error))
            {
                ReportFailure(error);
                return;
            }

            Debug.Log(
                $"[FrameworkUpdater] 已写入最新框架依赖 {updatedDependency}");
            Client.Resolve();
            EditorUtility.DisplayDialog(
                "框架更新",
                "已开始更新，请等待 Package Manager 下载和 Unity 编译完成",
                "确定");
        }

        private static void ReportFailure(string message)
        {
            Debug.LogWarning($"[FrameworkUpdater] {message}");
            if (_interactive)
            {
                EditorUtility.DisplayDialog(
                    "框架更新失败",
                    message,
                    "确定");
            }
        }
    }
}
