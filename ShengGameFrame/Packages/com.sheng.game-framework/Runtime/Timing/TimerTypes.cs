using System;

namespace Sheng.GameFramework.Timing
{
    /// <summary>
    /// 计时器使用的时间类型
    /// </summary>
    public enum TimerTimeMode
    {
        Scaled,
        Unscaled
    }

    /// <summary>
    /// 计时器生命周期
    /// </summary>
    public enum TimerLifetime
    {
        Scene,
        Persistent
    }

    /// <summary>
    /// 计时器唯一标识
    /// </summary>
    public readonly struct TimerId : IEquatable<TimerId>
    {
        internal TimerId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(TimerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TimerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return IsValid ? Value.ToString() : "Invalid";
        }

        public static bool operator ==(TimerId left, TimerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TimerId left, TimerId right)
        {
            return !left.Equals(right);
        }
    }
}
