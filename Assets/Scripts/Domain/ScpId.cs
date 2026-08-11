using System;

namespace Scp.Domain
{
    /// <summary>收容物编号。173 对应 SCP-173，格式化由表现层负责。</summary>
    public readonly struct ScpId : IEquatable<ScpId>
    {
        public ScpId(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public bool Equals(ScpId other)
        {
            return Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            return obj is ScpId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Number;
        }

        public override string ToString()
        {
            return "ScpId { Number = " + Number + " }";
        }

        public static bool operator ==(ScpId left, ScpId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ScpId left, ScpId right)
        {
            return !left.Equals(right);
        }
    }
}
