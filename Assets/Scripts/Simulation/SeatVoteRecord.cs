using Scp.Domain;

namespace Scp.Simulation
{
    public sealed class SeatVoteRecord
    {
        public SeatId SeatId { get; set; }

        public VoteChoice Choice { get; set; }
    }
}
