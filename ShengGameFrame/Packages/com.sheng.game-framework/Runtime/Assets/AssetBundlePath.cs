using UnityEngine;

namespace Sheng.GameFramework.Assets
{
    public static class AssetBundlePath
    {
        public const string StreamingAssetsFolderName = "AssetBundles";

        public static string PlatformName
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.Android:
                        return "Android";
                    case RuntimePlatform.IPhonePlayer:
                        return "iOS";
                    case RuntimePlatform.WindowsEditor:
                    case RuntimePlatform.WindowsPlayer:
                        return "StandaloneWindows64";
                    case RuntimePlatform.LinuxEditor:
                    case RuntimePlatform.LinuxPlayer:
                        return "StandaloneLinux64";
                    case RuntimePlatform.WebGLPlayer:
                        return "WebGL";
                    case RuntimePlatform.OSXEditor:
                    case RuntimePlatform.OSXPlayer:
                    default:
                        return "StandaloneOSX";
                }
            }
        }

        public static string DefaultBundleRoot => Join(
            Application.streamingAssetsPath,
            StreamingAssetsFolderName,
            PlatformName);

        public static string DefaultManifestBundleName => PlatformName;

        public static string Join(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return string.Empty;
            }

            string result = string.Empty;
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                result = string.IsNullOrEmpty(result)
                    ? segment.TrimEnd('/', '\\')
                    : result.TrimEnd('/', '\\') + "/" + segment.Trim('/', '\\');
            }

            return result;
        }

        public static string NormalizeBundleName(string bundleName)
        {
            if (string.IsNullOrWhiteSpace(bundleName))
            {
                return string.Empty;
            }

            string normalizedName = bundleName.Replace('\\', '/').Trim('/');
            string[] segments = normalizedName.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(segments[i])
                    || segments[i] == "."
                    || segments[i] == "..")
                {
                    return string.Empty;
                }
            }

            return normalizedName;
        }
    }
}
