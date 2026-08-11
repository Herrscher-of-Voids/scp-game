using System;

namespace Scp.Application
{
    public sealed class WorldViewModel
    {
        public long Tick { get; set; }

        public long Funds { get; set; }

        public int EthicsScore { get; set; }

        public AnomalyViewModel[] Anomalies { get; set; } = Array.Empty<AnomalyViewModel>();
    }
}
