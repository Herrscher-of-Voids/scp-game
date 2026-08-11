using System;

namespace Scp.Domain
{
    /// <summary>大事件时间线标识。</summary>
    public readonly struct ScenarioId : IEquatable<ScenarioId>
    {
        public ScenarioId(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public bool Equals(ScenarioId other)
        {
            return string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ScenarioId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Key == null ? 0 : StringComparer.Ordinal.GetHashCode(Key);
        }

        public override string ToString()
        {
            return "ScenarioId { Key = " + Key + " }";
        }

        public static bool operator ==(ScenarioId left, ScenarioId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScenarioId left, ScenarioId right)
        {
            return !left.Equals(right);
        }
    }
}
