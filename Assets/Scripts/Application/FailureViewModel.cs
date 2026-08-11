using Scp.Domain;

namespace Scp.Application
{
    public sealed class FailureViewModel
    {
        public bool IsEnded { get; set; }

        public GameEndReason EndReason { get; set; }
    }
}
