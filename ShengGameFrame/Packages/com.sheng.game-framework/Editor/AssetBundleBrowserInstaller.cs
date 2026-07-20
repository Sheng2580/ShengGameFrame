using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Sheng.GameFramework.Editor
{
    /// <summary>
    /// 框架加载时自动安装官方 Asset Bundle Browser
    /// </summary>
    [InitializeOnLoad]
    internal static class AssetBundleBrowserInstaller
    {
        private const string PackageName = "com.unity.assetbundlebrowser";
        private const string PackageUrl =
            "https://github.com/Unity-Technologies/AssetBundles-Browser.git#1.7.0";
        private const string InstallAttemptedKey =
            "ShengGameFramework.AssetBundleBrowser.InstallAttempted";
        private const string MenuRoot = "Sheng Game Framework/AssetBundles/";

        private static AddRequest _addRequest;

        static AssetBundleBrowserInstaller()
        {
            EditorApplication.delayCall += TryAutoInstall;
        }

        [MenuItem(MenuRoot + "Install Asset Bundle Browser")]
        private static void InstallFromMenu()
        {
            SessionState.EraseBool(InstallAttemptedKey);
            BeginInstall(true);
        }

        [MenuItem(MenuRoot + "Install Asset Bundle Browser", true)]
        private static bool ValidateInstallFromMenu()
        {
            return _addRequest == null && !IsPackageInstalled();
        }

        [MenuItem(MenuRoot + "Open Asset Bundle Browser")]
        private static void OpenBrowser()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/AssetBundle Browser"))
            {
                Debug.LogWarning("[AssetBundleBrowser] 找不到可视化工具窗口 请先完成安装");
            }
        }

        [MenuItem(MenuRoot + "Open Asset Bundle Browser", true)]
        private static bool ValidateOpenBrowser()
        {
            return IsPackageInstalled();
        }

        private static void TryAutoInstall()
        {
            if (IsPackageInstalled()
                || SessionState.GetBool(InstallAttemptedKey, false))
            {
                return;
            }

            BeginInstall(false);
        }

        private static void BeginInstall(bool manualRequest)
        {
            if (_addRequest != null)
            {
                return;
            }

            if (IsPackageInstalled())
            {
                if (manualRequest)
                {
                    Debug.Log("[AssetBundleBrowser] 已安装 Asset Bundle Browser");
                }

                return;
            }

            SessionState.SetBool(InstallAttemptedKey, true);
            try
            {
                _addRequest = Client.Add(PackageUrl);
                EditorApplication.update += PollInstallRequest;
                Debug.Log("[AssetBundleBrowser] 开始安装官方 Asset Bundle Browser 1.7.0");
            }
            catch (Exception exception)
            {
                _addRequest = null;
                Debug.LogError($"[AssetBundleBrowser] 无法发起安装请求 {exception.Message}");
            }
        }

        private static void PollInstallRequest()
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= PollInstallRequest;
            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[AssetBundleBrowser] 安装完成 {_addRequest.Result.packageId}");
            }
            else
            {
                string errorMessage = _addRequest.Error != null
                    ? _addRequest.Error.message
                    : "未知错误";
                Debug.LogError(
                    $"[AssetBundleBrowser] 安装失败 {errorMessage}\n" +
                    $"可通过菜单重试 {MenuRoot}Install Asset Bundle Browser");
            }

            _addRequest = null;
        }

        private static bool IsPackageInstalled()
        {
            PackageManagerPackageInfo[] packages =
                PackageManagerPackageInfo.GetAllRegisteredPackages();
            if (packages == null)
            {
                return false;
            }

            for (int i = 0; i < packages.Length; i++)
            {
                PackageManagerPackageInfo package = packages[i];
                if (package != null
                    && string.Equals(package.name, PackageName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
