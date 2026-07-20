using System;
using Sheng.GameFramework.Assets;

namespace Sheng.GameFramework.Pooling
{
    /// <summary>
    /// 对象池唯一编号
    /// </summary>
    public readonly struct PoolKey : IEquatable<PoolKey>
    {
        private PoolKey(string value)
        {
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static PoolKey FromName(string name)
        {
            string normalizedName = name?.Trim();
            if (string.IsNullOrEmpty(normalizedName))
            {
                throw new ArgumentException("对象池名称不能为空", nameof(name));
            }

            return new PoolKey("custom://" + normalizedName);
        }

        public static PoolKey FromAsset(string bundleName, string assetName)
        {
            string normalizedBundleName = AssetBundlePath.NormalizeBundleName(bundleName);
            string normalizedAssetName = assetName?.Trim();
            if (string.IsNullOrEmpty(normalizedBundleName)
                || string.IsNullOrEmpty(normalizedAssetName))
            {
                throw new ArgumentException("Bundle 名称和资源名称不能为空");
            }

            return new PoolKey($"asset://{normalizedBundleName}/{normalizedAssetName}");
        }

        public bool Equals(PoolKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PoolKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(PoolKey left, PoolKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PoolKey left, PoolKey right)
        {
            return !left.Equals(right);
        }
    }
}
