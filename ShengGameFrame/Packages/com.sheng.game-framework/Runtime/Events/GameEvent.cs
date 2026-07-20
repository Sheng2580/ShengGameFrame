using System;

namespace Sheng.GameFramework.Events
{
    /// <summary>
    /// 可由具体项目扩展的事件标识
    /// </summary>
    [Serializable]
    public readonly struct GameEvent : IEquatable<GameEvent>
    {
        private readonly string _name;

        public GameEvent(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("事件名称不能为空", nameof(name));
            }

            _name = name.Trim();
        }

        public string Name => _name ?? string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(_name);

        public static GameEvent From<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            return new GameEvent(
                typeof(TEnum).FullName + "." + value);
        }

        public bool Equals(GameEvent other)
        {
            return string.Equals(_name, other._name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GameEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _name == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(_name);
        }

        public override string ToString()
        {
            return IsValid ? _name : "<InvalidGameEvent>";
        }

        public static bool operator ==(GameEvent left, GameEvent right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameEvent left, GameEvent right)
        {
            return !left.Equals(right);
        }
    }
}
