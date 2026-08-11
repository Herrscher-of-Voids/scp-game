using System;

namespace Scp.Simulation
{
    public sealed class WorldFacts
    {
        public bool WasWorldReset { get; set; }

        public bool WorldWasRestarted { get; set; }

        public string[] KnownFactKeys { get; set; } = Array.Empty<string>();

        public string[] CouncilLegacyKeys { get; set; } = Array.Empty<string>();

        public int PersonnelTerminated { get; set; }

        public int PrivilegeUses { get; set; }

        public int AlphaOneDeployments { get; set; }

        public int OverseerCyclesServed { get; set; }
    }
}
