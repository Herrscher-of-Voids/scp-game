using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class DomainEvent
    {
        public DomainEventKind Kind { get; set; }

        public long Tick { get; set; }

        public ScpId? ScpId { get; set; }

        public long Amount { get; set; }

        public string Detail { get; set; } = string.Empty;
    }
}
