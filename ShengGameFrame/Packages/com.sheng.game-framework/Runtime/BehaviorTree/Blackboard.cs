using System;
using System.Collections.Generic;

namespace Sheng.GameFramework.BehaviorTree
{
    /// <summary>
    /// 带值类型约束的黑板键
    /// </summary>
    public readonly struct BlackboardKey<T> : IEquatable<BlackboardKey<T>>
    {
        public BlackboardKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("黑板键名称不能为空", nameof(name));
            }

            Name = name;
        }

        public string Name { get; }

        public bool Equals(BlackboardKey<T> other)
        {
            return string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BlackboardKey<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Name != null ? StringComparer.Ordinal.GetHashCode(Name) : 0;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// 行为树共享运行数据容器
    /// </summary>
    public sealed class Blackboard
    {
        private sealed class Entry
        {
            public Type ValueType;
            public object Value;
        }

        private readonly Dictionary<string, Entry> _values =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        public int Count => _values.Count;

        public void Set<T>(BlackboardKey<T> key, T value)
        {
            if (_values.TryGetValue(key.Name, out Entry entry))
            {
                if (entry.ValueType != typeof(T))
                {
                    throw new InvalidOperationException(
                        $"黑板键 {key.Name} 已绑定类型 {entry.ValueType.Name}");
                }

                entry.Value = value;
                return;
            }

            _values.Add(key.Name, new Entry
            {
                ValueType = typeof(T),
                Value = value
            });
        }

        public bool TryGet<T>(BlackboardKey<T> key, out T value)
        {
            if (_values.TryGetValue(key.Name, out Entry entry)
                && entry.ValueType == typeof(T))
            {
                value = entry.Value == null ? default : (T)entry.Value;
                return true;
            }

            value = default;
            return false;
        }

        public T Get<T>(BlackboardKey<T> key)
        {
            if (TryGet(key, out T value))
            {
                return value;
            }

            throw new KeyNotFoundException($"黑板中不存在键 {key.Name}");
        }

        public T GetOrDefault<T>(BlackboardKey<T> key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        public bool Contains<T>(BlackboardKey<T> key)
        {
            return _values.TryGetValue(key.Name, out Entry entry)
                   && entry.ValueType == typeof(T);
        }

        public bool Remove<T>(BlackboardKey<T> key)
        {
            return _values.Remove(key.Name);
        }

        public void Clear()
        {
            _values.Clear();
        }
    }
}
