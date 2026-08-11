namespace Scp.Domain
{
    public sealed class ContainmentRequirement
    {
        public int MinimumSecurityLevel { get; set; }

        public int RequiredObserverCapacity { get; set; }

        public int MonthlyCost { get; set; }
    }
}
