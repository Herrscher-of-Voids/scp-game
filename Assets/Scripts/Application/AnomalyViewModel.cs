using Scp.Domain;

namespace Scp.Application
{
    public sealed class AnomalyViewModel
    {
        public ScpId Id { get; set; }

        public ObjectClass Class { get; set; }

        public SiteId SiteId { get; set; }

        public int Stability { get; set; }

        public long AccumulatedResource { get; set; }
    }
}
