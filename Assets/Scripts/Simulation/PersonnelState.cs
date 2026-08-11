using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class PersonnelState
    {
        public PersonnelId Id { get; set; }

        public SiteId SiteId { get; set; }

        public int Morale { get; set; }

        public bool IsAvailable { get; set; }
    }
}
