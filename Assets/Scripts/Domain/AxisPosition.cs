using System;

namespace Scp.Domain
{
    /// <summary>议会立场三轴。每轴取值 -100..100。</summary>
    public readonly struct AxisPosition : IEquatable<AxisPosition>
    {
        public AxisPosition(int containment, int personnelEthics, int veilPolicy)
        {
            Containment = containment;
            PersonnelEthics = personnelEthics;
            VeilPolicy = veilPolicy;
        }

        public int Containment { get; }

        public int PersonnelEthics { get; }

        public int VeilPolicy { get; }

        public AxisPosition Clamp()
        {
            return new AxisPosition(ClampAxis(Containment), ClampAxis(PersonnelEthics), ClampAxis(VeilPolicy));
        }

        public int DistanceTo(AxisPosition other)
        {
            return Math.Abs(Containment - other.Containment) +
                Math.Abs(PersonnelEthics - other.PersonnelEthics) +
                Math.Abs(VeilPolicy - other.VeilPolicy);
        }

        public bool Equals(AxisPosition other)
        {
            return Containment == other.Containment &&
                PersonnelEthics == other.PersonnelEthics &&
                VeilPolicy == other.VeilPolicy;
        }

        public override bool Equals(object obj)
        {
            return obj is AxisPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = Containment;
            hash = (hash * 397) ^ PersonnelEthics;
            hash = (hash * 397) ^ VeilPolicy;
            return hash;
        }

        public override string ToString()
        {
            return "AxisPosition { Containment = " + Containment +
                ", PersonnelEthics = " + PersonnelEthics +
                ", VeilPolicy = " + VeilPolicy + " }";
        }

        public static bool operator ==(AxisPosition left, AxisPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AxisPosition left, AxisPosition right)
        {
            return !left.Equals(right);
        }

        private static int ClampAxis(int value)
        {
            return value < -100 ? -100 : value > 100 ? 100 : value;
        }
    }
}
