using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class FailureState
    {
        public bool IsEnded { get; set; }

        public GameEndReason EndReason { get; set; }

        public int HiddenEthicsRemovalRisk { get; set; }
    }
}
