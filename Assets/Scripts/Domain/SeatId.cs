using System;

namespace Scp.Domain
{
    /// <summary>议会席位编号。1..13 对应 O5-1..O5-13。</summary>
    public readonly struct SeatId : IEquatable<SeatId>
    {
        public SeatId(int number)
        {
            Number = number;
        }

        public int Number { get; }

        public bool Equals(SeatId other)
        {
            return Number == other.Number;
        }

        public override bool Equals(object obj)
        {
            return obj is SeatId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Number;
        }

        public override string ToString()
        {
            return "SeatId { Number = " + Number + " }";
        }

        public static bool operator ==(SeatId left, SeatId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SeatId left, SeatId right)
        {
            return !left.Equals(right);
        }
    }
}
