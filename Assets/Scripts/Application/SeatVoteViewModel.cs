using Scp.Domain;

namespace Scp.Application
{
    public sealed class SeatVoteViewModel
    {
        public SeatId SeatId { get; set; }

        public VoteChoice Choice { get; set; }
    }
}
