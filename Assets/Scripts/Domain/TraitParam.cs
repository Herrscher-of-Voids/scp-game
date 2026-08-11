using System;

namespace Scp.Domain
{
    /// <summary>trait 参数。取值统一用 int 定点，禁止 float 参与关键累计。</summary>
    public readonly struct TraitParam : IEquatable<TraitParam>
    {
        public TraitParam(TraitParamKey key, int value)
        {
            Key = key;
            Value = value;
        }

        public TraitParamKey Key { get; }

        public int Value { get; }

        public bool Equals(TraitParam other)
        {
            return Key == other.Key && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TraitParam other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Key * 397) ^ Value;
        }

        public override string ToString()
        {
            return "TraitParam { Key = " + Key + ", Value = " + Value + " }";
        }

        public static bool operator ==(TraitParam left, TraitParam right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TraitParam left, TraitParam right)
        {
            return !left.Equals(right);
        }
    }
}
