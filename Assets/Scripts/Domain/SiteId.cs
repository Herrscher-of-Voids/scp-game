using System;

namespace Scp.Domain
{
    /// <summary>站点标识。</summary>
    public readonly struct SiteId : IEquatable<SiteId>
    {
        public SiteId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(SiteId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SiteId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "SiteId { Value = " + Value + " }";
        }

        public static bool operator ==(SiteId left, SiteId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SiteId left, SiteId right)
        {
            return !left.Equals(right);
        }
    }
}
