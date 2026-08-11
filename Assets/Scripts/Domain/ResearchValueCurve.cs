namespace Scp.Domain
{
    public sealed class ResearchValueCurve
    {
        public int InitialValue { get; set; }

        public int DecayPerCycle { get; set; }

        public int MinimumValue { get; set; }

        public int GetValue(int completedCycles)
        {
            var value = InitialValue - (DecayPerCycle * completedCycles);
            return value < MinimumValue ? MinimumValue : value;
        }
    }
}
