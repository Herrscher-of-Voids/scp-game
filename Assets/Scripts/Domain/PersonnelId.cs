using System;

namespace Scp.Domain
{
    /// <summary>人员标识。</summary>
    public readonly struct PersonnelId : IEquatable<PersonnelId>
    {
        public PersonnelId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(PersonnelId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PersonnelId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "PersonnelId { Value = " + Value + " }";
        }

        public static bool operator ==(PersonnelId left, PersonnelId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PersonnelId left, PersonnelId right)
        {
            return !left.Equals(right);
        }
    }
}
